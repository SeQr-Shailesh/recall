using System.Text.Json;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SeQrRecall.Api.Middleware;
using Serilog;

namespace SeQrRecall.Api.Extensions;

internal static class ApplicationBuilderExtensions
{
    public static WebApplication UseSeQrRecallPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (httpContext, _, exception) =>
            {
                if (exception is not null)
                {
                    return Serilog.Events.LogEventLevel.Error;
                }

                PathString path = httpContext.Request.Path;
                if (path.StartsWithSegments("/health"))
                {
                    return Serilog.Events.LogEventLevel.Debug;
                }

                return Serilog.Events.LogEventLevel.Information;
            };
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCors("Default");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                IApiVersionDescriptionProvider provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
                foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", $"SeQr Recall {description.GroupName}");
                }
            });
        }

        app.MapControllers();

        HealthCheckOptions healthOptions = new()
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponse
        };
        app.MapHealthChecks("/health", healthOptions);

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse
        });

        return app;
    }

    private static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new { status = report.Status.ToString() };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
