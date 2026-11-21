using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Data;
using be_digitalin.Infrastructure.Redis;
using be_digitalin.Infrastructure.Security;
using be_digitalin.Infrastructure.Settings;
using be_digitalin.Infrastructure.Workers;
using be_digitalin.Providers;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Settings;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace be_digitalin.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var serverVersion = new MariaDbServerVersion(new Version(10, 11, 0));
        var migrationsAssembly = typeof(DigitalinDbContext).Assembly.FullName;

        services.AddDbContext<DigitalinDbContext>(options =>
            options.UseMySql(connectionString, serverVersion, mySql => mySql.MigrationsAssembly(migrationsAssembly)));

        services.AddDbContextFactory<DigitalinDbContext>(options =>
            options.UseMySql(connectionString, serverVersion, mySql => mySql.MigrationsAssembly(migrationsAssembly)),
            ServiceLifetime.Scoped);

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DigitalinDbContext>());

        // Fail-fast core settings
        services.AddOptions<DigiflazzSettings>()
            .BindConfiguration("Digiflazz")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MidtransSettings>()
            .BindConfiguration("Midtrans")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OrderPublicHashSettings>()
            .BindConfiguration("OrderPublicHash")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Storage & External Auth Providers
        services.Configure<CloudflareR2Settings>(configuration.GetSection("CloudflareR2"));
        services.AddScoped<ICloudflareR2Provider, CloudflareR2Provider>();

        services.Configure<GoogleAuthSettings>(configuration.GetSection("GoogleAuth"));
        services.AddScoped<IGoogleProvider, GoogleProvider>();

        services.Configure<FacebookAuthSettings>(configuration.GetSection("FacebookAuth"));
        services.AddHttpClient<IFacebookProvider, FacebookProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(15));

        services.Configure<RecaptchaSettings>(configuration.GetSection("Recaptcha"));

        // External HTTP clients with explicit timeouts
        services.AddHttpClient<IDigiflazzProvider, DigiflazzProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient<IMidtransProvider, MidtransProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<ITelegramProvider, TelegramProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<IRecaptchaProvider, RecaptchaProvider>(client =>
            client.Timeout = TimeSpan.FromSeconds(10));

        // Background Workers
        if (configuration.GetValue("EnableDigiflazzSync", false))
            services.AddHostedService<DigiflazzSyncWorker>();
        services.AddHostedService<OrderSweepWorker>();

        // Infrastructure Services (Settings, Token, Email, MemoryCache, Redis)
        services.AddScoped<IAppSettingsService, AppSettingsService>();
        services.AddScoped<ITokenService, TokenService>();

        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.AddSingleton<IEmailProvider, EmailProvider>();

        services.AddMemoryCache();

        var redisConnectionString = configuration.GetSection("Redis")["ConnectionString"];
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException(
                "Konfigurasi 'Redis:ConnectionString' belum diisi. " +
                "Set di appsettings.{Environment}.json atau environment variable Redis__ConnectionString.");
        }

        services.Configure<RedisSettings>(configuration.GetSection("Redis"));
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString, options =>
            {
                options.AbortOnConnectFail = false;
            }));
        services.AddSingleton<IRedisService, RedisService>();

        return services;
    }
}
