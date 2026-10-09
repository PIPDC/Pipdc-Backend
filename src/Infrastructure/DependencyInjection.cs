using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PIPDC.Application.Auth;
using PIPDC.Application.AiChat;
using PIPDC.Application.Data;
using PIPDC.Application.Email;
using PIPDC.Infrastructure.Auth;
using PIPDC.Domain.Entities;
using PIPDC.Infrastructure.Data;
using PIPDC.Infrastructure.Captcha;
using PIPDC.Infrastructure.Email;
using PIPDC.Infrastructure.Gemini;
using PIPDC.Infrastructure.HealthChecks;
using PIPDC.Infrastructure.Idempotency;
using PIPDC.Infrastructure.OpenRouter;
using PIPDC.Infrastructure.RateLimiting;

namespace PIPDC.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Application classifies unique-constraint races through this seam so it
        // never has to name Npgsql. Registered here, in the layer that owns the
        // provider-specific test.
        services.AddScoped<IUniqueViolationDetector, PostgresUniqueViolationDetector>();

        services.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;

                // Lockout settings
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.Configure<JwtSettings>(config.GetSection("JwtSettings"));
        var jwtSettings = config.GetSection("JwtSettings").Get<JwtSettings>()!;

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                    ValidTypes = new[] { "JWT" },
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    // SignalR clients (browsers/WebSocket) send the JWT as an
                    // "access_token" query parameter during negotiation. Extract
                    // it only for the SignalR hub path. REST endpoints continue
                    // to use the Authorization header, and the token validation
                    // rules above are unchanged.
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs/messaging"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins is not configured. Add allowed origins " +
                "(e.g. \"http://localhost:5173\") to appsettings.Development.json " +
                "or the Cors__AllowedOrigins__* environment variables.");
        }

        services.AddCors(options =>
            options.AddPolicy("AllowFrontend", policy =>
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()));

        services.AddAuthorization();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.Configure<GmailApiSettings>(config.GetSection("GmailApiSettings"));

        // Non-secret email configuration (currently the public contact-form
        // recipient). Secrets stay in GmailApiSettings / user secrets.
        services.Configure<EmailSettings>(config.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailService, GmailApiEmailService>();

        // Outbound email is queued and delivered by a background worker so
        // notification mail never blocks or fails a user request. The queue is
        // a singleton and in-memory: unsent messages are lost on restart.
        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<EmailQueue>());
        services.AddHostedService<EmailQueueWorker>();

        services.AddRateLimiting();

        // Idempotency and Turnstile markers are Application-layer metadata; the
        // behaviour runs here as global action filters that only engage on marked
        // actions, so controllers never name an Infrastructure type.
        services.AddScoped<IdempotencyActionFilter>();
        services.AddScoped<VerifyHumanActionFilter>();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<IdempotencyActionFilter>();
            options.Filters.Add<VerifyHumanActionFilter>();
        });

        // Cloudflare Turnstile anti-bot verification (server-side). Registered as a
        // typed HttpClient so VerifyHumanActionFilter can resolve the verifier.
        services.Configure<TurnstileSettings>(config.GetSection("Turnstile"));
        services.AddHttpClient<TurnstileVerifier>(client =>
            client.BaseAddress = new Uri("https://challenges.cloudflare.com"));

        // AI property assistant (server-side only — the API key never reaches the
        // browser). Implemented as a typed HttpClient like Turnstile. The provider is
        // selected through configuration (AiChat:Provider): "OpenRouter" routes through
        // the OpenAI-compatible OpenRouter endpoint, "Gemini" keeps Google's Generative
        // Language API directly. Both re-use the same IGeminiClient abstraction.
        var aiProvider = config.GetSection("AiChat:Provider").Value ?? "OpenRouter";
        if (string.Equals(aiProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<GeminiSettings>(config.GetSection("Gemini"));
            services.AddHttpClient<IGeminiClient, GeminiClient>(client =>
                client.BaseAddress = new Uri("https://generativelanguage.googleapis.com"));
        }
        else
        {
            services.Configure<OpenRouterSettings>(config.GetSection("OpenRouter"));
            var openRouterBase = config["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1";
            services.AddHttpClient<IGeminiClient, OpenRouterClient>(client =>
            {
                client.BaseAddress = new Uri(openRouterBase);
                client.Timeout = TimeSpan.FromSeconds(90);
            });
        }

        services.AddDatabaseHealthCheck();

        return services;
    }
}
