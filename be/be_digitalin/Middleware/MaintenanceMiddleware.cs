using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using be_digitalin.Constants;
using be_digitalin.DTOs.Responses;
using be_digitalin.Services;

namespace be_digitalin.Middleware;

/// <summary>
/// Intercepts every inbound request when Maintenance Mode is active.
/// Whitelisted paths and (optionally) admin JWT holders are allowed through.
/// Everyone else receives HTTP 503 with a JSON maintenance payload.
/// </summary>
public class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Paths (prefix-matched, case-insensitive) that bypass the maintenance block.
    /// </summary>
    private static readonly string[] WhitelistedPrefixes =
    [
        "/admin",                          // Admin panel — must be accessible to disable maintenance
        "/auth/local/login",               // Users can still log in (but can't transact)
        "/auth/google",                    // Google OAuth flow
        "/auth/local/request-password-reset", // Password recovery should remain open
        "/auth/local/reset-password",
        "/health",                         // Health / readiness probes
        "/swagger",                        // Swagger UI (dev)
        "/ping-db",                        // DB connectivity check (dev)
    ];

    public MaintenanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAppSettingsService appSettings)
    {
        // 1. Fast-path: whitelisted paths always pass through
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsWhitelisted(path))
        {
            await _next(context);
            return;
        }

        // 2. Check if maintenance is enabled (cached — no DB hit on every request)
        if (!await appSettings.IsMaintenanceEnabledAsync())
        {
            await _next(context);
            return;
        }

        // 3. Maintenance is ON — check admin bypass
        var config = await appSettings.GetMaintenanceConfigAsync();

        if (config.AllowAdmin && IsAdminRequest(context))
        {
            await _next(context);
            return;
        }

        // 4. Block the request with HTTP 503
        context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var body = new MaintenanceBlockedResponse(
            Maintenance:     true,
            Title:           config.Title,
            Message:         config.Message,
            EstimatedFinish: config.EstimatedFinish
        );

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsWhitelisted(string path) =>
        WhitelistedPrefixes.Any(prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns true if the request carries a valid JWT with Role=ADMIN.
    /// Note: this check is intentionally lightweight — it only reads the
    /// already-parsed ClaimsPrincipal set by the Authentication middleware.
    /// </summary>
    private static bool IsAdminRequest(HttpContext context)
    {
        var user = context.User;
        return user.Identity?.IsAuthenticated == true
            && user.IsInRole(AppConstants.Roles.Admin);
    }
}

/// <summary>Extension method for clean registration in Program.cs.</summary>
public static class MaintenanceMiddlewareExtensions
{
    public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder app) =>
        app.UseMiddleware<MaintenanceMiddleware>();
}
