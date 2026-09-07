using be_digitalin.Application.Ports;
using be_digitalin.Data;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Infrastructure.Workers;
using be_digitalin.Providers;
using be_digitalin.Middleware;
using be_digitalin.Infrastructure.OpenApi;
using be_digitalin.Infrastructure.Redis;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using Microsoft.AspNetCore.Authentication.Abstractions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using be_digitalin.Services.Admin;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Content;
using be_digitalin.Services.Customer;
using be_digitalin.Services.Games;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Security;
using be_digitalin.Services.Settings;

var builder = WebApplication.CreateBuilder(args);

// Mengambil connection string 
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

//  database service
var serverVersion = new MariaDbServerVersion(new Version(10, 11, 0)); 
builder.Services.AddDbContext<DigitalinDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// IDbContextFactory — required by AppSettingsService (called from Middleware, which is singleton-ish)
builder.Services.AddDbContextFactory<DigitalinDbContext>(options =>
    options.UseMySql(connectionString, serverVersion), ServiceLifetime.Scoped);

// IApplicationDbContext — pragmatic persistence port (Fase 3 Clean Architecture).
// Resolusi dipetakan ke instance yang sama dengan registrasi AddDbContext (satu instance per scope).
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<DigitalinDbContext>());

// ── Settings inti: fail-fast saat startup ──
// Jwt/Digiflazz/Midtrans adalah jalur uang & auth — config kosong harus
// mematikan aplikasi saat boot, bukan meledak jadi 500 di request pertama.
builder.Services.AddOptions<DigiflazzSettings>()
    .BindConfiguration("Digiflazz")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<MidtransSettings>()
    .BindConfiguration("Midtrans")
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<CloudflareR2Settings>(builder.Configuration.GetSection("CloudflareR2"));
builder.Services.AddScoped<ICloudflareR2Provider, CloudflareR2Provider>();

builder.Services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection("GoogleAuth"));
builder.Services.AddScoped<IGoogleProvider, GoogleProvider>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();

// ── Facebook OAuth (authorization code flow) ──
// Konfigurasi per-environment: section "FacebookAuth" / env var Facebook__*
// (AppId, AppSecret, RedirectUri, FrontendRedirectUri). Fitur opsional —
// config kosong tidak mematikan app; endpoint menolak dengan pesan jelas.
builder.Services.Configure<FacebookAuthSettings>(builder.Configuration.GetSection("FacebookAuth"));
// HTTP ke Facebook: timeout eksplisit (jangan andalkan default 100s) —
// token exchange + /me dua panggilan berurutan di jalur login customer.
builder.Services.AddHttpClient<IFacebookProvider, FacebookProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<IFacebookAuthService, FacebookAuthService>();
builder.Services.Configure<RecaptchaSettings>(builder.Configuration.GetSection("Recaptcha"));
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();

// ── External HTTP clients: explicit timeout, jangan andalkan default 100s ──
// Digiflazz 60s (price-list besar; topup tetap di-cap 30s oleh CTS caller).
builder.Services.AddHttpClient<IDigiflazzProvider, DigiflazzProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(60));
// Midtrans 20s (Snap creation sinkron di checkout path).
builder.Services.AddHttpClient<IMidtransProvider, MidtransProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(20));
// Telegram auxiliary: cepat gagal, tidak boleh menahan request.
builder.Services.AddHttpClient<ITelegramProvider, TelegramProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));
// reCAPTCHA verify singkat.
builder.Services.AddHttpClient<IRecaptchaProvider, RecaptchaProvider>(client =>
    client.Timeout = TimeSpan.FromSeconds(10));

// Worker
// DigiflazzSyncWorker: default OFF — aktifkan dengan EnableDigiflazzSync=true
// (env/config). Sync price-list periodik membakar limit rate Digiflazz saat
// development restart-restart; produksi menyalakannya lewat env var.
if (builder.Configuration.GetValue("EnableDigiflazzSync", false))
    builder.Services.AddHostedService<be_digitalin.Infrastructure.Workers.DigiflazzSyncWorker>();
builder.Services.AddHostedService<be_digitalin.Infrastructure.Workers.OrderSweepWorker>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IInvoiceLookupService, InvoiceLookupService>();
builder.Services.AddScoped<IBannerService, BannerService>();
builder.Services.AddScoped<IFaqService, FaqService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IAppSettingsService, be_digitalin.Infrastructure.Settings.AppSettingsService>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IAdminManagementService, AdminManagementService>();
builder.Services.AddScoped<ITokenService, be_digitalin.Infrastructure.Security.TokenService>();

