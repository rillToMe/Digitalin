using be_digitalin.DTOs;

namespace be_digitalin.Services;

/// <summary>Outcome of an admin-management action — mapped to HTTP by the endpoint.</summary>
public enum AdminActionOutcome
{
    Success,
    NotFound,
    BadRequest
}

public record AdminActionResult(
    AdminActionOutcome Outcome,
    string Message);

public interface IAdminManagementService
{
    Task<IReadOnlyList<AdminListItem>> GetAdminsAsync();

    Task<AdminActionResult> CreateAdminAsync(CreateAdminRequest request);

    Task<AdminActionResult> UpdateAdminRoleAsync(int id, string role, string? callerUsername);

    Task<AdminActionResult> ResetAdminPasswordAsync(int id, string newPassword);

    Task<AdminActionResult> DeleteAdminAsync(int id, string? callerUsername);
}
