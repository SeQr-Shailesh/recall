using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SeQrRecall.Api.OpenApi;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Api.Serialization;
using SeQrRecall.Application.Common.Constants;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.SwaggerGen;
using CorsOptions = SeQrRecall.Application.Configuration.CorsOptions;

namespace SeQrRecall.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeQrRecallApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Secret) && options.Secret.Length >= 32,
                "Jwt:Secret must be at least 32 characters.")
            .ValidateOnStart();
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));
        services.AddOptions<UploadsOptions>()
            .Bind(configuration.GetSection(UploadsOptions.SectionName))
            .Validate(
                options => options.AudioMaxBytes > 0 && options.AudioMaxBytes <= UploadsOptions.HardCeilingBytes,
                "Uploads:AudioMaxBytes must be between 1 and 104857600.")
            .Validate(
                options => options.PhotoMaxBytes > 0 && options.PhotoMaxBytes <= UploadsOptions.HardCeilingBytes,
                "Uploads:PhotoMaxBytes must be between 1 and 104857600.")
            .ValidateOnStart();

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = ApiJsonSerializer.Options.PropertyNamingPolicy;
                options.JsonSerializerOptions.DefaultIgnoreCondition = ApiJsonSerializer.Options.DefaultIgnoreCondition;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        string correlationId = context.HttpContext.Items[HttpHeaderNames.CorrelationId] as string
                            ?? context.HttpContext.TraceIdentifier;
                        context.Response.Headers[HttpHeaderNames.CorrelationId] = correlationId;
                        string json = JsonSerializer.Serialize(
                            ApiResponse.Fail("Authentication is required."),
                            ApiJsonSerializer.Options);
                        await context.Response.WriteAsync(json);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        string json = JsonSerializer.Serialize(
                            ApiResponse.Fail("You are not allowed to access this resource."),
                            ApiJsonSerializer.Options);
                        await context.Response.WriteAsync(json);
                    }
                };
            });
        services.AddAuthorization();

        services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen();

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy("Default", policy =>
            {
                if (environment.IsDevelopment() && allowedOrigins.Length == 0)
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .WithExposedHeaders(HttpHeaderNames.CorrelationId);
                    return;
                }

                if (allowedOrigins.Length == 0)
                {
                    policy.SetIsOriginAllowed(_ => false);
                    return;
                }

                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders(HttpHeaderNames.CorrelationId);
            });
        });

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
            options.Preload = false;
        });

        UploadsOptions uploads = configuration.GetSection(UploadsOptions.SectionName).Get<UploadsOptions>()
            ?? new UploadsOptions();
        long bodyLimit = uploads.MultipartBodyLimitBytes();
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = bodyLimit;
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = 16 * 1024;
        });
        services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = bodyLimit;
        });
        services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = bodyLimit;
        });

        RateLimitOptions rateLimits = RateLimitRejectionWriter.Normalize(
            configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions());
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = RateLimitRejectionWriter.WriteAsync;
            options.AddPolicy(
                RateLimitPolicyNames.OtpSend,
                httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitRejectionWriter.PartitionKey(httpContext),
                    _ => SlidingWindow(rateLimits.OtpSendPermitLimit, rateLimits.OtpWindowSeconds)));
            options.AddPolicy(
                RateLimitPolicyNames.OtpVerify,
                httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitRejectionWriter.PartitionKey(httpContext),
                    _ => SlidingWindow(rateLimits.OtpVerifyPermitLimit, rateLimits.OtpWindowSeconds)));
            options.AddPolicy(
                RateLimitPolicyNames.Upload,
                httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    RateLimitRejectionWriter.PartitionKey(httpContext),
                    _ => SlidingWindow(rateLimits.UploadPermitLimit, rateLimits.UploadWindowSeconds)));
        });

        return services;
    }

    private static SlidingWindowRateLimiterOptions SlidingWindow(int permitLimit, int windowSeconds)
    {
        return new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = RateLimitRejectionWriter.Window(windowSeconds),
            SegmentsPerWindow = 4,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };
    }
}
