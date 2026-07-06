# Admin Entity Implementation — Walkthrough

## Build Status

✅ **Build succeeded — 0 Warnings, 0 Errors**

---

## Files Changed

| File | Action | Summary |
|------|--------|---------|
| [`Models/Admin.cs`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/be_digitalin/Models/Admin.cs) | **NEW** | Admin entity with 6 exact columns per spec |
| [`Constants/AppConstants.cs`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/be_digitalin/Constants/AppConstants.cs) | **MODIFIED** | Added `SuperAdmin = "SUPER_ADMIN"` role constant |
| [`Data/DigitalinDbContext.cs`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/be_digitalin/Data/DigitalinDbContext.cs) | **MODIFIED** | Added `DbSet<Admin>`, Fluent API (varchar lengths, required, unique index on Username) |
| [`Config/JwtSettings.cs`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/be_digitalin/Config/JwtSettings.cs) | **MODIFIED** | Removed `AdminUser` / `AdminPass` (now dead) |
| [`Program.cs`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/be_digitalin/Program.cs) | **MODIFIED** | Removed BCrypt plaintext-hash block, added idempotent SUPER_ADMIN seed |
| [`Endpoints/AuthEndpoints.cs`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/be_digitalin/Endpoints/AuthEndpoints.cs) | **MODIFIED** | Admin login now queries `Admins` table, verifies BCrypt, updates `LastLoginAt` |
| [`API_CONTRACT.md`](file:///e:/Project/02_Software_Engineering/Web-App/Digitalin/be/API_CONTRACT.md) | **UPDATED** | Admin login endpoint documented under §1 Authentication |

---

## Migration Commands

Run from the `be_digitalin` project directory:

```bash
dotnet ef migrations add AddAdminsTable
dotnet ef database update
```

---

## Expected Migration Output

When `dotnet ef migrations add AddAdminsTable` runs, EF Core will generate:

### Table Created: `Admins`

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `int` | Primary Key, Auto-increment |
| `Username` | `varchar(100)` | `NOT NULL` |
| `PasswordHash` | `varchar(255)` | `NOT NULL` |
| `Role` | `varchar(50)` | `NOT NULL` |
| `CreatedAt` | `datetime(6)` | `NOT NULL` |
| `LastLoginAt` | `datetime(6)` | `NULL` (nullable) |

### Index Created

```
IX_Admins_Username (UNIQUE)
```

### Data Seed

The seed account is **NOT** in the migration — it uses the project's existing **startup-time idempotent seeding** pattern (same as `AppSettings`). The SUPER_ADMIN is created the first time the app boots against the migrated database:

| Field | Value |
|-------|-------|
| Username | `superadmin` |
| Password | `ChangeMe123!` |
| Role | `SUPER_ADMIN` |
| PasswordHash | BCrypt hash, workFactor=11 (computed at startup) |

The seed runs only if `db.Admins.AnyAsync()` returns `false` — **idempotent, safe to re-run**.

---

## Design Notes

- **JWT backward compat**: All admins (both `SUPER_ADMIN` and `ADMIN` DB roles) receive `ClaimTypes.Role = "ADMIN"` in their JWT. The existing `AdminOnly` policy and all `/admin/*` endpoints continue to work with zero changes.
- **`LastLoginAt` tracking**: Updated automatically on every successful login.
- **No `appsettings.json` dependency**: Admin credentials are entirely in the database.