// EmailSettings
builder.Services.Configure<be_digitalin.Config.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<IEmailProvider, be_digitalin.Providers.EmailProvider>(); 

// AKTIVKAN MEMORY CACHE (AppSettingsService — bukan untuk OTP registrasi)
builder.Services.AddMemoryCache();

// ── Redis: temporary state store (registration OTP) ──
var redisConnectionString = builder.Configuration.GetSection("Redis")["ConnectionString"];
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    throw new InvalidOperationException(
        "Konfigurasi 'Redis:ConnectionString' belum diisi. " +
        "Set di appsettings.{Environment}.json atau environment variable Redis__ConnectionString.");
}
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString, options =>
    {
        // Redis = optimization, bukan dependency wajib: app tetap start &
        // read-only endpoint fallback ke MySQL saat Redis down (fail-open),
        // sementara OTP verification tetap fail-closed di level pemakaiannya.
        options.AbortOnConnectFail = false;
    }));
builder.Services.AddSingleton<IRedisService, RedisService>();

builder.Services.AddOptions<JwtSettings>()
    .BindConfiguration("Jwt")
    .ValidateDataAnnotations()
    .ValidateOnStart();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // JWT V2 
        options.UseSecurityTokenValidators = false; 

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings!.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // AdminOnly: accessible by both ADMIN and SUPER_ADMIN
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppConstants.Roles.Admin, AppConstants.Roles.SuperAdmin);
    });

    // SuperAdminOnly: restricted to SUPER_ADMIN exclusively
    options.AddPolicy("SuperAdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppConstants.Roles.SuperAdmin);
    });
});

// ── Forwarded Headers: baca IP klien asli dari reverse proxy ──
// Tanpa ini semua request di belakang proxy terlihat datang dari IP proxy →
// rate limiter per-IP menjadi SATU bucket global (lockout seluruh situs).
// Default hanya memercayai proxy loopback (nginx/container same-host, pola
// deployment saat ini); proxy eksternal → tambahkan IP-nya ke KnownProxies.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // ── Rate limiting partitioned PER CLIENT-IP ──
    // Memperbaiki bucket global lama: 1 user tidak lagi bisa menghabiskan
    // kuota user lain. NOTE multi-instance: bucket in-memory per instance
    // (belum distributed) — konsisten dengan deployment saat ini.
    static FixedWindowRateLimiterOptions IpFixedWindow(int permitLimit, int windowMinutes) =>
        new()
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(windowMinutes),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        };

    string PartitionByIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Registrasi OTP + request reset password: 3 / 10 menit / IP.
    options.AddPolicy("OtpPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(3, 10)));

    // Customer login (lokal & Google): 10 / 5 menit / IP.
    options.AddPolicy("CustomerLoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(10, 5)));


    // Invoice lookup (GET /invoices/{orderNo} - read-only, anonymous):
    // 100 / 5 menit / IP. Cukup untuk polling halaman status (~12/menit)
    // sambil tetap menutup enumerasi nomor invoice (40-bit CSPRNG).
    options.AddPolicy("InvoiceLookupPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(100, 5)));
    // Admin login: 5 / 5 menit / IP.
    options.AddPolicy("AdminLoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(5, 5)));

    // Checkout (POST /orders — anonymous): 10 / 5 menit / IP. Tiap call
    // menulis order, mereservasi kuota voucher, dan memanggil Midtrans;
    // tanpa limit, spam = biaya API + pembakaran kuota voucher.
    options.AddPolicy("OrderPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(10, 5)));

    // Custom rejection response: 429 Too Many Requests (semua policy).
    options.OnRejected = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Terlalu banyak permintaan. Coba lagi nanti." },
            cancellationToken);
        return ValueTask.CompletedTask;
    };
});

builder.Services.AddEndpointsApiExplorer();

// ── Phase 0: MVC Controllers (migrasi Minimal API → Controllers) ──
// Belum ada controller; AddControllers() ini inert sampai MapControllers()
// dan controllers dibuat di fase berikutnya.
builder.Services.AddControllers();

// Shim ApiBehaviorOptions: saat [ApiController] auto-validasi DataAnnotations,
// bentuk error DIPAKSA kembali { message } (sesuai kontrak lama yang validasi
// manual di Minimal API). Berlaku untuk action controller saja.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    // Jangan otomatis ubah 4xx tanpa body (mis. NotFound()/BadRequest() dari
    // action) menjadi ProblemDetails — endpoint Minimal API lama mengembalikan
    // body kosong untuk kasus itu, dan kontrak harus tetap identik.
    options.SuppressMapClientErrors = true;

    options.InvalidModelStateResponseFactory = context =>
    {
        var firstMessage = context.ModelState
            .Where(state => state.Value?.Errors.Count > 0)
            .SelectMany(state => state.Value!.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));

        return new BadRequestObjectResult(new { message = firstMessage ?? "Request tidak valid." })
        {
            ContentTypes = { System.Net.Mime.MediaTypeNames.Application.Json }
        };
    };
});

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1;

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "Digitalin API";
        document.Info.Version = "v1";
        return Task.CompletedTask;
    });

    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

