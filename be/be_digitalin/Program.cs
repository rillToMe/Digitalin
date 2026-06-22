using be_digitalin.Data;
using be_digitalin.Endpoints;
using be_digitalin.Services;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Workers;
using be_digitalin.Providers;
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

builder.Services.Configure<DigiflazzSettings>(builder.Configuration.GetSection("Digiflazz"));

builder.Services.Configure<MidtransSettings>(builder.Configuration.GetSection("Midtrans"));

builder.Services.Configure<CloudflareR2Settings>(builder.Configuration.GetSection("CloudflareR2"));
builder.Services.AddScoped<CloudflareR2Provider>();

builder.Services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection("GoogleAuth"));
builder.Services.AddScoped<GoogleProvider>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();

builder.Services.AddHttpClient();

builder.Services.AddScoped<MidtransProvider>();
builder.Services.AddScoped<DigiflazzProvider>();
builder.Services.AddScoped<TelegramProvider>();

// Worker

// builder.Services.AddHostedService<DigiflazzSyncWorker>();
// builder.Services.AddHostedService<be_digitalin.Workers.OrderSweepWorker>();

builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IBannerService, BannerService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// EmailSettings
builder.Services.Configure<be_digitalin.Config.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddSingleton<be_digitalin.Providers.EmailProvider>(); 

// AKTIVKAN MEMORY CACHE (Untuk nyimpan OTP 5 menit)
builder.Services.AddMemoryCache();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Auto-hash admin password if stored as plaintext 
{
    var jwtConfig = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
    if (jwtConfig != null && !string.IsNullOrEmpty(jwtConfig.AdminPass)
        && !jwtConfig.AdminPass.StartsWith("$2"))
    {
        jwtConfig.AdminPass = BCrypt.Net.BCrypt.HashPassword(jwtConfig.AdminPass, workFactor: 11);
        // Re-bind the hashed value so IOptions<JwtSettings> picks it up
        builder.Services.PostConfigure<JwtSettings>(s => s.AdminPass = jwtConfig.AdminPass);
    }
}

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
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(AppConstants.Roles.Admin);
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
                "http://127.0.0.1:5173",  // loopback IP fallback for dev
                "http://localhost:3000")
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

// Rate Limiter must be placed BEFORE Authentication/Authorization
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapBannerEndpoints();
app.MapSearchEndpoints();
app.MapVoucherEndpoints();

//admin
app.MapDashboardEndpoints();

// Endpoint ping-db untuk testing
app.MapGet("/ping-db", async (DigitalinDbContext db) => {
    return await db.Database.CanConnectAsync() 
        ? Results.Ok("Koneksi Database via EF Core Sukses! ") 
        : Results.Problem("Gagal terhubung.");
});

app.Run();
