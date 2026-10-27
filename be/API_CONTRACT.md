# Digitalin Backend - API Contract Documentation

> **Target Audience**: Frontend AI Agent  
> **Base URL**: Configured per environment (see `launchSettings.json`)  
> **Content-Type**: `application/json` unless stated otherwise  
> **Date Format**: ISO 8601 (`yyyy-MM-ddTHH:mm:ss.fffZ`)

---

## CRITICAL WARNINGS

| # | Gotcha | Detail |
|---|--------|--------|
| 1 | **Upload Cover Game uses `GameMetadata` ID** | `POST /api/v1/admin/system/metadata/{id}/upload-game-cover` — the `{id}` is a `GameMetadata.Id` |
| 2 | **Upload Item Image uses `GameProduct` ID** | `POST /api/v1/admin/products/{id}/upload-item-image` — the `{id}` is a `GameProduct.Id` |
| 3 | **`inputType` determines form rendering** | If `USER_ID_AND_ZONE_ID`, the order form MUST show a Zone/Server ID field |
| 4 | **Typo in GET /api/v1/auth/profile response** | The field is literally `"createtAt"` (NOT `"createdAt"`) — match this exactly |
| 5 | **PlayerId concatenation has NO separator** | `playerId = targetId + zoneId` (e.g. `"12345" + "6789"` → `"123456789"`) |
| 6 | **Voucher matching is case-insensitive** | Always send voucher codes as-is; backend normalizes |
| 7 | **All decimal values use `decimal` precision** | Do NOT truncate or round on frontend — display as received |
| 8 | **Admin JWT expires in 12h, Customer JWT in 24h** | Handle token refresh/re-login accordingly |
| 9 | **Order data is a Transaction Snapshot** | `productName`, `price`, `imageUrl` are frozen at order creation. If product changes later, old orders keep old values |

---

## Authentication & Authorization Patterns

### Token Format
- **Type**: Bearer JWT
- **Header**: `Authorization: Bearer <token>`

### Roles & Claims

| Role | JWT Claim | Token Lifetime | Issued By |
|------|-----------|----------------|-----------|
| CUSTOMER | `ClaimTypes.NameIdentifier` = customerId, `ClaimTypes.Role` = "CUSTOMER" | 24 hours | `POST /api/v1/auth/local/login`, `POST /api/v1/auth/google`, `GET /api/v1/auth/facebook/callback` |
| ADMIN | `ClaimTypes.Name` = username, `ClaimTypes.Role` = `"ADMIN"` | 12 hours | `POST /api/v1/admin/auth/login` |
| SUPER_ADMIN | `ClaimTypes.Name` = username, `ClaimTypes.Role` = `"SUPER_ADMIN"` | 12 hours | `POST /api/v1/admin/auth/login` |

### Endpoint Protection Levels

| Prefix / Pattern | Auth Required | Role |
|------------------|---------------|------|
| `POST /api/v1/auth/local/*`, `POST /api/v1/auth/google`, `GET /api/v1/auth/facebook/*`, `GET /api/v1/faqs*`, `GET /api/v1/leaderboard`, `GET /api/v1/invoices/*`, `GET /api/v1/games/*` | No | Public |
| `GET /api/v1/banners/` | No | Public |
| `POST /api/v1/orders/`, `POST /api/v1/orders/preview` | Optional | Any (customerId extracted if present) |
| `GET /api/v1/auth/profile`, `PUT /api/v1/auth/profile`, `GET /api/v1/orders/my-history` | Yes | CUSTOMER |
| `/api/v1/admin/*` (`AdminOnly`) | Yes | ADMIN, SUPER_ADMIN |
| `/api/v1/admin/admins/*` (`SuperAdminOnly`) | Yes | SUPER_ADMIN only |
| `/webhooks/*` | No (signature-validated) | N/A |

---

## Order Status Flow

```
PENDING ──(payment received)──► PAID ──(Digiflazz called)──► PROCESSING
    │                                                              │
    │                                                    ┌─────────┴─────────┐
    │                                                    ▼                   ▼
    │                                               COMPLETED          NEEDS_ACTION
    │                                              (SN issued)        (fulfillment failed)
    │                                                                       │
    ├──(payment expired)──► EXPIRED                                         ├──(admin retry)──► PROCESSING
    │                                                                       └──(admin refund)──► REFUNDED
    └──(payment cancelled/denied)──► FAILED
```

| Status | Meaning |
|--------|---------|
| `PENDING` | Waiting for payment |
| `PAID` | Payment received |
| `PROCESSING` | Digiflazz fulfilling |
| `COMPLETED` | Order fulfilled, SN issued |
| `NEEDS_ACTION` | Payment received but fulfillment failed — admin intervention required |
| `FAILED` | Payment cancelled/denied |
| `EXPIRED` | Payment window expired |
| `REFUNDED` | Manual refund issued |

---

## Input Types

| Value | Meaning | Frontend Behavior |
|-------|---------|-------------------|
| `USER_ID_ONLY` | Only player ID needed (e.g. Valorant, CODM) | Show single "User ID" input |
| `USER_ID_AND_ZONE_ID` | Player ID + Zone/Server (e.g. Mobile Legends) | Show "User ID" + "Zone ID" inputs |

---

# ENDPOINTS

---

## 1. Authentication

---

### POST /api/v1/admin/auth/login

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/auth/login` |
| Description | Authenticate admin using database credentials and receive JWT |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `username` | `string` | Required | Admin username (stored in `Admins` table) |
| `password` | `string` | Required | Admin password (verified via BCrypt) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminLoginResponse {
  token: string; // JWT, expires in 12 hours
}
```

#### BUSINESS RULES & EDGE CASES

- Admin credentials are stored in the `Admins` database table — **NOT** in `appsettings.json`
- Username lookup is case-sensitive (direct DB query)
- Password is verified using BCrypt (`workFactor: 11`)
- On successful login, `LastLoginAt` column is updated to `DateTime.UtcNow`
- Error 401: Username not found or password mismatch (no detail to prevent enumeration)
- JWT claims: `ClaimTypes.Name` = admin username, `ClaimTypes.Role` = actual DB role (`"ADMIN"` or `"SUPER_ADMIN"`)
- `SUPER_ADMIN` token is accepted by both `AdminOnly` and `SuperAdminOnly` policies
- `ADMIN` token is accepted only by `AdminOnly` policy
- Default seed account: `superadmin` / `ChangeMe123!` (created on first app startup if no admins exist)

---

### POST /api/v1/auth/local/request-otp

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/auth/local/request-otp` |
| Description | Sends a 6-digit OTP to the user's email for registration |
| Auth | None (Public) |
| Rate Limit | 3 requests per 10 minutes per IP |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `email` | `string` | Required | Valid email address to receive OTP |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface RequestOtpResponse {
  message: "Kode OTP berhasil dikirim ke email Anda. Berlaku 5 menit.";
}
```

#### BUSINESS RULES & EDGE CASES

- OTP is exactly 6 digits
- OTP expires after 5 minutes
- If rate limit exceeded → HTTP 429

---

### POST /api/v1/auth/local/register

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/auth/local/register` |
| Description | Register a new customer account using OTP verification |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `email` | `string` | Required | Must match the email used in request-otp |
| `password` | `string` | Required | Account password |
| `fullName` | `string` | Required | Customer's display name |
| `otpCode` | `string` | Required | 6-digit OTP received via email |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface RegisterResponse {
  message: "Registrasi sukses!";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 400: `"Kode OTP salah atau sudah kedaluwarsa."` — OTP mismatch or expired
- Error 400: `"Email sudah terdaftar."` — duplicate email
- OTP must be used within 5 minutes of issuance

---

### GET /api/v1/auth/check-email

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/auth/check-email` |
| Description | Check apakah email sudah terdaftar (dipakai form register & lupa password) |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `email` | `string` (query) | Required | Email yang dicek |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — email tersedia
interface CheckEmailResponse {
  message: string; // "Email tersedia."
}
```

#### BUSINESS RULES & EDGE CASES

- Error 400 `email` kosong: `{ message: "Email tidak boleh kosong." }`
- Error 400 email sudah terdaftar: `{ message: "Email sudah terdaftar. Silakan login." }`

---

### POST /api/v1/auth/local/login

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/auth/local/login` |
| Description | Authenticate customer and receive JWT |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `email` | `string` | Required | Registered email |
| `password` | `string` | Required | Account password |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface LoginResponse {
  token: string; // JWT, expires in 24 hours
}
```

#### BUSINESS RULES & EDGE CASES

- Error 401: Invalid email or password (generic message, does not reveal which)
- JWT claims: `NameIdentifier` = customerId (integer), `Role` = "CUSTOMER"

---

### POST /api/v1/auth/google

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/auth/google` |
| Description | Authenticate with Google ID Token (from Google Identity Services on frontend). Auto-registers if the email is new; links to existing local account if the same email already exists. Returns the same JWT shape as local login. |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `idToken` | `string` | Required | Google ID Token obtained from Google Identity Services (GIS) on the frontend |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface GoogleLoginResponse {
  token: string; // JWT, expires in 24 hours — identical to local login response
}
```

#### BUSINESS RULES & EDGE CASES

- Backend verifies the Google ID Token using the official Google SDK (signature, audience, issuer, expiration)
- **New email** → a new Customer is auto-created with `AuthProvider = "GOOGLE"`, `GoogleId = sub`, `FullName = Google display name`. Email is treated as verified.
- **Avatar**: on every successful Google login the verified profile's `picture` URL is stored/refreshed as `Customer.AvatarUrl` (nullable; preserved as-is when Google omits the picture). Provider-agnostic field - no Google-specific columns.
- **Existing local account with same email** → the account is linked by setting `GoogleId`. No duplicate account is created. Local password login continues to work.
- **Existing Google-linked account** → login proceeds normally, no DB write required.
- JWT claims are identical to local login: `NameIdentifier` = customerId, `Role` = "CUSTOMER", 24-hour expiry.
- Error 400: `"ID token tidak boleh kosong."` — missing or empty `idToken`
- Error 400: `"Invalid Google ID token."` — token failed verification (bad signature, wrong audience, expired, or malformed)
- Error 400: `"Google token verification failed."` — network or unexpected error during verification
- The backend does NOT store the Google ID token, access token, or refresh token — only `GoogleId` (the `sub` claim) and the email.

---

### GET /api/v1/auth/facebook/login

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/auth/facebook/login` |
| Description | Start Facebook OAuth (authorization code flow). Issues a CSRF `state` (stored server-side, 10 minute TTL, single-use) and responds `302` redirecting the browser to the Facebook authorization dialog (scope: `email, public_profile`). Called via full-page navigation from the frontend, NOT XHR. |
| Auth | None (Public) |
| Rate limit | `CustomerLoginPolicy` (10 / 5 min / IP) |

#### REQUEST PAYLOAD

_No request body / query params._

#### RESPONSE PAYLOAD

```
302 Found
Location: https://www.facebook.com/dialog/oauth?client_id=...&redirect_uri=...&state=...&response_type=code&scope=email,public_profile
```

#### BUSINESS RULES & EDGE CASES

- `state` is generated with CSPRNG (32 bytes, base64url) and stored in Redis keyed `digitalin:oauth:facebook:state:{state}` — validated and consumed at the callback.
- If Facebook OAuth is not configured (empty `FacebookAuth:AppId/AppSecret/RedirectUri`), the endpoint responds `302` to the frontend callback URL with `#error=Login Facebook belum dikonfigurasi di server.` (or `400 { message }` if `FrontendRedirectUri` is also unset).

---

### GET /api/v1/auth/facebook/callback

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/auth/facebook/callback` |
| Description | OAuth callback target (must be registered in the Meta App as a Valid OAuth Redirect URI). Validates `state`, exchanges the code server-to-server for the Facebook profile, then logs in / auto-registers the Facebook account and `302`-redirects the browser to the configured frontend redirect URL carrying the JWT in the URL fragment. |
| Auth | None (Public) — invoked by Facebook via browser redirect |
| Rate limit | `CustomerLoginPolicy` (10 / 5 min / IP) |

#### REQUEST PAYLOAD

| Query Param | Type | Required/Optional | Description & Constraints |
|-------------|------|-------------------|---------------------------|
| `code` | `string` | Required (on success) | Authorization code from Facebook |
| `state` | `string` | Required (on success) | CSRF state issued by `/auth/facebook/login` |
| `error` | `string` | Optional | `access_denied` when the user cancels; other values for provider-side errors |

#### RESPONSE PAYLOAD

```
302 Found
Location: {FacebookAuth:FrontendRedirectUri}#token={JWT}          // success
Location: {FacebookAuth:FrontendRedirectUri}#error={safe message} // failure
```

- The JWT is identical to local/Google login (`NameIdentifier` = customerId, `Role` = "CUSTOMER", 24h expiry) — same `ITokenService` pipeline.
- The fragment (`#token=...`) never reaches any server; the frontend callback page clears it from the address bar after reading.

#### BUSINESS RULES & EDGE CASES

- **State validation (CSRF)**: `state` must exist server-side (Redis) and is deleted BEFORE the code exchange — single-use; replay or mismatched state fails with `"Sesi login Facebook kedaluwarsa atau tidak valid. Silakan coba lagi."`
- **User cancels at Facebook** (`error=access_denied`): `#error=Login Facebook dibatalkan.` — no state consumption, no code exchange.
- **Missing code / invalid code / token exchange failure / profile failure**: user-friendly `#error=...` messages; internal details (HTTP status, AppSecret, tokens) are never exposed or logged.
- **Account separation (differs from Google!)**: the Facebook identity is the pair (`Provider = "FACEBOOK"`, `ProviderUserId` = Facebook user id) stored in the `ExternalLogins` table — lookup NEVER uses email. NO account linking/merging: the same email registered via local/Google stays a separate account from the Facebook one.
- **New Facebook user** → new Customer (`AuthProvider = "FACEBOOK"`, empty `PasswordHash`) + `ExternalLogins` row.
- **Avatar**: on every successful Facebook login the profile picture URL from the `/me` response (`picture.type(large)`, requested in the same Graph call - no extra HTTP) is stored/refreshed as `Customer.AvatarUrl` (nullable; preserved as-is when Facebook omits the picture). Provider-agnostic field - no Facebook-specific columns.
- **Email handling**: the Facebook email is used only if no other account already uses it; otherwise (or when Facebook provides no email) a deterministic placeholder `fb_{facebookUserId}@noreply.facebook.invalid` is stored — required because `Customers.Email` has a unique index.
- The backend stores no Facebook access token — only the provider user id (`ExternalLogins.ProviderUserId`).
- Facebook profile data always comes from the server-side Graph API call, never from client-supplied data.

---

