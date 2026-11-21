using System.Text;
using System.Threading.RateLimiting;
using be_digitalin.Application;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Infrastructure;
using be_digitalin.Infrastructure.OpenApi;
using be_digitalin.Middleware;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Clean Architecture Layer Registrations ──
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();

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
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

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

    // Invoice lookup (GET /invoices/{orderNo} - read-only, anonymous): 100 / 5 menit / IP.
    options.AddPolicy("InvoiceLookupPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(100, 5)));

    // Admin login: 5 / 5 menit / IP.
    options.AddPolicy("AdminLoginPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionByIp(context), _ => IpFixedWindow(5, 5)));

    // Checkout (POST /orders — anonymous): 10 / 5 menit / IP.
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
builder.Services.AddControllers();

// Shim ApiBehaviorOptions: bentuk error DIPAKSA kembali { message }.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
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

// CORS - Tightened to specific headers and methods
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
    context.Response.Headers.Append("X-XSS-Protection", "0");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin-allow-popups");

    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseForwardedHeaders();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMaintenanceMode();
app.MapControllers();

app.MapGet("/ping-db", async (DigitalinDbContext db) => {
    return await db.Database.CanConnectAsync()
        ? Results.Ok("Koneksi Database via EF Core Sukses! ")
        : Results.Problem("Gagal terhubung.");
});

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

    var publicHashSettings = seedScope.ServiceProvider
        .GetRequiredService<IOptions<OrderPublicHashSettings>>().Value;
    while (true)
    {
        var batch = await db.TopupOrders
            .Where(o => o.PublicHash == null)
            .OrderBy(o => o.Id)
            .Take(500)
            .ToListAsync();

        if (batch.Count == 0) break;

        foreach (var order in batch)
        {
            order.PublicHash = OrderPublicHash.Compute(
                order.OrderNo, publicHashSettings.SecretKey);
        }
        await db.SaveChangesAsync();
    }

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
