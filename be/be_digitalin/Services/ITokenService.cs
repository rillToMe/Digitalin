namespace be_digitalin.Services;

/// <summary>
/// Single source of truth for issuing JWTs. Centralizes signing key, issuer,
/// audience, claim layout, and lifetime so token config can't drift between
/// admin, customer, and Google login paths.
/// </summary>
public interface ITokenService
{
    /// <summary>Admin/super-admin token: NameIdentifier + Name + Role, 12h lifetime.</summary>
    string CreateAdminToken(int adminId, string username, string role);

    /// <summary>Customer token (local or Google login): NameIdentifier + Role, 24h lifetime.</summary>
    string CreateCustomerToken(int customerId);
}
