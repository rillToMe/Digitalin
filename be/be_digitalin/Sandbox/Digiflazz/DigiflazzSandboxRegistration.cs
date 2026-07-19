using be_digitalin.Config;
using Microsoft.Extensions.Options;

namespace be_digitalin.Sandbox.Digiflazz;

public static class DigiflazzSandboxRegistration
{
    public static IServiceCollection AddDigiflazzSandbox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DigiflazzSettings>(
            configuration.GetSection("Digiflazz"));

        services.AddHttpClient<DigiflazzSandboxClient>();

        services.AddScoped<DigiflazzSandboxService>();

        return services;
    }
}