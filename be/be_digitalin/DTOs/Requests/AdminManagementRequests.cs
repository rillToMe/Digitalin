namespace be_digitalin.DTOs;

public record AdminListItem(
    int Id,
    string Username,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record CreateAdminRequest(string Username, string Password, string Role);

public record UpdateAdminRoleRequest(string Role);

public record ResetAdminPasswordRequest(string NewPassword);
