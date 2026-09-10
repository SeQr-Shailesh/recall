using SeQrRecall.Api.Extensions;
using SeQrRecall.Application;
using SeQrRecall.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("SeQr Recall API starting");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "SeQrRecall");
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddSeQrRecallApi(builder.Configuration, builder.Environment);

    WebApplication app = builder.Build();
    if (app.Environment.IsProduction())
    {
        string otpProvider = app.Configuration["Otp:Provider"] ?? "Dummy";
        if (string.Equals(otpProvider, "Dummy", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Otp:Provider is Dummy in Production. Replace DummyOtpService before going live.");
        }
    }

    app.UseSeQrRecallPipeline();
    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "SeQr Recall API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