### GET /api/v1/auth/profile

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/auth/profile` |
| Description | Get authenticated customer's profile |
| Auth | JWT Required (CUSTOMER) |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface ProfileResponse {
  email: string;
  fullName: string;
  createtAt: string; // ⚠️ TYPO IS INTENTIONAL - ISO datetime
  avatarUrl: string | null; // OAuth (Google/Facebook) profile picture URL; null = no avatar
}
```

#### BUSINESS RULES & EDGE CASES

- ⚠️ The field is `createtAt` (NOT `createdAt`) - this is a backend typo that must be matched exactly
- `avatarUrl` comes ONLY from the server-verified OAuth provider profile (Google `picture` / Facebook Graph `picture`), stored as-is on `Customer.AvatarUrl`. It is provider-agnostic - never `googlePictureUrl`/`facebookPictureUrl`. `null` for email/password users without an OAuth login.
- On successful Google/Facebook OAuth login the avatar is refreshed to the provider's current picture; if the provider omits the picture the existing `avatarUrl` is preserved (never overwritten with null).
- `avatarUrl` cannot be set from the client - `PUT /auth/profile` accepts only `fullName`/`newPassword`.
- Error 401: Missing/invalid token
- Error 404: Customer not found

---

### PUT /api/v1/auth/profile

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/auth/profile` |
| Description | Update authenticated customer's profile |
| Auth | JWT Required (CUSTOMER) |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `fullName` | `string` | Required | New display name |
| `newPassword` | `string \| null` | Optional | If null/empty, password remains unchanged |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdateProfileResponse {
  message: "Profil berhasil diperbarui!";
}
```

#### BUSINESS RULES & EDGE CASES

- Only `fullName` and `newPassword` can be updated
- Email cannot be changed

---



### POST /api/v1/auth/local/request-password-reset

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/auth/local/request-password-reset` |
| Description | Sends a 6-digit OTP to the user's email for password reset |
| Auth | None (Public) |
| Rate Limit | 3 requests per 10 minutes per IP (shared with OtpPolicy) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `email` | `string` | Required | Valid email address; must be already registered |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface RequestPasswordResetResponse {
  message: "Kode OTP reset password berhasil dikirim.";
}
```

#### BUSINESS RULES & EDGE CASES

- Email wajib diisi dan harus format email valid → Error 400
- Email tidak terdaftar → Error 400: `"Email tidak terdaftar."`
- OTP terdiri dari 6 digit angka acak (cryptographically secure)
- OTP berlaku selama 5 menit sejak diterbitkan
- Jika sudah ada OTP reset password aktif untuk akun yang sama, OTP lama akan otomatis ditimpa
- Rate limit: 3 request per 10 menit per IP → HTTP 429: `"Terlalu banyak permintaan OTP. Coba lagi dalam beberapa menit."`
- OTP dikirim via email dengan template khusus reset password (subject: "Reset Password — Kode OTP Digitalin")
- OTP disimpan di database pada tabel `CustomerOtp` (bukan in-memory), bertahan antar restart server

---

### POST /api/v1/auth/local/reset-password

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/auth/local/reset-password` |
| Description | Validates OTP and updates the customer's password |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `email` | `string` | Required | Valid email address |
| `otpCode` | `string` | Required | 6-digit OTP received via email |
| `newPassword` | `string` | Required | New password, minimum 8 characters |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface ResetPasswordResponse {
  message: "Password berhasil diubah.";
}
```

#### VALIDATION

| Field | Rule | Error |
|-------|------|-------|
| `email` | Required, valid format | 400: `"Email wajib diisi."` / `"Format email tidak valid."` |
| `otpCode` | Required | 400: `"Kode OTP wajib diisi."` |
| `newPassword` | Required, min 8 chars | 400: `"Password baru wajib diisi."` / `"Password baru minimal 8 karakter."` |

#### BUSINESS RULES & EDGE CASES

- Email tidak terdaftar → Error 400: `"Email tidak terdaftar."`
- OTP salah → Error 400: `"Kode OTP salah atau sudah kedaluwarsa."`
- OTP sudah expired → Error 400: `"Kode OTP salah atau sudah kedaluwarsa."`
- OTP langsung dihapus setelah percobaan validasi (baik berhasil maupun gagal) — OTP hanya dapat digunakan **satu kali**
- Jika OTP valid, password baru di-hash menggunakan BCrypt sebelum disimpan
- Setelah reset berhasil, user harus login ulang menggunakan password baru

---

---

## 1.1. Admin Management

> Auth: **SuperAdminOnly** (`SUPER_ADMIN` role required for all endpoints below)

---

### GET /api/v1/admin/admins

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/admins` |
| Description | Retrieve list of all admins |
| Auth | SuperAdminOnly (SUPER_ADMIN) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminListItem {
  id: number;
  username: string;
  role: "ADMIN" | "SUPER_ADMIN";
  createdAt: string; // ISO 8601
  lastLoginAt: string | null; // ISO 8601 or null
}
type AdminListResponse = AdminListItem[];
```

#### BUSINESS RULES & EDGE CASES

- Returns all admins ordered by Id ascending
- `lastLoginAt` is `null` if the admin has never logged in
- PasswordHash is **never** included in the response

---

### POST /api/v1/admin/admins

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/admins` |
| Description | Create a new admin account |
| Auth | SuperAdminOnly (SUPER_ADMIN) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|--------------------------|
| `username` | `string` | Required | Unique admin username |
| `password` | `string` | Required | Plaintext password — hashed with BCrypt (workFactor: 11) before storing |
| `role` | `string` | Required | `"ADMIN"` or `"SUPER_ADMIN"` |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface CreateAdminResponse {
  message: "Admin berhasil dibuat.";
}
```

#### VALIDATION

| Field | Rule | Error |
|-------|------|-------|
| `username` | Required, non-empty | 400: `"Username wajib diisi."` |
| `password` | Required, non-empty | 400: `"Password wajib diisi."` |
| `role` | Must be `ADMIN` or `SUPER_ADMIN` | 400: `"Role tidak valid. Gunakan ADMIN atau SUPER_ADMIN."` |
| `username` | Must be unique in `Admins` table | 400: `"Username sudah digunakan."` |

---

### PUT /api/v1/admin/admins/{id}/role

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/admins/{id}/role` |
| Description | Update the role of an existing admin |
| Auth | SuperAdminOnly (SUPER_ADMIN) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|--------------------------|
| `role` | `string` | Required | `"ADMIN"` or `"SUPER_ADMIN"` |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdateRoleResponse {
  message: "Role berhasil diperbarui.";
}
```

#### VALIDATION & ERRORS

| Condition | Response |
|-----------|----------|
| `role` not `ADMIN` / `SUPER_ADMIN` | 400: `"Role tidak valid. Gunakan ADMIN atau SUPER_ADMIN."` |
| Admin with `{id}` not found | 404: `"Admin tidak ditemukan."` |
| Caller attempts to change own role | 400: `"Tidak dapat mengubah role diri sendiri."` |

---

### PUT /api/v1/admin/admins/{id}/password

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/admins/{id}/password` |
| Description | Reset password for an existing admin |
| Auth | SuperAdminOnly (SUPER_ADMIN) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|--------------------------|
| `newPassword` | `string` | Required | Plaintext new password — hashed with BCrypt (workFactor: 11) before storing |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface ResetAdminPasswordResponse {
  message: "Password berhasil diperbarui.";
}
```

#### VALIDATION & ERRORS

| Condition | Response |
|-----------|----------|
| `newPassword` empty or missing | 400: `"Password baru wajib diisi."` |
| Admin with `{id}` not found | 404: `"Admin tidak ditemukan."` |

---

### DELETE /api/v1/admin/admins/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/api/v1/admin/admins/{id}` |
| Description | Permanently delete an admin account |
| Auth | SuperAdminOnly (SUPER_ADMIN) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface DeleteAdminResponse {
  message: "Admin berhasil dihapus.";
}
```

#### VALIDATION & ERRORS

| Condition | Response |
|-----------|----------|
| Admin with `{id}` not found | 404: `"Admin tidak ditemukan."` |
| Caller attempts to delete own account | 400: `"Tidak dapat menghapus akun diri sendiri."` |

## 2. Products / Catalog

---

### GET /api/v1/games/brands

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/games/brands` |
| Description | List game brands for the storefront catalog, optionally filtered by category and paginated |
| Auth | None (Public) |

#### REQUEST PAYLOAD

```typescript
// Query parameters (all optional — no parameters at all = full catalog)
interface BrandsQueryParams {
  category?: string; // storefront category slug; "all"/omitted = no filter, "lainnya" = uncategorized only
  offset?: number;   // brands to skip; default 0
  limit?: number;    // batch size; omitted = no limit (full catalog)
}
```

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type BrandsResponse = BrandItem[];

interface BrandItem {
  brand: string;
  slug: string; // auto-generated: brand.toLowerCase().replace(" ", "-")
  imageUrl: string | null;
  category: string; // Digiflazz product category, currently always "Games"
  categorySlugs: string[]; // storefront category slugs (e.g. ["mobile"], ["pc", "mobile"]); empty = uncategorized
}
```

#### BUSINESS RULES & EDGE CASES

- Results ordered alphabetically by `brand`
- `slug` is derived from brand name (lowercase, spaces → hyphens)
- Only brands with at least one active `Games` product are returned
- `categorySlugs` comes from the `GameMetadata.Categories` column (comma-separated slugs), editable via `PUT /api/v1/admin/system/metadata/{id}`. A game may belong to several categories at once and then appears in each of their sections; an empty array means the brand is not categorized yet (frontend groups it under "Lainnya")
- `category` is normalized like any other slug (trim → lowercase → spaces to hyphens), so `?category=Lainnya` and `?category=lainnya` are the same filter. Two virtual buckets are accepted here but can never be created by an admin (reserved slugs): `all` = no filter, `lainnya` = brands whose `categorySlugs` is empty
- A `category` that no brand uses returns `[]`, not the whole catalog
- `limit` is clamped to **1..60**; a larger value is silently capped. `offset` below 0 becomes 0, and an offset past the end returns `[]`. Omitting `limit` returns everything after `offset` — kept for callers that legitimately want the full catalog
- Pagination is **offset-based on purpose**: the homepage first batch is deliberately smaller than the "show more" batch (12/6 then 24), which page numbers cannot express without skipping or repeating brands. The caller advances `offset` by the number of brands it already holds
- The section total is not returned here — use `brandCount` from `GET /api/v1/games/categories`, so a section knows when to stop without an extra request

---

### GET /api/v1/games/brands/popular

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/games/brands/popular` |
| Description | Game brands manually curated by admin for the "Populer" section at the top of the homepage |
| Auth | None (Public) |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK — same shape as GET /api/v1/games/brands, ordered by the admin selection
type BrandsResponse = BrandItem[];
```

#### BUSINESS RULES & EDGE CASES

- The list is manually curated through `PUT /api/v1/admin/system/popular-games`; array order is preserved on the homepage
- Capped at **12** brands (one full homepage rail)
- A selected brand is dropped from the public response if it has no `GameMetadata` row or no active `Games` product left, while the saved setting remains unchanged
- Missing, blank, or malformed `AppSettings["POPULAR_GAMES"]` degrades to `[]` instead of failing the storefront
- Returns `[]` before an admin selects any game. The frontend hides the section entirely rather than rendering an empty heading
- `"populer"` is a reserved storefront category slug (`PUT /api/v1/admin/system/game-categories` rejects it) — this section is virtual and must not collide with a manually created category. A brand listed here still shows in its own categories' sections too

---

### GET /api/v1/games/brands/recent

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/games/brands/recent` |
| Description | Game brands the buyer actually purchased, most recent first — the "Terakhir Dibeli" section on the homepage |
| Auth | None (Public) — **optional-auth**: a valid customer JWT switches the source to the account's purchase history in the DB |

#### REQUEST PAYLOAD

```typescript
// Query parameters — guest only; ignored when a valid JWT is present
interface RecentBrandsQueryParams {
  slugs?: string; // comma-separated brand slugs from the guest FE cookie (e.g. "free-fire,mobile-legends"); max 12 entries
}
```

#### RESPONSE PAYLOAD

```typescript
// 200 OK — same shape as GET /api/v1/games/brands, ordered most-recent-purchase-first
type BrandsResponse = BrandItem[];
```

#### BUSINESS RULES & EDGE CASES

- **Authenticated caller (SSOT)**: brands come from the caller's own orders in the DB — `CustomerId` is read from the JWT claim (never from client input), and only orders whose money actually arrived count (`AppConstants.OrderStatus.PaidStatuses` = PAID / PROCESSING / COMPLETED / NEEDS_ACTION; PENDING / EXPIRED / FAILED / REFUNDED do not). Ordered by most recent purchase; a brand bought several times appears once (position = its newest order). The `slugs` parameter is ignored entirely
- **Guest caller**: `slugs` is only an ordering hint of public catalog identifiers — each entry is sanitized (`Domain.Slug.From`: lowercase, non-alphanumerics to hyphens) and re-validated against the active catalog; unknown, tampered, or duplicate entries are dropped/normalized silently. It is never treated as proof of purchase or authorization, and it cannot surface any private order data
- Returns `[]` when the caller has no history (guest cookie missing/invalid/expired, or a logged-in user without purchases) — the frontend hides the section entirely
- Only brands with a `GameMetadata` row and at least one active `Games` product are returned (same activeness rule as the other catalog sections), so a deactivated brand disappears from this list while the cookie/history itself is untouched
- Capped at **12** brands on both paths (`RecentBrandLimit`); the controller also caps `slugs` input at 12 entries
- A brand shown here still appears in its own categories' sections — this section is additive and does not change category ordering
- `"recently"` is a reserved storefront category slug (`PUT /api/v1/admin/system/game-categories` rejects it), same as `"populer"` / `"all"` / `"lainnya"`

---

### GET /api/v1/games/categories

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/games/categories` |
| Description | List storefront categories with their active brand counts (including the virtual `all` and `lainnya` buckets) |
| Auth | None (Public) |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type GameCategoriesResponse = GameCategoryItem[];

interface GameCategoryItem {
  slug: string;      // kebab-case, unique (e.g. "all", "mobile", "pc", "lainnya")
  name: string;      // display name (e.g. "Mobile Game"); falls back to the slug itself for unknown slugs
  brandCount: number; // brands in this category that have ≥1 active Games product
}
```

#### BUSINESS RULES & EDGE CASES

