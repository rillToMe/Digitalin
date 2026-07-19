using be_digitalin.Sandbox.Digiflazz;

namespace be_digitalin.Sandbox;

public static class SandboxModule
{
    public static IServiceCollection AddSandbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDigiflazzSandbox(configuration);

        return services;
    }

    public static WebApplication MapSandbox(
        this WebApplication app)
    {
        app.MapDigiflazzSandbox();

        return app;
    }
}