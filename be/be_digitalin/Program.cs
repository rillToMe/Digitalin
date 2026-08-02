using be_digitalin.Data;
using be_digitalin.Endpoints;
using be_digitalin.Services;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Workers;
using be_digitalin.Providers;
using be_digitalin.Middleware;
using be_digitalin.Sandbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Abstractions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;

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

builder.Services.Configure<DigiflazzSettings>(builder.Configuration.GetSection("Digiflazz"));

builder.Services.Configure<MidtransSettings>(builder.Configuration.GetSection("Midtrans"));

builder.Services.Configure<CloudflareR2Settings>(builder.Configuration.GetSection("CloudflareR2"));
builder.Services.AddScoped<CloudflareR2Provider>();

builder.Services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection("GoogleAuth"));
builder.Services.AddScoped<GoogleProvider>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.Configure<RecaptchaSettings>(builder.Configuration.GetSection("Recaptcha"));
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddHttpClient();

builder.Services.AddScoped<MidtransProvider>();
builder.Services.AddScoped<DigiflazzProvider>();
builder.Services.AddScoped<TelegramProvider>();
builder.Services.AddHttpClient<RecaptchaProvider>();

//Sandbox mode
builder.Services.AddSandbox(builder.Configuration);
// Worker
//di komen dulu supaya tidak langsung membuat limit rate digiflazz aktif setiap kali development
// builder.Services.AddHostedService<DigiflazzSyncWorker>();   
builder.Services.AddHostedService<be_digitalin.Workers.OrderSweepWorker>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IBannerService, BannerService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IAdminManagementService, AdminManagementService>();
builder.Services.AddScoped<ITokenService, TokenService>();

// EmailSettings
builder.Services.Configure<be_digitalin.Config.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<be_digitalin.Providers.EmailProvider>(); 

// AKTIVKAN MEMORY CACHE (Untuk nyimpan OTP 5 menit)
builder.Services.AddMemoryCache();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

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

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("OtpPolicy", config =>
    {
        config.PermitLimit = 3;
        config.Window = TimeSpan.FromMinutes(10);
        config.QueueLimit = 0;          
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Custom rejection response: 429 Too Many Requests
    options.OnRejected = (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Terlalu banyak permintaan OTP. Coba lagi dalam beberapa menit." },
            cancellationToken);
        return ValueTask.CompletedTask;
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Digitalin API", Version = "v1" });

    // Konfigurasi agar Swagger menyediakan tombol "Authorize" untuk JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Masukkan token JWT dengan format: Bearer {token Anda}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document, null),
            new List<string>()
        }
    });
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
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Digitalin API V1");
    });
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


//sandbox mode untuk check Endpoint dari external api
if (app.Environment.IsDevelopment())
{
    app.MapSandbox();
}
// Rate Limiter must be placed BEFORE Authentication/Authorization
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Maintenance Mode — runs AFTER Auth so that JWT is parsed & we can check Admin role
app.UseMaintenanceMode();

app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapBannerEndpoints();
app.MapSearchEndpoints();
app.MapVoucherEndpoints();

//admin
app.MapDashboardEndpoints();
app.MapSettingsEndpoints();
app.MapAdminManagementEndpoints();

// Endpoint ping-db untuk testing
app.MapGet("/ping-db", async (DigitalinDbContext db) => {
    return await db.Database.CanConnectAsync() 
        ? Results.Ok("Koneksi Database via EF Core Sukses! ") 
        : Results.Problem("Gagal terhubung.");
});

// ── Seed default Maintenance AppSettings (idempotent — only inserts if missing) ──
await using (var seedScope = app.Services.CreateAsyncScope())
{
    var db = seedScope.ServiceProvider.GetRequiredService<DigitalinDbContext>();
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
        if (!await db.AppSettings.AnyAsync(s => s.Key == key))
        {
            db.AppSettings.Add(new be_digitalin.Models.AppSetting { Key = key, Value = value });
        }
    }
    await db.SaveChangesAsync();

    // ── Seed default SUPER_ADMIN (idempotent — only inserts if no admins exist) ──
    if (!await db.Admins.AnyAsync())
    {
        db.Admins.Add(new be_digitalin.Models.Admin
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
