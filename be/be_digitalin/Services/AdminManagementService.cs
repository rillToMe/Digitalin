using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.DTOs;
using be_digitalin.Models;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services;

public class AdminManagementService : IAdminManagementService
{
    // Valid role values accepted by the management API
    private static readonly HashSet<string> ValidRoles =
    [
        AppConstants.Roles.Admin,
        AppConstants.Roles.SuperAdmin,
    ];

    private readonly DigitalinDbContext _db;

    public AdminManagementService(DigitalinDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminListItem>> GetAdminsAsync()
    {
        return await _db.Admins
            .OrderBy(a => a.Id)
            .Select(a => new AdminListItem(
                a.Id,
                a.Username,
                a.Role,
                a.CreatedAt,
                a.LastLoginAt))
            .ToListAsync();
    }

    public async Task<AdminActionResult> CreateAdminAsync(CreateAdminRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Username wajib diisi.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Password wajib diisi.");

        if (!ValidRoles.Contains(request.Role))
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Role tidak valid. Gunakan ADMIN atau SUPER_ADMIN.");

        // Check username uniqueness
        var exists = await _db.Admins.AnyAsync(a => a.Username == request.Username);
        if (exists)
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Username sudah digunakan.");

        var admin = new Admin
        {
            Username     = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11),
            Role         = request.Role,
            CreatedAt    = DateTime.UtcNow,
        };

        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return new AdminActionResult(AdminActionOutcome.Success, "Admin berhasil dibuat.");
    }

    public async Task<AdminActionResult> UpdateAdminRoleAsync(int id, string role, string? callerUsername)
    {
        if (!ValidRoles.Contains(role))
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Role tidak valid. Gunakan ADMIN atau SUPER_ADMIN.");

        var target = await _db.Admins.FindAsync(id);
        if (target == null)
            return new AdminActionResult(AdminActionOutcome.NotFound, "Admin tidak ditemukan.");

        // Prevent self role-change
        if (target.Username == callerUsername)
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Tidak dapat mengubah role diri sendiri.");

        target.Role = role;
        await _db.SaveChangesAsync();

        return new AdminActionResult(AdminActionOutcome.Success, "Role berhasil diperbarui.");
    }

    public async Task<AdminActionResult> ResetAdminPasswordAsync(int id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Password baru wajib diisi.");

        var target = await _db.Admins.FindAsync(id);
        if (target == null)
            return new AdminActionResult(AdminActionOutcome.NotFound, "Admin tidak ditemukan.");

        target.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 11);
        await _db.SaveChangesAsync();

        return new AdminActionResult(AdminActionOutcome.Success, "Password berhasil diperbarui.");
    }

    public async Task<AdminActionResult> DeleteAdminAsync(int id, string? callerUsername)
    {
        var target = await _db.Admins.FindAsync(id);
        if (target == null)
            return new AdminActionResult(AdminActionOutcome.NotFound, "Admin tidak ditemukan.");

        // Prevent self-delete
        if (target.Username == callerUsername)
            return new AdminActionResult(AdminActionOutcome.BadRequest, "Tidak dapat menghapus akun diri sendiri.");

        _db.Admins.Remove(target);
        await _db.SaveChangesAsync();

        return new AdminActionResult(AdminActionOutcome.Success, "Admin berhasil dihapus.");
    }
}