//  CORS - Tightened to specific headers and methods
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",  
                "http://localhost:3000",
                "http://10.208.16.245:5173")
            .WithHeaders("Authorization", "Content-Type", "Accept")   
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")               
            .AllowCredentials();
    });
});


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowReactApp");

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "0"); // Modern browsers use CSP, not XSS filter
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    // Required for Google OAuth popup flow — allows postMessage from popup to opener
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin-allow-popups");

    // HSTS - only in production
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});


// Forwarded Headers HARUS sebelum RateLimiter supaya partisi per-IP
// memakai IP klien asli (X-Forwarded-For), bukan IP proxy.
app.UseForwardedHeaders();

// Rate Limiter must be placed BEFORE Authentication/Authorization
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Maintenance Mode — runs AFTER Auth so that JWT is parsed & we can check Admin role
app.UseMaintenanceMode();

// Seluruh route API sekarang berasal dari Controllers (attribute routing;
// prefix versi ada di [Route] masing-masing via AppConstants.Api.Prefix).
// Migrasi Minimal API → Controllers selesai: folder Endpoints/ sudah dihapus.
app.MapControllers();

// (Webhook Midtrans/Digiflazz sudah dipindah ke WebhookController —
//  route /webhooks tetap root, tanpa prefix versi.)

// Endpoint ping-db untuk testing
app.MapGet("/ping-db", async (DigitalinDbContext db) => {
    return await db.Database.CanConnectAsync()
        ? Results.Ok("Koneksi Database via EF Core Sukses! ")
        : Results.Problem("Gagal terhubung.");
});

// Health / readiness probe — whitelist MaintenanceMiddleware. Liveness ringan:
// 200 tanpa cek dependensi (DB down tidak boleh mematikan probe container).
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

// ── Seed default Maintenance AppSettings (idempotent — only inserts if missing) ──
await using (var seedScope = app.Services.CreateAsyncScope())
{
    var db = seedScope.ServiceProvider.GetRequiredService<DigitalinDbContext>();
    var appSettings = seedScope.ServiceProvider.GetRequiredService<IAppSettingsService>();
    var defaults = new Dictionary<string, string>
    {
        [AppConstants.SystemSettings.MaintenanceKeys.Enabled]         = "false",
        [AppConstants.SystemSettings.MaintenanceKeys.Title]           = "Website Sedang Maintenance",
        [AppConstants.SystemSettings.MaintenanceKeys.Message]         = "Kami sedang melakukan peningkatan sistem. Silakan kembali beberapa saat lagi.",
        [AppConstants.SystemSettings.MaintenanceKeys.EstimatedFinish] = "",
        [AppConstants.SystemSettings.MaintenanceKeys.AllowAdmin]      = "true",
    };
    foreach (var (key, value) in defaults)
    {
        if (await appSettings.GetAsync(key) is null)
        {
            await appSettings.SetAsync(key, value);
        }
    }

    // Seed default SUPER_ADMIN (idempotent — only inserts if no admins exist) 
    if (!await db.Admins.AnyAsync())
    {
        db.Admins.Add(new be_digitalin.Domain.Entities.Admin
        {
            Username     = "superadmin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!", workFactor: 11),
            Role         = AppConstants.Roles.SuperAdmin,
            CreatedAt    = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}

app.Run();
