using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using PIPDC.API.Extensions;
using PIPDC.API.Hubs;
using PIPDC.Application;
using PIPDC.Infrastructure.Data;
using PIPDC.Infrastructure.HealthChecks;
using PIPDC.Infrastructure;
using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Global request body cap: 10 MB matches the image cap, well above the small JSON
// bodies the API accepts, and far below Kestrel's default 30 MB.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, JwtSubUserIdProvider>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    // Expose the JWT bearer scheme so Scalar offers a global "Authorize" dialog
    // where the access token is entered once and attached to every request.
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste your JWT access token. Protected endpoints send it as 'Authorization: Bearer <token>'."
        };
        return Task.CompletedTask;
    });

    // Mark only operations that actually carry [Authorize] as requiring the bearer
    // token (derived from endpoint metadata, so login/register/public endpoints stay
    // public and role restrictions keep their existing enforcement server-side).
    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<AuthorizeAttribute>()
            .Any();

        if (requiresAuth)
        {
            operation.Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = new List<string>()
                }
            };
        }

        return Task.CompletedTask;
    });
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Return automatic model-state validation failures (from DataAnnotations on request
// DTOs) in the same { code, message, type } error shape the rest of the API uses.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(ms => ms.Value?.Errors.Count > 0)
            .Select(ms => $"{ms.Key}: {ms.Value!.Errors[0].ErrorMessage}");

        var message = string.Join("; ", errors);

        var error = new Error(
            "validation.requestinvalid",
            string.IsNullOrWhiteSpace(message) ? "The request is invalid." : message,
            ErrorType.Validation);

        return new BadRequestObjectResult(error);
    };
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

// Auto-migrate only outside Production: a failed block on a live deploy is an
// availability risk, and multiple instances racing the same migration are a
// concurrency hazard. Production uses controlled migrations.
if (!app.Environment.IsProduction())
{
    await dbContext.Database.MigrateAsync();
    await RoleSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    await DevelopmentSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.UseExceptionHandler();

// Forward X-Forwarded-For / -Proto headers from the reverse proxy so that
// RemoteIpAddress reflects the real client IP. Required so the rate limiter can
// partition anonymous callers by their true IP and not the proxy's. Placed as early
// as possible (immediately after error handling) so downstream middleware reads the
// corrected IP.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    KnownNetworks = { new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Loopback, 8) }
});

// Security headers: HSTS (https-only responses) + hardening headers for every API
// response. They are set for every request so error paths are covered too.
app.UseHsts();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

    if (app.Environment.IsDevelopment())
    {
        // Development: relax CSP just enough for Scalar's local UI (scripts, styles,
        // inline bootstrap). Production keeps the strict allow-nothing policy below.
        headers["Content-Security-Policy"] =
           "default-src 'self' 'unsafe-inline'; " +
           "script-src 'self' 'unsafe-inline'; " +
           "style-src 'self' 'unsafe-inline'; " +
           "img-src 'self' data:; " +
           "connect-src 'self'; " +
           "frame-ancestors 'none'; " +
           "base-uri 'none'; form-action 'none'";
    }
    else
    {
        headers["Content-Security-Policy"] =
           "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
// Rate limiting must run AFTER authentication so the global limiter can partition by
// the authenticated user's id, and BEFORE authorization so a rejected request is not
// even considered for authorization.
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

// Liveness + readiness probes (registered under Infrastructure.HealthChecks).
app.MapHealthCheckEndpoints();

app.MapHub<MessagingHub>("/hubs/messaging");

app.Run();
