using System.Security.Claims;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Endpoints;

public static class AdminManagementEndpoints
{
    // Valid role values accepted by the management API
    private static readonly HashSet<string> ValidRoles =
    [
        AppConstants.Roles.Admin,
        AppConstants.Roles.SuperAdmin,
    ];

    public static void MapAdminManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var adminGroup = app.MapGroup("/admin/admins")
            .WithTags("Admin Management")
            .RequireAuthorization("SuperAdminOnly");

        // ── GET /admin/admins ─────────────────────────────────────────────────
        adminGroup.MapGet("/", async ([FromServices] DigitalinDbContext db) =>
        {
            var admins = await db.Admins
                .OrderBy(a => a.Id)
                .Select(a => new AdminListItem(
                    a.Id,
                    a.Username,
                    a.Role,
                    a.CreatedAt,
                    a.LastLoginAt))
                .ToListAsync();

            return Results.Ok(admins);
        });

        // ── POST /admin/admins ────────────────────────────────────────────────
        adminGroup.MapPost("/", async (
            [FromBody] CreateAdminRequest req,
            [FromServices] DigitalinDbContext db) =>
        {
            // Validate role
            if (string.IsNullOrWhiteSpace(req.Username))
                return Results.BadRequest(new { message = "Username wajib diisi." });

            if (string.IsNullOrWhiteSpace(req.Password))
                return Results.BadRequest(new { message = "Password wajib diisi." });

            if (!ValidRoles.Contains(req.Role))
                return Results.BadRequest(new { message = "Role tidak valid. Gunakan ADMIN atau SUPER_ADMIN." });

            // Check username uniqueness
            var exists = await db.Admins.AnyAsync(a => a.Username == req.Username);
            if (exists)
                return Results.BadRequest(new { message = "Username sudah digunakan." });

            var admin = new Admin
            {
                Username     = req.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 11),
                Role         = req.Role,
                CreatedAt    = DateTime.UtcNow,
            };

            db.Admins.Add(admin);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Admin berhasil dibuat." });
        });

        // ── PUT /admin/admins/{id}/role ───────────────────────────────────────
        adminGroup.MapPut("/{id:int}/role", async (
            int id,
            [FromBody] UpdateAdminRoleRequest req,
            ClaimsPrincipal user,
            [FromServices] DigitalinDbContext db) =>
        {
            if (!ValidRoles.Contains(req.Role))
                return Results.BadRequest(new { message = "Role tidak valid. Gunakan ADMIN atau SUPER_ADMIN." });

            // Prevent self role-change
            var callerUsername = user.FindFirst(ClaimTypes.Name)?.Value;
            var target = await db.Admins.FindAsync(id);

            if (target == null)
                return Results.NotFound(new { message = "Admin tidak ditemukan." });

            if (target.Username == callerUsername)
                return Results.BadRequest(new { message = "Tidak dapat mengubah role diri sendiri." });

            target.Role = req.Role;
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Role berhasil diperbarui." });
        });

        // ── PUT /admin/admins/{id}/password ───────────────────────────────────
        adminGroup.MapPut("/{id:int}/password", async (
            int id,
            [FromBody] ResetAdminPasswordRequest req,
            [FromServices] DigitalinDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.NewPassword))
                return Results.BadRequest(new { message = "Password baru wajib diisi." });

            var target = await db.Admins.FindAsync(id);
            if (target == null)
                return Results.NotFound(new { message = "Admin tidak ditemukan." });

            target.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 11);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Password berhasil diperbarui." });
        });

        // ── DELETE /admin/admins/{id} ─────────────────────────────────────────
        adminGroup.MapDelete("/{id:int}", async (
            int id,
            ClaimsPrincipal user,
            [FromServices] DigitalinDbContext db) =>
        {
            // Prevent self-delete
            var callerUsername = user.FindFirst(ClaimTypes.Name)?.Value;
            var target = await db.Admins.FindAsync(id);

            if (target == null)
                return Results.NotFound(new { message = "Admin tidak ditemukan." });

            if (target.Username == callerUsername)
                return Results.BadRequest(new { message = "Tidak dapat menghapus akun diri sendiri." });

            db.Admins.Remove(target);
            await db.SaveChangesAsync();

            return Results.Ok(new { message = "Admin berhasil dihapus." });
        });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record AdminListItem(
    int Id,
    string Username,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record CreateAdminRequest(string Username, string Password, string Role);

public record UpdateAdminRoleRequest(string Role);

public record ResetAdminPasswordRequest(string NewPassword);