- The first entry is always the virtual bucket `all` ("Semua"), whose `brandCount` is the number of **distinct** brands with an active `Games` product — a multi-category brand is counted once here
- The last entry is the virtual bucket `lainnya` ("Lainnya") when at least one brand has no category; it is omitted when there are none. Both virtual slugs are reserved (`PUT /api/v1/admin/system/game-categories` rejects them) so a manual category can never collide with them
- Every `slug` returned here is a valid `?category=` value for `GET /api/v1/games/brands`, and its `brandCount` is exactly how many brands that call can page through — the frontend uses it to decide when to stop fetching
- Returns `[]` when no brand has an active `Games` product (empty catalog); the frontend renders no tabs at all rather than a "Semua 0" tab
- Real categories with `brandCount = 0` are omitted (not returned at all)
- Brands without a category (`GameMetadata.Categories = null`) contribute only to `all` and `lainnya`, never to a real category
- A brand with several categories is counted in **each** of them, so the sum of the real categories' `brandCount` may exceed `all`
- Result order is `all` → the admin-defined registry order (`AppSettings["GAME_CATEGORIES"]`, see `PUT /api/v1/admin/system/game-categories`) → `lainnya`; the frontend renders sections/tabs in the order received
- `name` comes from the registry (admin-editable). Slugs found on brands but missing from the registry are appended after the registry entries, sorted by slug, named from the built-in name map or the slug itself — so a brand can never silently vanish from the storefront

---

### GET /api/v1/games/{brandSlug}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/games/{brandSlug}` |
| Description | Get a specific brand's details and its available products |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `brandSlug` (path) | `string` | Required | Kebab-case slug (e.g. `"mobile-legends"`) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface BrandDetailResponse {
  brand: string;
  gameCode: string;
  inputFields: GameInputField[];
  imageUrl: string | null;
  canCheckUsername: boolean;
  products: ProductItem[];
}

