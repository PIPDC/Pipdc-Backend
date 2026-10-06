using System.Net;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using PIPDC.API.Extensions;
using PIPDC.API.Hubs;
using PIPDC.Application;
using PIPDC.Application.Conversations;
using PIPDC.Infrastructure.Data;
using PIPDC.Infrastructure.HealthChecks;
using PIPDC.Infrastructure;
using PIPDC.Domain.Common;
using PIPDC.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
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

// Enums cross the wire as their names ("Open", "FraudOrScam"), not their numeric
// ordinals. The rest of the API has always done this by taking a string in the DTO
// and calling Enum.TryParse (see PropertyService.TryResolveStatus,
// EnquiryService line 222, BlogService.ResolveStatus). This converter makes the
// remaining DTOs that declare a real enum behave the same way, in both
// directions, so a client that sends "UnderReview" is understood and a response
// reports "Open" rather than 0. Without it, System.Text.Json only accepts
// numbers and every such request fails to bind.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, JwtSubUserIdProvider>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// API versioning: the version lives in the URL path (e.g. /api/v1/properties). New
// versions are introduced by adding an [ApiVersion] to a controller and exposing the
// new route segment; older versions remain reachable while they are still supported.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    // Group documents by version (v1, v2, ...) so each version gets its own
    // OpenAPI document and Scalar reference.
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddOpenApi();
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// SignalR is an API-layer delivery mechanism, so its implementation of the
// Application layer's IMessageNotifier is registered here, next to the hub.
// Application never names this type; it only knows the interface.
builder.Services.AddScoped<IMessageNotifier, SignalRMessageNotifier>();

// One OpenAPI document per API version so Scalar (and clients) can see and select
// each version independently. The versioner configures the document transformer with
// the bearer scheme and the per-operation security markers shared by all versions.
builder.Services.Configure<Microsoft.AspNetCore.OpenApi.OpenApiOptions>("v1", options =>
{
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
