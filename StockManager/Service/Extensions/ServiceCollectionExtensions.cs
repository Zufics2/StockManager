using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using StockManager.Abstract;
using StockManager.Models.Entities;
using StockManager.Models.Options;
using StockManager.Service.Data;
using StockManager.Service.Filters;
using StockManager.Service.Mappings;
using StockManager.Service.Services;

namespace StockManager.Service.Extensions;

public static class ServiceCollectionExtensions
{
    public const string CorsPolicy = "DefaultCors";
    public const string AuthRateLimitPolicy = "auth";

    // ---------------- MVC / JSON / validation ----------------
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                options.Filters.Add<ValidationActionFilter>();
                // non-nullable reference properties are NOT implicitly [Required]; FluentValidation gives nicer messages
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            });

        services.AddValidatorsFromAssemblyContaining<Program>();
        return services;
    }

    // ---------------- EF Core + Identity ----------------
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services;
    }

    // ---------------- Application services, MediatR, AutoMapper ----------------
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.Section));
        services.Configure<CurrencyOptions>(configuration.GetSection(CurrencyOptions.Section));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.Section));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.Section));

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IFileService, FileService>();

        services.AddAutoMapper(typeof(MappingProfile).Assembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        return services;
    }

    // ---------------- External API: HttpClient + Polly ----------------
    public static IServiceCollection AddCurrencyClient(this IServiceCollection services, IConfiguration configuration)
    {
        var currency = configuration.GetSection(CurrencyOptions.Section).Get<CurrencyOptions>() ?? new CurrencyOptions();

        services
            .AddHttpClient<ICurrencyService, CurrencyService>(client =>
            {
                client.BaseAddress = new Uri(currency.BaseUrl);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("StockManager/1.0");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            })
            .AddResilienceHandler("currency-api", pipeline =>
            {
                // 1) retry transient failures (5xx, 408, 429, network errors) with exponential back-off + jitter
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = currency.RetryCount,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(500)
                });

                // 2) stop hammering a provider that is clearly down
                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.5,
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30)
                });

                // 3) per-attempt timeout
                pipeline.AddTimeout(TimeSpan.FromSeconds(currency.TimeoutSeconds));
            });

        return services;
    }

    // ---------------- JWT ----------------
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
            throw new InvalidOperationException(
                "Jwt:Key is missing or shorter than 32 bytes. Set it via user-secrets or the Jwt__Key environment variable.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false; // keep "sub", "name", "role" as issued
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorization();
        return services;
    }

    // ---------------- Swagger ----------------
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "StockManager API",
                Version = "v1",
                Description = "Inventory management: products, warehouses, suppliers and stock movements."
            });

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the JWT returned by /api/auth/login (without the 'Bearer ' prefix).",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    // ---------------- CORS ----------------
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
            options.AddPolicy(CorsPolicy, policy =>
            {
                if (origins.Length > 0)
                    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }));

        return services;
    }

    // ---------------- Rate limiting ----------------
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions();
        var window = TimeSpan.FromSeconds(Math.Max(1, limits.WindowSeconds));

        static string Client(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // every client (IP) may make N requests per window ...
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(Client(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            // ... and login/register are throttled much harder (brute-force protection)
            options.AddPolicy(AuthRateLimitPolicy, ctx =>
                RateLimitPartition.GetFixedWindowLimiter("auth:" + Client(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.AuthPermitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Rate limit exceeded. Please slow down and retry later."
                    },
                    options: (JsonSerializerOptions?)null,
                    contentType: "application/problem+json",
                    cancellationToken: ct);
            };
        });

        return services;
    }
}