interface ProductItem {
  id: number;
  name: string;
  price: number; // decimal
  gameCode: string;
  brand: string;
  category: string;
  imageUrl: string | null;
  customMargin: number | null; // decimal
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: Brand slug not found
- Products ordered by `price` ascending
- `price` is the final selling price (includes margin)
- Product selected as `usernameCheckProductId` is omitted from `products`; it is only used by the username-check endpoint.

---

### POST /api/v1/games/{brandSlug}/check-username

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/games/{brandSlug}/check-username` |
| Description | Validate game account through admin-selected Digiflazz SKU |
| Auth | None (Public) |

#### REQUEST PAYLOAD

```typescript
type UsernameCheckRequest = Record<string, string>;
```

Keys and values must follow `inputFields` from game detail. Backend validates fields then renders saved `targetTemplate` into Digiflazz `customer_no`.

#### RESPONSE PAYLOAD

```typescript
interface UsernameCheckResponse {
  isValid: boolean;
  username: string | null;
  message: string;
}
```

#### BUSINESS RULES & EDGE CASES

- Available only when `canCheckUsername` is `true`.
- The configured product must be active and belong to same game brand; otherwise `400`.
- A validation SKU is never included in purchasable game products.

---

### GET /api/v1/admin/products/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/products/` |
| Description | List all products with pagination and search (admin) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `search` | `string` | Optional | Case-insensitive substring match on `name` + `brand` |
| `page` | `integer` | Optional | Page number, default = 1, minimum = 1 |
| `limit` | `integer` | Optional | Items per page, default = 20, maximum = 100 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminProductsResponse {
  items: ProductResponse[];
  totalCount: number;
  page: number;
  limit: number;
}

interface ProductResponse {
  id: number;
  name: string;
  price: number; // decimal
  gameCode: string;
  buyerSkuCode: string;
  brand: string;
  category: string;
  imageUrl: string | null;
  customMargin: number | null; // decimal
}
```

#### BUSINESS RULES & EDGE CASES

- Search is OR-matched on `name` and `brand` (case-insensitive substring)
- If `page` exceeds total pages, returns empty `items` array

---

### POST /api/v1/admin/products/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/products/` |
| Description | Create a new product manually |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `name` | `string` | Required | Product display name |
| `price` | `decimal` | Required | Selling price |
| `gameCode` | `string` | Required | Game code identifier |
| `buyerSkuCode` | `string` | Required | Digiflazz SKU code |
| `brand` | `string` | Required | Brand name |
| `category` | `string` | Required | Product category |
| `imageUrl` | `string \| null` | Optional | Direct image URL |
| `customMargin` | `decimal \| null` | Optional | Custom margin amount |

#### RESPONSE PAYLOAD

```typescript
// 201 Created
interface ProductResponse {
  id: number;
  name: string;
  price: number;
  gameCode: string;
  buyerSkuCode: string;
  brand: string;
  category: string;
  imageUrl: string | null;
  customMargin: number | null;
}
```

#### BUSINESS RULES & EDGE CASES

- Returns HTTP 201 (not 200)

---

### POST /api/v1/admin/products/{id}/upload-item-image

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/products/{id}/upload-item-image` |
| Description | Upload/replace the image for a specific product nominal |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>`, `Content-Type: multipart/form-data` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | **GameProduct** ID |
| `file` (form) | `binary` | Required | Image file, max 2 MB |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UploadItemImageResponse {
  message: "Gambar item nominal berhasil diperbarui!";
  url: string; // New R2 public URL
}
```

#### BUSINESS RULES & EDGE CASES

- Error 400: `"File tidak boleh kosong."` — no file attached
- Error 400: `"Ukuran file terlalu besar. Maksimal 2 MB."` — exceeds 2 MB
- Error 404: Product not found
- Old image is deleted from R2 before uploading new one
- Stored in R2 folder: `icon-item`

---

### PUT /api/v1/admin/products/{id}/margin

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/products/{id}/margin` |
| Description | Set or update custom margin for a product |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | GameProduct ID |
| `margin` | `decimal \| null` | Required | New margin value (null to remove) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface SetMarginResponse {
  message: "Margin berhasil diatur!";
}
```

#### BUSINESS RULES & EDGE CASES

- Price recalculation logic: `modalPrice = currentPrice - oldMargin; newPrice = modalPrice + newMargin`
- If margin is null, price reverts to modal (base) price

---

## 3. Orders / Transactions

---

### POST /api/v1/orders/preview

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/orders/preview` |
| Description | Simulate checkout — returns price, discount, and final price WITHOUT creating an order |
| Auth | None (Public) |

#### REQUEST PAYLOAD

```typescript
interface CheckoutPreviewRequest {
  productId: number;
  voucherCode?: string | null;
}
```

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `productId` | `integer` | Required | ID of the GameProduct |
| `voucherCode` | `string \| null` | Optional | Voucher code to simulate (case-insensitive) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface CheckoutPreviewResponse {
  productId: number;
  productName: string;
  imageUrl: string | null;
  gameCode: string;
  originalPrice: number; // decimal — price before discount
  discountAmount: number; // decimal — 0 if no voucher
  finalPrice: number; // decimal — originalPrice - discountAmount
  currency: string; // "IDR"
  isVoucherApplied: boolean;
  voucherCode: string | null; // null if no voucher
  discountType: "Percentage" | "FixedAmount" | null;
  discountValue: number | null;
  maximumDiscount: number | null;
}
```

#### BUSINESS RULES & EDGE CASES

- **This endpoint does NOT create an order** — it only simulates checkout
- **This endpoint does NOT reserve voucher quota** — it only validates
- If `voucherCode` is empty/null: `discountAmount = 0`, `finalPrice = originalPrice`, `isVoucherApplied = false`
- If `voucherCode` is provided, the same validation chain runs as `POST /api/v1/orders/`:
  1. Code must exist → `"Kode voucher tidak ditemukan."`
  2. Must be active → `"Voucher ini sudah tidak aktif."`
  3. Must not be expired → `"Voucher ini sudah kedaluwarsa."`
  4. Quota available → `"Kuota voucher ini sudah habis."`
  5. Minimum transaction → `"Voucher ini membutuhkan minimal transaksi sebesar Rp{amount}."`
- Discount calculation is **identical** to `POST /api/v1/orders/` (same shared method)
- Error 400: `"Produk tidak ditemukan!"` — invalid productId
- Error 400: `"Harga akhir setelah diskon tidak boleh nol atau negatif."` — finalPrice ≤ 0

#### EXAMPLE REQUEST

```json
{ "productId": 5, "voucherCode": "HEMAT20" }
```

#### EXAMPLE RESPONSE

```json
{
  "productId": 5,
  "productName": "Mobile Legends 86 Diamonds",
  "imageUrl": "https://cdn.example.com/ml-86.webp",
  "gameCode": "mobile-legends",
  "originalPrice": 25000,
  "discountAmount": 5000,
  "finalPrice": 20000,
  "currency": "IDR",
  "isVoucherApplied": true,
  "voucherCode": "HEMAT20",
  "discountType": "Percentage",
  "discountValue": 20,
  "maximumDiscount": 10000
}
```

#### EXAMPLE: No Voucher

```json
{ "productId": 5 }
```

```json
{
  "productId": 5,
  "productName": "Mobile Legends 86 Diamonds",
  "imageUrl": "https://cdn.example.com/ml-86.webp",
  "gameCode": "mobile-legends",
  "originalPrice": 25000,
  "discountAmount": 0,
  "finalPrice": 25000,
  "currency": "IDR",
  "isVoucherApplied": false,
  "voucherCode": null,
  "discountType": null,
  "discountValue": null,
  "maximumDiscount": null
}
```

---

### POST /api/v1/orders/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/orders/` |
| Description | Create a new top-up order with optional voucher |
| Auth | Optional (JWT accepted but not required) |
| Headers | `Authorization: Bearer <token>` (optional) |
| Rate Limit | 10 requests per 5 minutes per IP |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `productId` | `integer` | Required | ID of the GameProduct to purchase |
| `targetId` | `string` | Required | Player's User ID |
| `zoneId` | `string \| null` | **CONDITIONAL** | **REQUIRED if product's `inputType` == `"USER_ID_AND_ZONE_ID"`**. Zone/Server ID |
| `voucherCode` | `string \| null` | Optional | Discount voucher code (case-insensitive) |
| `paymentMethod` | `string` | **Required** | Midtrans Core API payment method: `"qris"`, `"gopay"`, `"shopeepay"`, `"bank_transfer"`, or `"cstore"` (case-insensitive). All transactions are created via Core API. DANA/LinkAja/OVO are not Midtrans channels — customers pay from those apps via QRIS |
| `paymentBank` | `string \| null` | **CONDITIONAL** | **REQUIRED if `paymentMethod` == `"bank_transfer"`**: VA bank — `"bca"`, `"bni"`, `"bri"`, `"cimb"`, `"permata"`, `"mandiri"`, `"bsi"`. `"mandiri"` maps internally to Midtrans `echannel` (Mandiri Bill Payment) — the response returns `paymentType: "echannel"` with `billerCode`/`billKey` instructions instead of a VA number. `"bsi"` is seeded disabled (payable only via the BYOND by BSI app — not interbank). Rejected if set for other methods |
| `paymentStore` | `string \| null` | **CONDITIONAL** | **REQUIRED if `paymentMethod` == `"cstore"`**: store — `"indomaret"` or `"alfamart"`. Response returns `paymentType: "cstore"` with `store`/`paymentCode` instructions. Rejected if set for other methods |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface CreateOrderResponse {
  id: number;
  orderNo: string; // Format: "INV-{yyyyMMdd}-{10 random uppercase hex chars}", globally unique (DB-enforced)
  playerId: string; // targetId + zoneId (concatenated, NO separator)
  status: string; // Initially "PENDING"
  productName: string;
  price: number; // Original price (before discount)
  orderDate: string; // ISO datetime
  sn: string | null; // Serial number (null until COMPLETED)
  imageUrl: string | null;
  discountAmount: number; // 0 if no voucher
  finalPrice: number; // price - discountAmount
  paymentType: string | null; // Midtrans payment_type ("qris"|"gopay"|"bank_transfer")
  paymentInstructions: OrderPaymentInstructions | null; // payment instructions for the chosen method
  publicHash: string | null; // HMAC-SHA256(SecretKey, orderNo) — lowercase hex, 64 chars; null only on pre-backfill rows
}

// Core API payment instructions
interface OrderPaymentInstructions {
  qrString: string | null;   // QRIS payload — render as QR code
  actions: OrderPaymentAction[]; // e.g. generate-qr-code URL, deeplink-redirect (GoPay)
  vaBank: string | null;     // VA bank ("bca"|"bni"|"bri"|"cimb"|"permata"; permata normalized here)
  vaNumber: string | null;   // Virtual Account number to transfer to
  billerCode: string | null; // Mandiri Bill (echannel): company code (e.g. "70012")
  billKey: string | null;    // Mandiri Bill (echannel): payment code to pay at ATM/Livin'
  store: string | null;      // cstore: chosen store ("indomaret" | "alfamart")
  paymentCode: string | null; // cstore: payment code to show at the cashier
  merchantId: string | null; // cstore: Midtrans merchant id (required by the Indomaret sandbox simulator & some cashier flows)
}

interface OrderPaymentAction {
  name: string; // e.g. "generate-qr-code", "deeplink-redirect"
  url: string;
}
```

#### BUSINESS RULES & EDGE CASES

- **PlayerId construction**: `zoneId` empty → `playerId = targetId`; `zoneId` provided → `playerId = targetId + zoneId` (NO separator)
- **Voucher validation chain** (in order):
  1. Code must exist (case-insensitive) → `"Kode voucher tidak ditemukan."`
  2. Must be active → `"Voucher ini sudah tidak aktif."`
  3. Must not be expired (`expiredAt >= UtcNow`) → `"Voucher ini sudah kedaluwarsa."`
  4. Quota available (`usedCount < usageLimit`) → `"Kuota voucher ini sudah habis."`
  5. Price must meet minimum → `"Voucher ini membutuhkan minimal transaksi sebesar Rp{amount}."`
- **Voucher system**: See **Section 9 — Voucher Management** for the complete admin CRUD, models, enums, business rules, and validation reference.
- **Discount calculation** (supports two types):
  - `Percentage`: `discountAmount = price × (discountValue / 100)`, capped by `maximumDiscount` if set
  - `FixedAmount`: `discountAmount = discountValue`
- `finalPrice = price - discountAmount` (must be > 0, else Error 400: `"Harga akhir setelah diskon tidak boleh nol atau negatif."`)
- Concurrent quota race condition → `"Kuota voucher ini sudah habis (persaingan bersamaan)."`
- Error 400: `"Produk tidak ditemukan!"` — invalid productId
- **Profit snapshot**: `profitAmount = finalPrice − costPrice` where `costPrice = price − customMargin` — a voucher discount reduces profit, not cost basis (can go negative if discount > margin)
- Error 429: `"Terlalu banyak permintaan. Coba lagi nanti."` — rate limit exceeded (10 per 5 min per IP)
- Error 500: unexpected server/provider failure — generic message, no internal details leaked (business-rule rejections remain 400 `{message}`)
- OrderNo format: `"INV-{yyyyMMdd}-{10 random uppercase hex chars}"` (e.g. `"INV-20260614-A3F91B7C2D"`) — uniqueness enforced by a DB unique index
- **PublicHash generation**: computed atomically with OrderNo at creation (`HMAC-SHA256(OrderPublicHash:SecretKey, orderNo)` → lowercase hex 64, unique DB index) — new orders always return `publicHash` in the response; FE navigates to `/order/status/{publicHash}`
- **Payment method validation** (runs BEFORE any DB access, order preserved):
  1. `paymentMethod` missing/empty → `"Metode pembayaran wajib dipilih: qris, gopay, atau bank_transfer."`
  2. `paymentMethod` not one of `qris`/`gopay`/`shopeepay`/`bank_transfer`/`cstore` (case-insensitive) → `"Metode pembayaran tidak didukung. Gunakan: qris, gopay, atau bank_transfer."`
  3. `paymentMethod` == `bank_transfer` without `paymentBank` → `"Pembayaran bank_transfer wajib memilih bank VA (bca/bni/bri/cimb/permata/mandiri/bsi)."`
  4. `paymentBank` not in the VA bank whitelist → `"Bank VA tidak didukung: {bank}. Pilih bca/bni/bri/cimb/permata/mandiri/bsi."`
  5. `paymentBank` set while `paymentMethod` != `bank_transfer` → `"paymentBank hanya berlaku untuk paymentMethod bank_transfer."`
  6. `paymentMethod` == `cstore` without `paymentStore` → `"Pembayaran cstore wajib memilih toko (indomaret/alfamart)."`
  7. `paymentStore` not in the store whitelist → `"Toko tidak didukung: {store}. Pilih indomaret/alfamart."`
  8. `paymentStore` set while `paymentMethod` != `cstore` → `"paymentStore hanya berlaku untuk paymentMethod cstore."`
- **Core API charge**: the transaction is created via Midtrans Core API `POST /v2/charge` for the chosen `payment_type`; payment instructions (QR string / VA number / deeplink) are persisted on the order and returned. Payment amount is ALWAYS server-side (DB product price − voucher discount); the client never sends an amount.
- **Midtrans transaction creation failure compensation**: if the Core API charge cannot be created after the order is committed, the order is immediately set to `EXPIRED`, the reserved voucher quota is released, and the client receives Error 500 with a generic message
- **Unpaid PENDING sweep**: the background sweep (runs every 2 minutes) marks `PENDING` orders older than the Midtrans expiry setting (default 30 min, see Section — Midtrans Expiry) + 5 min buffer as `EXPIRED` and releases their reserved voucher quota — a safety net for cases where the Midtrans Expire webhook never arrives

---

### GET /api/v1/orders/my-history

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/orders/my-history` |
| Description | Get paginated order history for authenticated customer |
| Auth | JWT Required (CUSTOMER) |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `page` | `integer` | Optional | Default = 1, values < 1 are clamped to 1 |
| `limit` | `integer` | Optional | Items per page, default = 20, clamped to 1..100 |
| `status` | `string` | Optional | Filter by order status: `PENDING`, `PAID`, `PROCESSING`, `COMPLETED`, `NEEDS_ACTION`, `FAILED`, `EXPIRED`, or `REFUNDED` |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface OrderHistoryResponse {
  data: OrderResponse[];
  totalItems: number;
  totalPages: number;
  currentPage: number;
}

interface OrderResponse {
  id: number;
  orderNo: string;
  playerId: string;
  status: string;
  productName: string;
  price: number;
  orderDate: string;
  sn: string | null;
  imageUrl: string | null;
  discountAmount: number;
  finalPrice: number;
  paymentType: string | null; // Midtrans payment_type ("qris"|"gopay"|"bank_transfer"); null on pre-migration orders
  paymentInstructions: OrderPaymentInstructions | null; // payment instructions; null on pre-migration orders
  publicHash: string | null; // HMAC-SHA256(SecretKey, orderNo), lowercase hex 64 chars — FE status URL uses this (/order/status/{publicHash}); null on pre-backfill rows
}
```

#### BUSINESS RULES & EDGE CASES

- Only returns orders belonging to the authenticated customer
- Ordered by `orderDate` descending (newest first)
- **Transaction Snapshot**: `productName`, `price`, and `imageUrl` are frozen at order creation time. Even if the product is later renamed, repriced, or deleted, historical orders retain the original values.

---

### GET /api/v1/admin/orders/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/orders/` |
| Description | Get all orders (admin view) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `page` | `integer` | Optional | Default = 1, values < 1 are clamped to 1 |
| `limit` | `integer` | Optional | Items per page, default = 20, clamped to 1..100 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — same structure as /api/v1/orders/my-history
interface AdminOrdersResponse {
  data: OrderResponse[];
  totalItems: number;
  totalPages: number;
  currentPage: number;
}
```

#### BUSINESS RULES & EDGE CASES

- Returns ALL orders regardless of customer
- When `status` is set, returns only orders with that status; pagination and totals apply after filtering
- Same `OrderResponse` shape as customer endpoint

---

### POST /api/v1/admin/orders/{orderNo}/retry

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/orders/{orderNo}/retry` |
| Description | Retry failed Digiflazz fulfillment for an order |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `orderNo` (path) | `string` | Required | Order number (e.g. `"INV-20260614-A3F91B7C2D"`) |

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK (retry sent)
interface RetryResponse {
  message: "Retry berhasil dikirim ke Digiflazz! Silakan tunggu SN masuk.";
  status: string;
}

// 200 OK (already completed at Digiflazz)
interface RetryAlreadyCompleteResponse {
  message: "Order sudah selesai di Digiflazz sebelumnya. SN berhasil disimpan.";
  status: "COMPLETED";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: Order not found
- Error 400: `"Tidak bisa retry. Status pesanan saat ini adalah: {status}"` — only `NEEDS_ACTION` status allowed
- Error 400: `"Digiflazz masih memproses pesanan ini. Harap tunggu sebelum mencoba retry."` — already processing
- Error 409: `"Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat."` — concurrency lock

---

### POST /api/v1/admin/orders/{orderNo}/refund

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/orders/{orderNo}/refund` |
| Description | Mark order as refunded (manual process) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `orderNo` (path) | `string` | Required | Order number |

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface RefundResponse {
  message: "Pesanan berhasil ditandai sebagai REFUNDED. Segera hubungi pembeli untuk transfer pengembalian dana secara manual via WA.";
  status: "REFUNDED";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: Order not found
- Error 400: Only `NEEDS_ACTION` status can be refunded
- Error 409: Concurrency lock conflict
- Refund is marked only — actual money transfer is manual (via WhatsApp)

---

## 4. Banners

---

### GET /api/v1/banners/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/banners/` |
| Description | Get all active banners for storefront display |
| Auth | None (Public) |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type BannersResponse = BannerItem[];

interface BannerItem {
  id: number;
  title: string;
  imageUrl: string;
  isActive: boolean; // Always true in this response
  createdAt: string; // ISO datetime
}
```

#### BUSINESS RULES & EDGE CASES

- Only returns banners where `isActive == true`
- Ordered by `createdAt` descending (newest first)

---

### GET /api/v1/admin/banners

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/banners` |
| Description | List semua banner (aktif & non-aktif) untuk panel admin |
| Auth | Admin JWT Required (`ADMIN`/`SUPER_ADMIN`) |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type AdminBannersResponse = BannerItem[];

interface BannerItem {
  id: number;
  title: string;
  imageUrl: string;
  isActive: boolean; // Bisa false — admin melihat semua status
  createdAt: string; // ISO datetime
}
```

#### BUSINESS RULES & EDGE CASES

- Berbeda dari `GET /api/v1/banners`: tidak memfilter `isActive` (admin butuh melihat banner non-aktif)
- Error 401 tanpa token admin

---

### POST /api/v1/admin/banners/upload

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/banners/upload` |
| Description | Upload a new banner image |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>`, `Content-Type: multipart/form-data` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `title` | `string` (form field) | Required | Banner title/label |
| `file` | `binary` (form field) | Required | Image file, max 2 MB |

#### RESPONSE PAYLOAD

```typescript
// 201 Created
interface CreateBannerResponse {
  id: number;
  title: string;
  imageUrl: string;
  isActive: boolean; // true by default
  createdAt: string;
}
```

#### BUSINESS RULES & EDGE CASES

- Returns HTTP 201 (not 200)
- Banner is created as `isActive = true` by default
- Stored in R2 folder: `banners`
- Error 400: file empty or exceeds 2 MB

---

### PUT /api/v1/admin/banners/{id}/toggle

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/banners/{id}/toggle` |
| Description | Toggle a banner's active/inactive status |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | Banner ID |

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface ToggleBannerResponse {
  message: "Status banner berhasil diperbarui.";
}
```

#### BUSINESS RULES & EDGE CASES

- Flips `isActive` from true→false or false→true

---

### DELETE /api/v1/admin/banners/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/api/v1/admin/banners/{id}` |
| Description | Permanently delete a banner and its image |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | Banner ID |

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface DeleteBannerResponse {
  message: "Banner dan file gambar berhasil dibumihanguskan!";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: `"Banner tidak ditemukan."`
- Also deletes the associated image file from R2 storage

---

## 5. Dashboard (Admin)

---

### GET /api/v1/admin/dashboard/stats

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/dashboard/stats` |
| Description | Get today's order statistics summary |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface DashboardStatsResponse {
  totalRevenueToday: number; // Sum of prices for COMPLETED orders today
  totalProfitToday: number; // Sum of profitAmount (finalPrice − costPrice) for COMPLETED orders today
  totalOrdersToday: number; // All orders created today
  successOrdersToday: number; // COMPLETED orders today
  pendingOrdersToday: number; // PENDING orders today
  failedOrdersToday: number; // FAILED + EXPIRED orders today
}
```

#### BUSINESS RULES & EDGE CASES

- "Today" is calculated in **Asia/Jakarta timezone** (UTC+7)
- Revenue = sum of `finalPrice` (TotalPaid) for COMPLETED orders — read from order snapshot, NOT from current product price
- Profit = sum of `profitAmount` for COMPLETED orders — read from order snapshot, NOT from current product margin
- Failed count includes both `FAILED` and `EXPIRED` statuses
- **Dashboard never joins Product table** — all data comes from `TopupOrder` snapshot fields

---

### GET /api/v1/admin/dashboard/chart

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/dashboard/chart` |
| Description | Get revenue/profit chart data for a time range |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `range` | `string` | Required | One of: `"today"`, `"yesterday"`, `"7days"`, `"30days"`, `"custom"` |
| `startDate` | `string` | Required if `range == "custom"` | Format: `YYYY-MM-DD` |
| `endDate` | `string` | Required if `range == "custom"` | Format: `YYYY-MM-DD` |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type ChartResponse = ChartDataPoint[];

interface ChartDataPoint {
  name: string; // Time label (format depends on range)
  pendapatan: number; // Revenue (decimal)
  profit: number; // Profit (decimal)
}
```

**Time label formats by range:**

| Range | Label Format | Example |
|-------|-------------|---------|
| `today` | `"HH:00"` | `"14:00"` |
| `yesterday` | `"HH:00"` | `"09:00"` |
| `7days` | `"DD Mon"` | `"14 Jun"` |
| `30days` | `"DD/MM"` | `"14/06"` |
| `custom` | `"DD/MM"` | `"01/06"` |

#### BUSINESS RULES & EDGE CASES

- Error 400: `"Parameter startDate dan endDate wajib diisi dengan format YYYY-MM-DD."` — missing dates for custom range
- Error 400: `"endDate tidak boleh lebih awal dari startDate."` — invalid date order
- Error 400: `"Rentang tanggal maksimal adalah 31 hari."` — exceeds 31 day max
- Error 400: `"Parameter range tidak valid. Pilih: today, yesterday, 7days, 30days, atau custom."` — invalid range value

---

### GET /api/v1/admin/dashboard/balance

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/dashboard/balance` |
| Description | Get remaining Digiflazz (supplier) deposit balance |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface DigiflazzBalanceResponse {
  deposit: number; // Remaining supplier deposit in IDR (decimal)
}

// 502 Bad Gateway — Digiflazz unreachable / rejected the request
interface ErrorResponse {
  message: string; // "Gagal mengambil saldo dari Digiflazz: <reason>"
}
```

#### BUSINESS RULES & EDGE CASES

- Read **live** from Digiflazz `POST /v1/cek-saldo` on every call — nothing is stored in the database, so the number is never stale
- Upstream payload: `{ cmd: "deposit", username, sign }` where `sign = md5(username + apiKey + "depo")` — the sign suffix (`"depo"`) differs from the `cmd` value (`"deposit"`)
- Digiflazz returns success and failure in the same envelope: `{ "data": { "deposit": 500000 } }` on success, `{ "data": { "message": "...", "rc": "41" } }` on failure (e.g. IP not whitelisted). A response without `deposit` is treated as an error
- **502, not 500** — the failure belongs to the upstream supplier, not to the admin's request. Frontend must degrade to a single failed card, never block the dashboard
- No server-side cache: the frontend throttles with a 60s `staleTime` plus a manual refresh button. Add caching here if Digiflazz rate-limits become an issue

---

## 6. System / Admin Operations

---

### POST /api/v1/admin/system/sync-digiflazz

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/system/sync-digiflazz` |
| Description | Manually trigger product sync from Digiflazz API |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface SyncResponse {
  message: string; // "Keajaiban selesai! {added} produk baru, {updated} produk diupdate."
}
```

#### BUSINESS RULES & EDGE CASES

- This may take several seconds (network call to Digiflazz)
- Dynamic message includes count of added/updated products

---

### DELETE /api/v1/admin/system/clean-dummy-products

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/api/v1/admin/system/clean-dummy-products` |
| Description | Remove products with no BuyerSkuCode (dummy/test data) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface CleanDummyResponse {
  message: string; // "Sapu bersih berhasil! {count} data dummy telah dihapus."
}
```

#### BUSINESS RULES & EDGE CASES

- Deletes all products where `BuyerSkuCode` is null or empty string
- Irreversible operation

---

### GET /api/v1/admin/system/midtrans-expiry

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/system/midtrans-expiry` |
| Description | Get current Midtrans payment expiry setting |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface MidtransExpiryResponse {
  expiry: number; // Minutes, defaults to 5 if not configured
}
```

---

### PUT /api/v1/admin/system/midtrans-expiry

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/system/midtrans-expiry` |
| Description | Update Midtrans payment expiry duration |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `expiry` | `integer` | Required | Payment window duration in minutes |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdateExpiryResponse {
  message: "Waktu kedaluwarsa Midtrans berhasil diubah!";
}
```

---

### GET /api/v1/admin/system/game-templates

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/system/game-templates` |
| Description | List all built-in game input template presets (SSOT for input schema & target template) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type GameTemplateListResponse = GameTemplate[];

interface GameTemplate {
  type: string;            // e.g. "UserId", "UserIdServer", "RiotId", "Custom"
  displayName: string;     // human-readable label, e.g. "User ID + Server"
  targetTemplate: string;  // e.g. "{userId}{server}"
  inputFields: GameInputField[];
}

interface GameInputField {
  key: string;
  label: string;
  placeholder: string;
  type: "text" | "number" | "select";
  required: boolean;
  order: number;
  options: string[];
}
```

#### BUSINESS RULES & EDGE CASES

- Read-only. Presets are defined in code (`GameTemplateFactory`) and are the single source of truth for default input fields and target templates.
- Consumed by the admin metadata editor as a preset picker (auto-fills `inputFields` + `targetTemplate`); admin can still edit manually afterwards.

---

### GET /api/v1/admin/system/game-categories

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/system/game-categories` |
| Description | Storefront category registry (display order, names, usage count) for the admin editor |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK — array order IS the storefront display order
type AdminGameCategoriesResponse = GameCategoryInfo[];

interface GameCategoryInfo {
  slug: string;       // permanent identifier, matches GameMetadata.category
  name: string;       // admin-editable display name
  brandCount: number; // brands with >=1 active product; 0 = safe to delete
}
```

#### BUSINESS RULES & EDGE CASES

- Same shape as public `GET /api/v1/games/categories`, but **includes categories with `brandCount = 0`** so a newly created custom category is assignable in the metadata editor before any game uses it
- Registry is stored as JSON in `AppSettings["GAME_CATEGORIES"]`; corrupt/missing value degrades to the built-in defaults (`mobile`, `pc`, `voucher`, `e-money`, `pulsa-data`) instead of failing
- Slugs present on games but absent from the registry are appended at the end (sorted by slug) so admin can see and adopt them

---

### PUT /api/v1/admin/system/game-categories

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/system/game-categories` |
| Description | Replace the whole registry — reorder, rename, add custom, delete |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>`, `Content-Type: application/json` |

#### REQUEST PAYLOAD

```typescript
interface UpdateGameCategoriesRequest {
  categories: GameCategoryEntryRequest[]; // array order = display order, 1..30 items
}

interface GameCategoryEntryRequest {
  slug: string | null; // existing category: send its slug unchanged. null/"" = new category, slug derived from name
  name: string;        // 1..40 chars after trim
}
```

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdateGameCategoriesResponse {
  message: string;               // "Kategori storefront berhasil disimpan!"
  categories: GameCategoryInfo[]; // saved registry, same shape as the GET above
}

// 400 Bad Request
interface ErrorResponse {
  message: string;
}
```

#### BUSINESS RULES & EDGE CASES

- **Replace-all semantics**: the payload becomes the entire registry. Last write wins (no optimistic locking).
- **Slug is permanent.** Send `slug` for existing categories so a rename never orphans `GameMetadata.Categories`. A new category derives its slug from `name` (lowercased kebab-case, e.g. `"Konsol Game"` → `"konsol-game"`).
- Validation errors → `400`:
  - empty list → `"Minimal satu kategori harus ada."`
  - more than 30 items → `"Maksimal 30 kategori."`
  - blank name → `"Nama kategori tidak boleh kosong."`
  - name longer than 40 chars → `"Nama kategori \"...\" terlalu panjang (maksimal 40 karakter)."`
  - name that yields no slug → `"Nama kategori \"...\" tidak bisa dijadikan slug."`
  - reserved slug `all` / `lainnya` / `populer` (frontend virtual buckets) → `"Slug \"...\" dipakai sistem dan tidak bisa dijadikan kategori."`
  - duplicate slug, case-insensitive → `"Slug kategori \"...\" duplikat."`
- **Delete guard**: omitting a category that is still referenced by any `GameMetadata` → `400` `"Kategori \"<slug>\" masih dipakai N game. Pindahkan game-nya dulu sebelum menghapus kategori."` The guard sees the slug in any position of a game's category list, not just the first. Move the games first (via `PUT /api/v1/admin/system/metadata/{id}`), then delete.
- Deleting a category with `brandCount = 0` is allowed only if no game references it at all — `brandCount` counts brands with an active product, the guard counts all metadata rows, so it can still refuse.

---

### GET /api/v1/admin/system/popular-games

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/system/popular-games` |
| Description | Get the current manual game curation and all active game options for the admin editor |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface PopularGamesAdminResponse {
  selected: BrandItem[];        // saved order, excluding games that are no longer active
  options: GameMetadataItem[];  // games with at least one active Games product
}
```

#### BUSINESS RULES & EDGE CASES

- `selected` uses the same `BrandItem` shape as `GET /api/v1/games/brands/popular` and preserves the saved admin order
- `options` is sorted alphabetically by brand and only includes metadata whose brand has at least one active `Games` product
- A saved game that later becomes inactive is omitted from `selected` but does not cause the endpoint to fail

---

### PUT /api/v1/admin/system/popular-games

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/system/popular-games` |
| Description | Replace the manually curated games shown in the homepage Populer section |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>`, `Content-Type: application/json` |

#### REQUEST PAYLOAD

```typescript
interface UpdatePopularGamesRequest {
  slugs: string[]; // brand slugs in display order, maximum 12
}
```

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdatePopularGamesResponse {
  message: "Daftar game populer berhasil disimpan!";
  selected: BrandItem[];
}

// 400 Bad Request
interface ErrorResponse {
  message: string;
}
```

#### BUSINESS RULES & EDGE CASES

- **Replace-all semantics**: array order becomes the display order in the homepage Populer rail
- Sending `[]` clears the curation; the homepage then hides the Populer section
- Maximum 12 slugs; duplicates are rejected case-insensitively
- Every slug must resolve to a game with metadata and at least one active `Games` product
- Missing/null `slugs` → `400` `"Daftar game populer wajib dikirim."`
- More than 12 items → `400` `"Maksimal 12 game populer."`
- Duplicate slugs → `400` `"Game populer tidak boleh duplikat."`
- Unknown or inactive game → `400` `"Ada game yang tidak ditemukan atau tidak aktif."`

---

### GET /api/v1/admin/system/metadata

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/system/metadata` |
| Description | List all game metadata configurations |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type MetadataListResponse = GameMetadataItem[];

interface GameMetadataItem {
  id: number;
  brand: string;
  brandSlug: string; // persisted URL slug (kebab-case); use as-is, never re-derive from brand
  inputFields: GameInputField[];
  targetTemplate: string;
  imageUrl: string | null;
  categories: string[]; // storefront category slugs (kebab-case); empty = uncategorized
  usernameCheckProductId: number | null;
}
```

#### BUSINESS RULES & EDGE CASES

- Ordered alphabetically by brand

---

### PUT /api/v1/admin/system/metadata/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/system/metadata/{id}` |
| Description | Update game metadata settings (input type, server list) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | GameMetadata ID |
| `inputFields` | `GameInputField[]` | Required | Form fields used for order and account check |
| `targetTemplate` | `string` | Required | Digiflazz `customer_no` template |
| `categories` | `string[]` | Optional | Storefront categories (e.g. `["mobile"]`, `["PC Game", "mobile"]`) — a game may belong to several. Each entry is normalized server-side to a kebab-case slug (lowercase, every non-alphanumeric run → hyphen, so `"E-Money"` → `"e-money"` and `"Konsol, Game"` → `"konsol-game"`); blanks and duplicates are dropped, admin order is kept. Omitted or empty array → uncategorized |
| `usernameCheckProductId` | `integer \| null` | Required | Active product from same game brand used only to validate account name; `null` disables check |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdateMetadataResponse {
  message: "Pengaturan game berhasil diperbarui!";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: Metadata not found

---

### POST /api/v1/admin/system/metadata/{id}/upload-game-cover

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/system/metadata/{id}/upload-game-cover` |
| Description | Upload/replace cover image for a game brand |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>`, `Content-Type: multipart/form-data` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | **GameMetadata** ID (NOT GameProduct ID) |
| `file` (form) | `binary` | Required | Image file, max 2 MB |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UploadGameCoverResponse {
  message: "Gambar cover game berhasil diperbarui!";
  url: string; // New R2 public URL
}
```

#### BUSINESS RULES & EDGE CASES

- Error 400: File empty or exceeds 2 MB
- Error 404: Metadata not found
- Old image deleted from R2 before upload
- Stored in R2 folder: `icon-cover`

---

### GET /api/v1/admin/settings/maintenance

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/settings/maintenance` |
| Description | Returns the current Maintenance Mode configuration |
| Auth | Bearer JWT — Admin only |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface MaintenanceStatusResponse {
  enabled: boolean;           // true = maintenance is active
  title: string;              // Displayed title on maintenance page
  message: string;            // Displayed message on maintenance page
  estimatedFinish: string | null; // ISO 8601 datetime or null if unknown
  allowAdmin: boolean;        // true = admin JWT holders bypass the block
}
```

#### BUSINESS RULES & EDGE CASES

- If no settings have been seeded yet, the server seeds defaults on first startup
- Default: `enabled = false`, `allowAdmin = true`
- Returns current values from the `AppSettings` table (via 30-second cache)

---

### PUT /api/v1/admin/settings/maintenance

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/settings/maintenance` |
| Description | Enables or disables Maintenance Mode and updates all related configuration |
| Auth | Bearer JWT — Admin only |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `enabled` | `boolean` | Required | `true` to activate maintenance, `false` to deactivate |
| `title` | `string` | Required | Short title shown to users, max 200 chars |
| `message` | `string` | Required | Full message shown to users, max 1000 chars |
| `estimatedFinish` | `string \| null` | Optional | ISO 8601 datetime string for when maintenance ends; `null` or empty if unknown |
| `allowAdmin` | `boolean` | Required | `true` = admin JWT holders can still access all endpoints |

```json
// Example request
{
  "enabled": true,
  "title": "Website Sedang Maintenance",
  "message": "Kami sedang melakukan peningkatan sistem. Silakan kembali beberapa saat lagi.",
  "estimatedFinish": "2026-06-25T13:00:00",
  "allowAdmin": true
}
```

#### RESPONSE PAYLOAD

```typescript
// 200 OK — returns the updated configuration
interface MaintenanceStatusResponse {
  enabled: boolean;
  title: string;
  message: string;
  estimatedFinish: string | null;
  allowAdmin: boolean;
}
```

#### VALIDATION

| Field | Rule | Error |
|-------|------|-------|
| `title` | Required, max 200 chars | 400: `"Title wajib diisi."` / `"Title maksimal 200 karakter."` |
| `message` | Required, max 1000 chars | 400: `"Message wajib diisi."` / `"Message maksimal 1000 karakter."` |

#### BUSINESS RULES & EDGE CASES

- All five values are persisted to the `AppSettings` table (upsert)
- Cache for each key is invalidated immediately after save; new requests see the update within 30 seconds
- When `enabled = true` and `allowAdmin = true`: authenticated admin JWT holders bypass the 503 block on all endpoints
- When `enabled = true` and `allowAdmin = false`: **even admins** are blocked (use with caution — you may lock yourself out)
- The following paths are **always whitelisted** regardless of maintenance state:
  - `/api/v1/admin/*` — admin panel
  - `/api/v1/auth/local/login` — user login
  - `/api/v1/auth/google` — Google OAuth
  - `/api/v1/auth/local/request-password-reset` — password recovery
  - `/api/v1/auth/local/reset-password` — password recovery
  - `/health`, `/swagger`, `/ping-db` — dev/ops tools

#### ERROR RESPONSES

```json
// 400 — Validation error
{ "message": "Title wajib diisi." }

// 401 — Missing or invalid JWT
// 403 — Valid JWT but not ADMIN role
```

---

## 7. Search


---

### GET /api/v1/search/games

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/search/games` |
| Description | Search game brands by keyword for frontend autocomplete |
| Auth | None (Public) |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `q` | `string` | Optional | Search keyword (case-insensitive substring match on brand name) |
| `limit` | `integer` | Optional | Max results to return, default = 10, clamped to 1–50 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type SearchGamesResponse = GameSearchResult[];

interface GameSearchResult {
  id: number;
  brand: string;
  slug: string; // auto-generated: brand.toLowerCase().replace(" ", "-")
  imageUrl: string | null;
  categorySlugs: string[]; // storefront category slugs; empty = uncategorized
}
```

#### BUSINESS RULES & EDGE CASES

- If `q` is empty, null, or whitespace → returns `[]` (empty array, NOT all games)
- If no results match → returns `[]`
- Results are prioritized: brands starting with the keyword appear first, then alphabetical within each group
  - Example: keyword `"mob"` → `"Mobile Legends"` ranks higher than `"Zombie Mobile"`
- Results are capped by `limit` (default 10, max 50)
- Uses `AsNoTracking()` + `Select()` projection for read-only performance
- No raw SQL — fully LINQ-based, database-provider agnostic

#### EXAMPLE REQUESTS

```
GET /api/v1/search/games?q=mobile
GET /api/v1/search/games?q=free
GET /api/v1/search/games?q=genshin&limit=5
GET /api/v1/search/games?q=        → returns []
```

---

## 9. Voucher Management

> Voucher is a **first-class feature** with its own admin CRUD, validation pipeline, and usage tracking.  
> Vouchers are applied during order creation (`POST /api/v1/orders/`) via the `voucherCode` field — there is no separate "apply voucher" endpoint.  
> See **Section 3 — POST /api/v1/orders/** for the customer-facing voucher validation chain.

---

### Enums

#### DiscountType

| Value | Meaning |
|-------|--------|
| `Percentage` | Discount is a percentage of the product price (e.g. 10 = 10%) |
| `FixedAmount` | Discount is a fixed nominal amount in IDR (e.g. 5000 = Rp5.000 off) |

#### VoucherStatus _(computed — not stored)_

| Value | Meaning | Computed Condition |
|-------|---------|-------------------|
| `Active` | Voucher is usable | `isActive == true` AND `expiredAt > now` AND `usedCount < usageLimit` |
| `Disabled` | Admin manually disabled | `isActive == false` |
| `Expired` | Past expiration date | `expiredAt <= now` |
| `QuotaExceeded` | All uses consumed | `usedCount >= usageLimit` |

> When multiple conditions match, priority order: **Disabled** → **Expired** → **QuotaExceeded** → **Active**.

---

### GET /api/v1/admin/vouchers

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/vouchers` |
| Description | List all vouchers with pagination, search, filter, and sorting |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `search` | `string` | Optional | Case-insensitive substring match on `code` |
| `status` | `string` | Optional | Filter by computed status: `Active`, `Disabled`, `Expired`, `QuotaExceeded` |
| `sortBy` | `string` | Optional | Sort field: `code`, `createdAt`, `expiredAt`, `discountValue`. Default = `createdAt` |
| `sortOrder` | `string` | Optional | `asc` or `desc`. Default = `desc` |
| `page` | `integer` | Optional | Page number, default = 1, minimum = 1 |
| `limit` | `integer` | Optional | Items per page, default = 20, maximum = 100 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminVoucherListResponse {
  items: AdminVoucherItem[];
  totalCount: number;
  currentPage: number;
  totalPages: number;
}

interface AdminVoucherItem {
  id: number;
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number; // decimal — percentage (10 = 10%) or fixed IDR (5000 = Rp5.000)
  minimumTransaction: number; // decimal
  maximumDiscount: number | null; // decimal — null = no cap
  usageLimit: number;
  usedCount: number;
  expiredAt: string; // ISO datetime
  isActive: boolean;
  status: "Active" | "Disabled" | "Expired" | "QuotaExceeded"; // computed
  createdAt: string; // ISO datetime
  updatedAt: string; // ISO datetime
}
```

#### BUSINESS RULES & EDGE CASES

- `status` is **computed server-side** from `isActive`, `expiredAt`, and `usedCount` vs `usageLimit`
- Search matches `code` case-insensitively (substring)
- If `page` exceeds total pages, returns empty `items` array
- Default sort: `createdAt` descending (newest first)

#### EXAMPLE REQUEST

```
GET /api/v1/admin/vouchers?search=HEMAT&status=Active&sortBy=createdAt&sortOrder=desc&page=1&limit=20
```

#### EXAMPLE RESPONSE

```json
{
  "items": [
    {
      "id": 1,
      "code": "HEMAT20",
      "discountType": "Percentage",
      "discountValue": 20,
      "minimumTransaction": 50000,
      "maximumDiscount": 10000,
      "usageLimit": 100,
      "usedCount": 15,
      "expiredAt": "2026-12-31T23:59:59.000Z",
      "isActive": true,
      "status": "Active",
      "createdAt": "2026-06-20T10:00:00.000Z",
      "updatedAt": "2026-06-20T10:00:00.000Z"
    }
  ],
  "totalCount": 1,
  "currentPage": 1,
  "totalPages": 1
}
```

---

### GET /api/v1/admin/vouchers/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/vouchers/{id}` |
| Description | Get full detail of a single voucher |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | Voucher ID |

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminVoucherDetailResponse {
  id: number;
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  usedCount: number;
  expiredAt: string;
  isActive: boolean;
  status: "Active" | "Disabled" | "Expired" | "QuotaExceeded";
  createdAt: string;
  updatedAt: string;
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: `"Voucher tidak ditemukan."`

#### EXAMPLE REQUEST

```
GET /api/v1/admin/vouchers/1
```

#### EXAMPLE RESPONSE

```json
{
  "id": 1,
  "code": "HEMAT20",
  "discountType": "Percentage",
  "discountValue": 20,
  "minimumTransaction": 50000,
  "maximumDiscount": 10000,
  "usageLimit": 100,
  "usedCount": 15,
  "expiredAt": "2026-12-31T23:59:59.000Z",
  "isActive": true,
  "status": "Active",
  "createdAt": "2026-06-20T10:00:00.000Z",
  "updatedAt": "2026-06-20T10:00:00.000Z"
}
```

---

### POST /api/v1/admin/vouchers

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/vouchers` |
| Description | Create a new voucher |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

```typescript
interface CreateVoucherPayload {
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  expiredAt: string; // ISO datetime
  isActive: boolean;
}
```

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `code` | `string` | Required | Unique, alphanumeric recommended, 3–50 characters |
| `discountType` | `string` | Required | `"Percentage"` or `"FixedAmount"` |
| `discountValue` | `decimal` | Required | Must be > 0. For Percentage: max 100. For FixedAmount: amount in IDR |
| `minimumTransaction` | `decimal` | Required | Minimum order price to use this voucher. Must be ≥ 0 |
| `maximumDiscount` | `decimal \| null` | Optional | Cap on discount amount. Only meaningful for `Percentage` type. `null` = no cap |
| `usageLimit` | `integer` | Required | Maximum total uses. Must be ≥ 1 |
| `expiredAt` | `string` | Required | ISO datetime. Must be in the future |
| `isActive` | `boolean` | Required | Whether the voucher is active on creation |

#### RESPONSE PAYLOAD

```typescript
// 201 Created
interface AdminVoucherDetailResponse {
  id: number;
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  usedCount: number; // 0 on creation
  expiredAt: string;
  isActive: boolean;
  status: "Active" | "Disabled" | "Expired" | "QuotaExceeded";
  createdAt: string;
  updatedAt: string;
}
```

#### BUSINESS RULES & EDGE CASES

- Error 400: `"Kode voucher sudah digunakan."` — duplicate code (case-insensitive)
- Error 400: `"Kode voucher harus antara 3 dan 50 karakter."` — invalid code length
- Error 400: `"Nilai diskon harus lebih besar dari 0."` — discountValue ≤ 0
- Error 400: `"Diskon persentase tidak boleh lebih dari 100%."` — percentage > 100
- Error 400: `"Tanggal kedaluwarsa harus di masa depan."` — expiredAt in the past
- Error 400: `"Batas penggunaan harus minimal 1."` — usageLimit < 1
- Error 400: `"Tipe diskon tidak valid. Pilih: Percentage atau FixedAmount."` — invalid discountType
- `usedCount` is initialized to `0`
- Returns HTTP 201 (not 200)

#### EXAMPLE REQUEST

```json
{
  "code": "NEWUSER50",
  "discountType": "Percentage",
  "discountValue": 50,
  "minimumTransaction": 25000,
  "maximumDiscount": 15000,
  "usageLimit": 200,
  "expiredAt": "2026-12-31T23:59:59.000Z",
  "isActive": true
}
```

#### EXAMPLE RESPONSE

```json
{
  "id": 3,
  "code": "NEWUSER50",
  "discountType": "Percentage",
  "discountValue": 50,
  "minimumTransaction": 25000,
  "maximumDiscount": 15000,
  "usageLimit": 200,
  "usedCount": 0,
  "expiredAt": "2026-12-31T23:59:59.000Z",
  "isActive": true,
  "status": "Active",
  "createdAt": "2026-06-21T08:30:00.000Z",
  "updatedAt": "2026-06-21T08:30:00.000Z"
}
```

---

### PUT /api/v1/admin/vouchers/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/api/v1/admin/vouchers/{id}` |
| Description | Update all voucher fields (full replacement) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

```typescript
interface UpdateVoucherPayload {
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  expiredAt: string; // ISO datetime
  isActive: boolean;
}
```

> All fields are **required** — same structure as CreateVoucherPayload.

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminVoucherDetailResponse {
  id: number;
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  usedCount: number;
  expiredAt: string;
  isActive: boolean;
  status: "Active" | "Disabled" | "Expired" | "QuotaExceeded";
  createdAt: string;
  updatedAt: string; // updated
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: `"Voucher tidak ditemukan."`
- Error 400: `"Kode voucher sudah digunakan."` — code already taken by another voucher
- Error 400: Same validation rules as Create
- `usedCount` **cannot** be modified via this endpoint (it is read-only, incremented by the system)
- `usageLimit` cannot be set lower than current `usedCount` → Error 400: `"Batas penggunaan tidak boleh kurang dari jumlah yang sudah digunakan ({usedCount})."` 

#### EXAMPLE REQUEST

```json
{
  "code": "HEMAT20",
  "discountType": "Percentage",
  "discountValue": 25,
  "minimumTransaction": 50000,
  "maximumDiscount": 12000,
  "usageLimit": 150,
  "expiredAt": "2027-01-31T23:59:59.000Z",
  "isActive": true
}
```

#### EXAMPLE RESPONSE

```json
{
  "id": 1,
  "code": "HEMAT20",
  "discountType": "Percentage",
  "discountValue": 25,
  "minimumTransaction": 50000,
  "maximumDiscount": 12000,
  "usageLimit": 150,
  "usedCount": 15,
  "expiredAt": "2027-01-31T23:59:59.000Z",
  "isActive": true,
  "status": "Active",
  "createdAt": "2026-06-20T10:00:00.000Z",
  "updatedAt": "2026-06-21T09:15:00.000Z"
}
```

---

### PATCH /api/v1/admin/vouchers/{id}/status

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PATCH` |
| Path | `/api/v1/admin/vouchers/{id}/status` |
| Description | Enable or disable a voucher without changing other data |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

```typescript
interface UpdateVoucherStatusPayload {
  isActive: boolean; // true = enable, false = disable
}
```

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `isActive` | `boolean` | Required | `true` to enable, `false` to disable |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface UpdateVoucherStatusResponse {
  message: "Status voucher berhasil diperbarui.";
  isActive: boolean;
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: `"Voucher tidak ditemukan."`
- Only changes `isActive` — all other fields remain unchanged
- Disabling a voucher immediately prevents its use at checkout (no effect on existing orders)

#### EXAMPLE REQUEST

```json
{ "isActive": false }
```

#### EXAMPLE RESPONSE

```json
{ "message": "Status voucher berhasil diperbarui.", "isActive": false }
```

---

### DELETE /api/v1/admin/vouchers/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/api/v1/admin/vouchers/{id}` |
| Description | Permanently delete a voucher |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | Voucher ID |

_No request body._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface DeleteVoucherResponse {
  message: "Voucher berhasil dihapus.";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: `"Voucher tidak ditemukan."`
- Error 409: `"Voucher tidak dapat dihapus karena sudah pernah digunakan ({usedCount} transaksi). Nonaktifkan voucher sebagai gantinya."` — if `usedCount > 0`
- **Soft approach**: If a voucher has been used, deletion is **blocked**. Admin must disable it instead via `PATCH /api/v1/admin/vouchers/{id}/status`
- Deletion is **permanent** and irreversible for vouchers with zero usage
- Existing orders that referenced the voucher code retain their `voucherCode` and `discountAmount` — deletion does NOT affect completed orders

#### EXAMPLE REQUEST

```
DELETE /api/v1/admin/vouchers/5
```

#### EXAMPLE RESPONSE

```json
{ "message": "Voucher berhasil dihapus." }
```

---

### GET /api/v1/admin/vouchers/{id}/usage

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/vouchers/{id}/usage` |
| Description | Get paginated list of orders that used this voucher |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `page` | `integer` | Optional | Default = 1 |
| `limit` | `integer` | Optional | Default = 20, maximum = 100 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface VoucherUsageResponse {
  items: VoucherUsageInfo[];
  totalCount: number;
  currentPage: number;
  totalPages: number;
}

interface VoucherUsageInfo {
  orderId: number;
  orderNo: string;
  customerName: string | null; // null for guest checkout
  productName: string;
  price: number; // original price
  discountAmount: number;
  finalPrice: number;
  orderDate: string; // ISO datetime
  orderStatus: string; // OrderStatus value
}
```

#### BUSINESS RULES & EDGE CASES

- Error 404: `"Voucher tidak ditemukan."`
- Ordered by `orderDate` descending (newest first)
- Includes orders regardless of their current status (PENDING, COMPLETED, etc.)

#### EXAMPLE REQUEST

```
GET /api/v1/admin/vouchers/1/usage?page=1&limit=10
```

#### EXAMPLE RESPONSE

```json
{
  "items": [
    {
      "orderId": 42,
      "orderNo": "INV-20260620-XY12",
      "customerName": "Budi Santoso",
      "productName": "Mobile Legends 86 Diamonds",
      "price": 25000,
      "discountAmount": 5000,
      "finalPrice": 20000,
      "orderDate": "2026-06-20T14:30:00.000Z",
      "orderStatus": "COMPLETED"
    }
  ],
  "totalCount": 1,
  "currentPage": 1,
  "totalPages": 1
}
```

---

## 10. FAQ (CMS)

> FAQ dikelola admin lewat `/admin/faqs`; halaman publik (FAQ page & homepage) hanya membaca endpoint public di bawah. Semua respons memakai DTO `FaqResponse` — entity EF tidak pernah terekspos.

### GET /api/v1/faqs

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/faqs` |
| Description | Daftar FAQ publik dengan filter kategori + search (cocok ke question ATAU answer, case-insensitive) |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Query Param | Type | Required/Optional | Description & Constraints |
|-------------|------|-------------------|---------------------------|
| `category` | `string` | Optional | Filter kategori eksak (mis. `Top Up`) |
| `search` | `string` | Optional | Pencocokan substring pada question atau answer |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — array FaqResponse, urut Id ascending
interface FaqResponse {
  id: number;
  question: string;
  answer: string;
  category: string;
  featured: boolean;
}
```

#### BUSINESS RULES & EDGE CASES

- Tanpa param → semua FAQ.
- `search` di-trim; string kosong/whitespace diabaikan.
- Kategori valid apapun diterima — kategori yang tidak ada menghasilkan array kosong (bukan error).

---

### GET /api/v1/faqs/featured

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/faqs/featured` |
| Description | FAQ unggulan untuk homepage — **maksimal 6, ditegakkan backend** |
| Auth | None (Public) |

**Response**: sama dengan `GET /faqs` (array `FaqResponse`).

**Business rules:**
- Filter `featured = true`, urut `Id` ascending, `Take(6)` — jika sumber data menandai >6 unggulan, yang dikembalikan tetap deterministik: 6 FAQ unggulan dengan Id terkecil.
- Membuat/mengubah FAQ menjadi unggulan ke-7 ditolak `400` (lihat endpoint admin) — jalur ini hanya fallback proteksi tampilan.

---

### GET /api/v1/faqs/categories

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/faqs/categories` |
| Description | Daftar kategori FAQ distinct (urut alfabetis) — frontend TIDAK boleh hardcode kategori |
| Auth | None (Public) |

**Response**: `string[]` (mis. `["Akun","Lainnya","Pembayaran","Top Up","Transaksi","Voucher"]`).

---

### GET /api/v1/admin/faqs

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/faqs` |
| Description | Daftar seluruh FAQ untuk manajemen admin (search/category filter, tanpa pagination) |
| Auth | JWT Required (ADMIN, SUPER_ADMIN) |

Query param: `search`, `category` (opsional, perilaku sama dengan `GET /faqs`). Response: array `FaqResponse` urut `Id`.

---

### GET /api/v1/admin/faqs/{id}

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/faqs/{id}` |
| Description | Detail satu FAQ |
| Auth | JWT Required (ADMIN, SUPER_ADMIN) |

**Response**: `FaqResponse`. **Error 404**: `{ message: "FAQ tidak ditemukan." }`.

---

### POST /api/v1/admin/faqs

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/admin/faqs` |
| Description | Buat FAQ baru |
| Auth | JWT Required (ADMIN, SUPER_ADMIN) |

#### REQUEST PAYLOAD

```typescript
interface CreateFaqRequest {
  question: string;  // wajib, non-kosong setelah trim
  answer: string;    // wajib, non-kosong setelah trim
  category: string;  // wajib, non-kosong setelah trim (string bebas — tidak divalidasi ke daftar tetap)
  featured: boolean; // wajib
}
```

**Response**: `201 Created` + `FaqResponse` (Location: `/api/v1/admin/faqs/{id}`). `id` tidak pernah diterima dari klien — selalu dibuat server.

**Errors (400 `{ message }`):**
- `"Pertanyaan tidak boleh kosong."` / `"Jawaban tidak boleh kosong."` / `"Kategori tidak boleh kosong."`
- `"Maksimal 6 FAQ unggulan. Matikan salah satu terlebih dahulu."` — membuat FAQ unggulan ke-7 (ada 6 unggulan existing). Tidak ada record lain yang diam-diam di-un-featured.

---

### PUT /api/v1/admin/faqs/{id}

Sama seperti POST (payload `UpdateFaqRequest` = shape `CreateFaqRequest`). **Response**: `200` + `FaqResponse` ter-update. **404** bila id tidak ada.

**Aturan unggulan saat update**: batas maks 6 hanya dihitung bila update MENYALAKAN `featured` dari record yang sebelumnya tidak unggulan; FAQ yang sudah unggulan dan tetap unggulan tidak dihitung ulang.

---

### DELETE /api/v1/admin/faqs/{id}

Hard delete (tidak ada soft-delete). **Response**: `200 { message: "FAQ berhasil dihapus." }`. **Error 404**: `{ message: "FAQ tidak ditemukan." }`. Menghapus FAQ unggulan membuka slot unggulan.

---

## 11. Leaderboard Pembeli (Public)

> Ranking pembeli berdasarkan total belanja aktual — derived data murni dari order; tidak ada CRUD/tabel sendiri, admin tidak pernah mengedit rank/totalSpent.

### GET /api/v1/leaderboard

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/leaderboard` |
| Description | Top 10 pembeli per periode, ranking = Sum(TotalPaid) order lunak milik customer teridentifikasi |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Query Param | Type | Required/Optional | Description & Constraints |
|-------------|------|-------------------|---------------------------|
| `period` | `string` | Optional (default `today`) | `today` \| `week` \| `month` (case-insensitive, di-trim; kosong/null = `today`) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — maks 10 entri, urut totalSpent descending
interface LeaderboardEntry {
  rank: number;        // 1..10, dihitung dari urutan hasil (tidak disimpan)
  displayName: string; // Customer.FullName; nama bentuk email disamarkan "bud***"
  avatarUrl: string | null; // Customer.AvatarUrl (OAuth); null = ikon default
  totalSpent: number;  // Sum(TotalPaid), decimal
}
```

#### BUSINESS RULES & EDGE CASES

- **Ranking**: `Sum(TotalPaid)` per customer, descending. Tie-break `CustomerId` ascending (deterministik). Rank 1..10 dihitung dari urutan hasil — tidak ada kolom rank di DB.
- **Order eligible** (uang masuk, dari state machine OrderStateTransition): `PAID`, `PROCESSING`, `COMPLETED`, `NEEDS_ACTION`. Dikecualikan: `PENDING` (belum bayar), `EXPIRED`, `FAILED`, `REFUNDED`.
- **Guest order** (`CustomerId` null) dikecualikan — tamu tidak dikelompokkan.
- **Periode** (kalender WIB / Asia/Jakarta, interval setengah-terbuka `start <= OrderDate < now`):
  - `today` — 00:00 WIB hari ini → sekarang
  - `week` — Senin 00:00 WIB minggu ini → sekarang
  - `month` — tanggal 1 00:00 WIB bulan ini → sekarang
- **Maks 10 entri** — ditegakkan di query (SQL `LIMIT 10`).
- **Privasi**: hanya 4 field publik di atas. Tidak ada email/telepon/password/OAuth ID/order number/CustomerId. `displayName` yang berbentuk email (fallback auto-register OAuth) disamarkan: maks 3 karakter pertama local-part + `***`.
- Agregasi seluruhnya terjadi di database (EF Core → `WHERE IN → JOIN → GROUP BY → SUM → ORDER BY → LIMIT`).
- Error 400: `?period=yearly` → `{ message: "Periode tidak valid. Gunakan today, week, atau month." }`
- Array kosong bila belum ada pembelian eligible pada periode tsb.

---

## 12. Cek Transaksi (Public Invoice Lookup)

> Halaman "Cek Transaksi" — pengguna memasukkan nomor invoice TANPA login, backend meresolusikannya ke PublicHash, lalu status dilihat di halaman status publik. Boundary akses order: `POST /invoices/resolve` (publik, OrderNo → PublicHash saja) + `GET /invoices/{publicHash}` (publik, view model aman — PublicHash satu-satunya identifier publik) vs `GET /orders/my-history` (privat, `[Authorize]`, khusus pemilik) vs `/admin/orders` (policy AdminOnly).

### POST /api/v1/invoices/resolve

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/api/v1/invoices/resolve` |
| Description | Cek Transaksi: resolusi nomor invoice (`INV-yyyyMMdd-XXXXXXXXXX`) → `PublicHash` untuk URL status. Tidak memerlukan login. Body (bukan query) supaya nomor invoice tidak tercatat di access log. |
| Auth | None (Public) |
| Rate limit | `InvoiceLookupPolicy` (100 / 5 menit / IP) — endpoint ini jalur enumerasi OrderNo, limit per-IP wajib melekat |

#### REQUEST PAYLOAD

| Body | Type | Required/Optional | Description & Constraints |
|------|------|-------------------|---------------------------|
| `orderNo` | `string` | Required | Nomor invoice lengkap; di-trim; kosong/whitespace → 400 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — MINIMAL: hanya PublicHash, tanpa data transaksi apa pun
interface InvoiceResolveResponse {
  publicHash: string; // HMAC-SHA256(SecretKey, orderNo) — untuk /order/status/{publicHash}
}
```

#### BUSINESS RULES & EDGE CASES

- Baris pra-backfill (PublicHash NULL) → tidak bisa diresolve (404) sampai startup backfill mengisi kolomnya (idempotent, batched).
- Error 400: `orderNo` kosong → `{ message: "Masukkan nomor invoice." }`
- Error 404: order tidak ditemukan → `{ message: "Invoice tidak ditemukan." }` (pesan sama dengan GET — tidak membocorkan keberadaan order)
- Error 429: rate limit (pesan standar)
- FE: setelah resolve sukses, redirect ke `/order/status/{publicHash}`.

### GET /api/v1/invoices/{publicHash}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/invoices/{publicHash}` |
| Description | Lookup satu transaksi untuk tampilan publik. **`{publicHash}` adalah identifier publik SATU-SATUNYA yang diterima** — 64 hex (case-insensitive, dinormalisasi huruf kecil). OrderNo (`INV-...`), URL lama `/order/status/{OrderNo}`, dan format lain → 404 seragam. Tidak memerlukan login. |
| Auth | None (Public) — **optional-auth**: JWT customer yang valid (bila ada) mengisi flag `belongsToAccount` |
| Rate limit | `InvoiceLookupPolicy` (100 / 5 menit / IP) — cukup untuk polling halaman status (~12 req/menit) |

#### REQUEST PAYLOAD

| Path Param | Type | Required/Optional | Description & Constraints |
|------------|------|-------------------|---------------------------|
| `publicHash` | `string` | Required | 64 karakter hex lowercase (case-insensitive); di-trim; kosong/whitespace → 400; format lain → 404 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface PublicInvoiceResponse {
  invoiceNumber: string;      // "INV-20260906-67310E54B0"
  productName: string;        // snapshot nama produk
  imageUrl: string | null;    // snapshot gambar produk
  brandSlug: string | null;   // snapshot slug katalog brand (URL /game/{slug}) — identifier PUBLIK, dipakai FE untuk cookie "Terakhir Dibeli" guest; null untuk order pra-kolom
  amount: number;             // decimal — TotalPaid (final setelah diskon)
  status: string;             // status order: PENDING|PAID|PROCESSING|COMPLETED|NEEDS_ACTION|FAILED|EXPIRED|REFUNDED
  paymentStatus: string;      // ringkasan pembayaran: UNPAID|PAID|FAILED|REFUNDED
  createdAt: string;          // ISO datetime UTC
  expiresAt: string | null;   // batas bayar Midtrans — HANYA saat status PENDING
  belongsToAccount: boolean;  // true bila JWT valid dan order milik customer tsb
  paymentType: string | null; // payment_type Core API ("qris"|"gopay"|"bank_transfer") — HANYA saat PENDING; null selainnya
  paymentInstructions: OrderPaymentInstructions | null; // instruksi bayar Core API — HANYA saat PENDING (bentuk sama seperti respons POST /orders)
}
```

#### BUSINESS RULES & EDGE CASES

- **paymentStatus mapping**: PENDING → UNPAID; PAID/PROCESSING/COMPLETED/NEEDS_ACTION → PAID; EXPIRED/FAILED → FAILED; REFUNDED → REFUNDED.
- **expiresAt**: `OrderDate + MidtransExpiryMinutes` (AppSettings — SSOT yang sama dengan charge & sweep); null untuk status non-PENDING.
- **paymentType / paymentInstructions**: hanya untuk PENDING dan hanya untuk order Core API (order pra-migrasi tanpa kolom Core API mendapat null). Level kepercayaan sama dengan `POST /orders` — instruksi bayar terikat order+nominal spesifik (QR string / nomor VA / deeplink), bukan kredensial akun. Null untuk semua status lain.
- **Identitas publik (SSOT bisnis = OrderNo)**: OrderNo (`INV-{yyyyMMdd}-{10 hex CSPRNG}`) tetap identifier bisnis — order_id Midtrans, ref_id Digiflazz, webhook, admin retry/refund, my-history. Tidak pernah valid sebagai URL/identifier status publik.
- **PublicHash** = `HMAC-SHA256(OrderPublicHash:SecretKey, OrderNo)` — hex kecil 64 char, disimpan di kolom `TopupOrders.PublicHash` (unique index, nullable untuk baris pra-backfill yang diisi idempotent saat startup). **Identifier publik SATU-SATUNYA** yang diterima endpoint status. Keyed HMAC (bukan SHA256 polos) — hash tidak bisa direkonstruksi tanpa secret; OrderNo hanya bisa dipetakan ke PublicHash lewat endpoint resolve (publik, rate-limited). Secret: environment variable `OrderPublicHash__SecretKey` (≥ 32 byte acak), fail-fast saat startup bila kosong; tidak pernah di-log.
- **Order baru**: PublicHash dihitung atomik bersama OrderNo saat `POST /orders` — respons selalu memuat `publicHash`; FE navigasi ke `/order/status/{publicHash}`.
- URL lama `/order/status/{OrderNo}` (bookmark) → 404 seragam. Pengguna dengan nomor invoice tetap bisa masuk lewat Cek Transaksi (`POST /invoices/resolve`).
- **Privasi**: hanya field di atas — tidak ada PlayerId, Sn (serial number produk), CustomerId, atau field admin. Entity EF tidak pernah diserialisasi langsung. (paymentInstructions PENDING = instruksi bayar, lihat aturan di atas.)
- `belongsToAccount`: guest (tanpa JWT) → selalu false; pemilik → true; customer lain → false. Flag ini tidak membatasi akses dan tidak membocorkan identitas pemilik.
- Guest order (`CustomerId` null) tetap bisa dicek publik — alur guest checkout tidak berubah.
- Error 400: `publicHash` kosong → `{ message: "Masukkan nomor invoice." }`
- Error 404: hash tidak ditemukan / format salah (termasuk OrderNo) → `{ message: "Invoice tidak ditemukan." }` (pesan sama untuk tidak-ada/format-salah — tidak membocorkan keberadaan order).
- Error 429: rate limit (pesan standar OrderPolicy).

---

## 13. Metode Pembayaran (Checkout Config)

> SSOT tampilan checkout: ikon, nama tampilan, urutan, dan status aktif metode/bank VA. Kemampuan provider (metode yang bisa diproses Core API) tetap di-whitelist backend — menonaktifkan lewat endpoint admin hanya menyembunyikan dari checkout + menolak order dengan metode itu.

### GET /api/v1/payment-methods

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/payment-methods` |
| Description | Metode pembayaran aktif untuk checkout (kartu metode + bank VA), urut `sortOrder` |
| Auth | None (Public) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type PaymentMethodsResponse = PaymentMethodResponse[];

interface PaymentMethodResponse {
  code: string;          // "qris" | "gopay" | "bank_transfer" (payment_type Core API)
  name: string;          // display name
  description: string | null;
  iconUrl: string | null;  // URL publik ikon (Cloudflare R2); null = FE render monogram
  banks: PaymentBankResponse[]; // hanya terisi relevan untuk bank_transfer; hanya bank aktif, urut sortOrder
}

interface PaymentBankResponse {
  code: string;          // "bca" | "bni" | "bri" | "cimb" | "permata" (kode bank VA Midtrans)
  name: string;
  iconUrl: string | null;
}
```

#### BUSINESS RULES & EDGE CASES

- Hanya metode/bank `enabled` yang dikembalikan; urutan mengikuti `sortOrder` (bukan logika FE).
- Respons publik TIDAK memuat `enabled`/`sortOrder` (field admin).
- Seed: QRIS, GoPay, ShopeePay, Transfer Bank, Toko Retail/cstore (aktif) + bank BCA/BNI/BRI/CIMB Niaga/Permata/Mandiri (aktif — Mandiri via kanal echannel; BSI nonaktif: VA asli via bank_transfer, hanya payable via app BYOND by BSI) + toko Indomaret/Alfamart (aktif, sub-kanal cstore), serta DANA/LinkAja/OVO (nonaktif — bukan kanal Midtrans; baris katalog untuk ekspansi provider, ordernya ditolak validasi awal).
- **expiresAt cstore**: order cstore berlaku 24 jam (Midtrans mengabaikan expiry custom di kanal ini — `OrderExpiryPolicy`); metode lain mengikuti konfigurasi expiry.
- Order dengan metode/bank yang dinonaktifkan tetap ditolak backend (`POST /orders`): `"Metode pembayaran sedang tidak tersedia."` / `"Bank VA sedang tidak tersedia."`.

### GET /api/v1/admin/payment-methods

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/api/v1/admin/payment-methods` |
| Description | Semua metode + bank (termasuk nonaktif) untuk admin |
| Auth | Admin JWT Required |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type AdminPaymentMethodsResponse = AdminPaymentMethodResponse[];

interface AdminPaymentMethodResponse {
  code: string;
  name: string;
  description: string | null;
  iconUrl: string | null;
  enabled: boolean;
  sortOrder: number;
  banks: AdminPaymentBankResponse[];
}

interface AdminPaymentBankResponse {
  code: string;
  name: string;
  iconUrl: string | null;
  enabled: boolean;
  sortOrder: number;
}
```

### PUT /api/v1/admin/payment-methods/{code}

Update parsial konfigurasi metode. Body (semua opsional, null = tidak diubah): `{ "displayName"?: string, "iconUrl"?: string | null, "enabled"?: boolean, "sortOrder"?: number }`. `code` tidak dapat diubah (identitas).

#### RESPONSE PAYLOAD

- 200: `AdminPaymentMethodResponse` (ter-update)
- 404: `{ message: "Metode pembayaran '{code}' tidak ditemukan." }`
- 400: `"Nama metode pembayaran tidak boleh kosong."` / `"URL ikon tidak valid — wajib URL http(s) publik (mis. hasil upload Cloudflare R2)."` / `"Urutan tidak boleh negatif."`

### PUT /api/v1/admin/payment-methods/{code}/banks/{bankCode}

Update parsial bank di bawah metode. Body sama dengan method (nama "Nama bank tidak boleh kosong.").

#### RESPONSE PAYLOAD

- 200: `AdminPaymentBankResponse`
- 404: `{ message: "Bank '{bankCode}' tidak ditemukan di bawah metode '{code}'." }`
- 400: aturan validasi yang sama

### POST /api/v1/admin/payment-methods/{code}/upload-icon

Upload ikon metode — multipart/form-data `file` (JPG/JPEG/PNG/WebP/SVG, maks 2 MB, magic-byte divalidasi; SVG tambah disanitasi: script/event-handler/foreignObject ditolak). File disimpan di Cloudflare R2 folder `payment-icons`; ikon lama otomatis dihapus dari R2; URL publik disimpan ke `iconUrl`.

### POST /api/v1/admin/payment-methods/{code}/banks/{bankCode}/upload-icon

Upload ikon bank — semantik sama dengan upload ikon metode.

#### RESPONSE PAYLOAD (kedua upload)

```typescript
// 200 OK
interface UploadImageResponse {
  message: string; // "Ikon metode pembayaran berhasil diperbarui!" | "Ikon bank berhasil diperbarui!"
  imageUrl: string; // URL publik R2
}
// 400: file kosong / > 2 MB / format tidak valid (signature byte)
// 404: code / bankCode tidak dikenal
```

---

## 8. Webhooks (Backend-to-Backend — NOT for frontend consumption)

> These endpoints are called by external services (Midtrans, Digiflazz). Documented here for reference only.

---

### POST /webhooks/midtrans

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/webhooks/midtrans` |
| Description | Receive payment status notifications from Midtrans |
| Auth | Signature-validated (not JWT) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `order_id` | `string` | Required | Maps to `orderNo` |
| `status_code` | `string` | Required | HTTP-like status code |
| `gross_amount` | `string` | Required | Amount as string |
| `transaction_status` | `string` | Required | `"settlement"` \| `"capture"` \| `"expire"` \| `"cancel"` \| `"deny"` \| `"pending"` (others ignored) |
| `fraud_status` | `string \| null` | Optional | Fraud check result. **Not always sent by Midtrans** (cstore settlement/pending notifications verified without it — sandbox E2E 2026-09-08). Missing ≠ rejected: the fraud decision falls to the settlement re-check (status API always carries `fraud_status`). If present and != `"accept"`, PAID is skipped |
| `signature_key` | `string` | Required | `SHA512(order_id + status_code + gross_amount + ServerKey).ToLower()` |
| `payment_type` | `string \| null` | Optional | Actual payment method (e.g. `"qris"`, `"gopay"`, `"bank_transfer"`) — recorded on the order if not already set (backfills pre-migration orders) |
| `transaction_id` | `string \| null` | Optional | Midtrans transaction id — recorded on the order if not already set (charge-time value wins) |
| `transaction_time` | `string \| null` | Optional | Accepted, not persisted |
| `settlement_time` | `string \| null` | Optional | Accepted, not persisted |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface MidtransWebhookResponse {
  message: "Notification received";
}

// 401 Unauthorized — invalid signature_key (request rejected, Midtrans should not retry)
// 500 — internal processing error (Midtrans may retry)
```

#### BUSINESS RULES & EDGE CASES

- `signature_key` is validated before any DB access; an invalid signature returns **401** (previously 500)
- Unknown `order_id` → 200 with no state change
- Duplicate/late notifications for non-PENDING orders → 200, ignored
- Amount guard: `gross_amount` must equal the integer part of the order's `totalPaid`, otherwise the notification is logged and ignored (status unchanged)
- **Status routing**: `expire` → EXPIRED; `cancel`/`deny` → FAILED; `settlement`/`capture` + `fraud_status == "accept"` (or `fraud_status` absent — see field note) → PAID (then fulfillment); `pending` and any other status → no-op. All non-settlement terminal transitions release reserved voucher quota
- **Enrichment**: on the first notification that transitions a PENDING order, `payment_type`/`transaction_id` are recorded on the order if not already set by the charge (Core API orders keep their charge-time values)
- **Settlement re-check (defense-in-depth)**: before committing PAID, the backend re-queries Midtrans `GET /v2/{order_id}/status`. If the status API is unreadable (error/404), the signed notification remains authoritative and the payment proceeds (**fail-open**). If the status API definitively contradicts the notification (terminal negative status `expire`/`cancel`/`deny`, `fraud_status == "deny"`, or a different `gross_amount`), the PAID transition is skipped and the contradiction is logged as critical — the order stays PENDING for reconciliation. A `pending` snapshot does not block (the notification event can be newer than the snapshot)
- PAID is committed in a short DB transaction (concurrency-token guarded); fulfillment (Digiflazz) runs outside it — fulfillment failure never rolls back PAID (→ NEEDS_ACTION)

---

### POST /webhooks/digiflazz

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/webhooks/digiflazz` |
| Description | Receive order fulfillment status from Digiflazz |
| Auth | HMAC signature in header |
| Headers | `x-hub-signature: sha1={hex_signature}` |

#### REQUEST PAYLOAD

```typescript
interface DigiflazzWebhookBody {
  data: {
    ref_id: string; // Maps to orderNo
    status: "Sukses" | "Gagal" | "Pending";
    sn: string; // Serial number (if successful)
    message: string;
  };
}
```

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface DigiflazzWebhookResponse {
  message: "Webhook Digiflazz received";
}
```

#### BUSINESS RULES & EDGE CASES

- Error 401: Invalid `x-hub-signature`

---

## Ops Endpoints

### GET /health

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/health` |
| Description | Liveness probe for containers / uptime monitors |
| Auth | None (Public) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — always
interface HealthResponse {
  status: "Healthy";
}
```

#### BUSINESS RULES & EDGE CASES

- Whitelisted from Maintenance Mode (never returns 503)
- Liveness only — no dependency checks (DB/Redis down must not kill the container probe; use `/ping-db` for DB connectivity)

### GET /ping-db

Dev-only DB connectivity check. Returns 200 with a success string when EF Core can connect, 500 `ProblemDetails` otherwise. Not part of the stable API surface.

---

## Voucher Model Reference

> Complete model reference for the Voucher feature. These types are used across Section 9 (Admin) and Section 3 (Orders).

### Core Models

```typescript
// Public-facing voucher info (shown to customers at checkout)
interface VoucherItem {
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
}

// Admin list item (includes tracking & status fields)
interface AdminVoucherItem {
  id: number;
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  usedCount: number;
  expiredAt: string;
  isActive: boolean;
  status: "Active" | "Disabled" | "Expired" | "QuotaExceeded";
  createdAt: string;
  updatedAt: string;
}

// Admin detail (same shape as AdminVoucherItem)
type AdminVoucherDetailResponse = AdminVoucherItem;

// Pagination wrapper for admin list
interface AdminVoucherListResponse {
  items: AdminVoucherItem[];
  totalCount: number;
  currentPage: number;
  totalPages: number;
}

// Payload for creating a voucher
interface CreateVoucherPayload {
  code: string;
  discountType: "Percentage" | "FixedAmount";
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  expiredAt: string;
  isActive: boolean;
}

// Payload for updating a voucher (same fields as create)
type UpdateVoucherPayload = CreateVoucherPayload;

// Payload for toggling voucher status
interface UpdateVoucherStatusPayload {
  isActive: boolean;
}

// Usage tracking record
interface VoucherUsageInfo {
  orderId: number;
  orderNo: string;
  customerName: string | null;
  productName: string;
  price: number;
  discountAmount: number;
  finalPrice: number;
  orderDate: string;
  orderStatus: string;
}
```

### Enum Reference

#### DiscountType

| Value | Meaning | Example |
|-------|---------|--------|
| `Percentage` | Percentage of product price | `discountValue: 10` → 10% off |
| `FixedAmount` | Fixed nominal discount in IDR | `discountValue: 5000` → Rp5.000 off |

#### VoucherStatus _(computed, not stored in DB)_

| Value | Meaning | Condition |
|-------|---------|-----------|
| `Active` | Usable at checkout | `isActive && expiredAt > now && usedCount < usageLimit` |
| `Disabled` | Manually deactivated | `isActive == false` |
| `Expired` | Past expiration | `expiredAt <= now` |
| `QuotaExceeded` | All uses consumed | `usedCount >= usageLimit` |

> Priority when multiple match: **Disabled** > **Expired** > **QuotaExceeded** > **Active**

### Discount Calculation

```
IF discountType == "Percentage":
    discountAmount = price × (discountValue / 100)
    IF maximumDiscount is not null:
        discountAmount = MIN(discountAmount, maximumDiscount)

IF discountType == "FixedAmount":
    discountAmount = discountValue

finalPrice = price - discountAmount
```

> `maximumDiscount` only applies to `Percentage` type. For `FixedAmount`, the discount is always exactly `discountValue`.

---

## Voucher Business Rules

| # | Rule | Detail |
|---|------|--------|
| 1 | **Code uniqueness** | Voucher code must be unique (case-insensitive). Duplicate codes are rejected on create and update |
| 2 | **Disable without delete** | Vouchers can be deactivated via `PATCH .../status` without losing data or usage history |
| 3 | **Expiration** | Vouchers have an expiration date (`expiredAt`). Expired vouchers cannot be used at checkout |
| 4 | **Usage limit** | Vouchers have a maximum number of uses (`usageLimit`). Once `usedCount >= usageLimit`, the voucher cannot be used |
| 5 | **Minimum transaction** | Vouchers can require a minimum order price. Orders below the threshold are rejected |
| 6 | **Maximum discount cap** | Percentage-type vouchers can have a cap (`maximumDiscount`) to limit the discount amount |
| 7 | **Expired = unusable** | Vouchers past their expiration date are automatically unusable, even if admin hasn't disabled them |
| 8 | **Quota exhausted = unusable** | Vouchers with no remaining quota cannot be used |
| 9 | **Disabled = unusable** | Vouchers that are disabled by admin cannot be used, regardless of other conditions |
| 10 | **Positive final price** | `finalPrice` must be > 0 after discount is applied |
| 11 | **Quota decrement timing** | Quota (`usedCount`) is decremented at **order creation time**, not at payment time |
| 12 | **Concurrency protection** | Quota reservation uses a single atomic conditional update (`usedCount` is incremented only where `usedCount < usageLimit`, evaluated in the database) to prevent lost updates and oversubscription under concurrent checkout |
| 13 | **Delete protection** | Vouchers that have been used (`usedCount > 0`) cannot be deleted — disable instead |
| 14 | **Case-insensitive matching** | Voucher codes are matched case-insensitively at checkout |

---

## Voucher Validation Rules

### Admin Validation (Create / Update)

| HTTP Status | Error Message | Condition |
|-------------|---------------|----------|
| 400 | `"Kode voucher harus antara 3 dan 50 karakter."` | Code length < 3 or > 50 |
| 400 | `"Kode voucher sudah digunakan."` | Code already exists (case-insensitive) |
| 400 | `"Nilai diskon harus lebih besar dari 0."` | `discountValue` ≤ 0 |
| 400 | `"Diskon persentase tidak boleh lebih dari 100%."` | Percentage type with value > 100 |
| 400 | `"Tipe diskon tidak valid. Pilih: Percentage atau FixedAmount."` | Invalid discountType value |
| 400 | `"Tanggal kedaluwarsa harus di masa depan."` | `expiredAt` is in the past |
| 400 | `"Batas penggunaan harus minimal 1."` | `usageLimit` < 1 |
| 400 | `"Batas penggunaan tidak boleh kurang dari jumlah yang sudah digunakan ({usedCount})."` | On update, `usageLimit` < current `usedCount` |
| 404 | `"Voucher tidak ditemukan."` | Voucher ID does not exist |
| 409 | `"Voucher tidak dapat dihapus karena sudah pernah digunakan ({usedCount} transaksi). Nonaktifkan voucher sebagai gantinya."` | Delete blocked: voucher has usage |

### Customer Validation (at Checkout — `POST /api/v1/orders/`)

Validation runs in this exact order. First failure stops the chain:

| HTTP Status | Error Message | Condition |
|-------------|---------------|----------|
| 400 | `"Kode voucher tidak ditemukan."` | Code does not exist (case-insensitive) |
| 400 | `"Voucher ini sudah tidak aktif."` | `isActive == false` |
| 400 | `"Voucher ini sudah kedaluwarsa."` | `expiredAt < UtcNow` |
| 400 | `"Kuota voucher ini sudah habis."` | `usedCount >= usageLimit` |
| 400 | `"Voucher ini membutuhkan minimal transaksi sebesar Rp{amount}."` | Product price < `minimumTransaction` |
| 400 | `"Harga akhir setelah diskon tidak boleh nol atau negatif."` | `finalPrice` ≤ 0 |
| 400 | `"Kuota voucher ini sudah habis (persaingan bersamaan)."` | Concurrent quota race condition |

---

## Voucher: Future-Ready Design

The Voucher model is designed to accommodate future features **without breaking changes**:

| Future Feature | How to Add | Breaking Change? |
|----------------|------------|------------------|
| Game-specific vouchers | Add nullable `gameId` field | No — null = applies to all games |
| Product-specific vouchers | Add nullable `productId` field | No — null = applies to all products |
| User-specific vouchers | Add nullable `customerId` field | No — null = applies to all users |
| Member-tier vouchers | Add nullable `memberTier` field | No — null = no tier restriction |
| Referral vouchers | Add `voucherType` enum field (default: `General`) | No — existing vouchers default to General |
| Affiliate vouchers | Add nullable `affiliateId` field | No — null = not affiliate-linked |
| Event / Flash Sale vouchers | Add `validFrom` datetime field | No — null = immediately valid |
| First purchase vouchers | Add `firstPurchaseOnly` boolean (default: false) | No — existing vouchers unaffected |
| Per-user usage limit | Add `maxUsagePerUser` integer field (default: null) | No — null = unlimited per user |

> **Principle**: All future fields are **nullable or have defaults**, so existing vouchers and API consumers are never broken by schema additions.

---

## Common HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created (POST /api/v1/admin/products/, POST /api/v1/admin/banners/upload, POST /api/v1/admin/vouchers) |
| 400 | Bad Request (validation failed, business rule violated) |
| 401 | Unauthorized (missing/invalid/expired token) |
| 404 | Not Found |
| 409 | Conflict (concurrency lock, voucher delete blocked by usage) |
| 429 | Too Many Requests (rate limited) |
