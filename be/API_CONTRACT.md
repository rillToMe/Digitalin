# Digitalin Backend - API Contract Documentation

> **Target Audience**: Frontend AI Agent  
> **Base URL**: Configured per environment (see `launchSettings.json`)  
> **Content-Type**: `application/json` unless stated otherwise  
> **Date Format**: ISO 8601 (`yyyy-MM-ddTHH:mm:ss.fffZ`)

---

## CRITICAL WARNINGS

| # | Gotcha | Detail |
|---|--------|--------|
| 1 | **Upload Cover Game uses `GameMetadata` ID** | `POST /admin/system/metadata/{id}/upload-game-cover` — the `{id}` is a `GameMetadata.Id` |
| 2 | **Upload Item Image uses `GameProduct` ID** | `POST /admin/products/{id}/upload-item-image` — the `{id}` is a `GameProduct.Id` |
| 3 | **`inputType` determines form rendering** | If `USER_ID_AND_ZONE_ID`, the order form MUST show a Zone/Server ID field |
| 4 | **Typo in GET /auth/profile response** | The field is literally `"createtAt"` (NOT `"createdAt"`) — match this exactly |
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
| CUSTOMER | `ClaimTypes.NameIdentifier` = customerId, `ClaimTypes.Role` = "CUSTOMER" | 24 hours | `POST /auth/local/login` |
| ADMIN | `ClaimTypes.Name` = username, `ClaimTypes.Role` = `"ADMIN"` | 12 hours | `POST /admin/auth/login` |
| SUPER_ADMIN | `ClaimTypes.Name` = username, `ClaimTypes.Role` = `"SUPER_ADMIN"` | 12 hours | `POST /admin/auth/login` |

### Endpoint Protection Levels

| Prefix / Pattern | Auth Required | Role |
|------------------|---------------|------|
| `POST /auth/local/*`, `POST /auth/google`, `GET /games/*` | No | Public |
| `GET /banners/` | No | Public |
| `POST /orders/`, `POST /orders/preview` | Optional | Any (customerId extracted if present) |
| `GET /auth/profile`, `PUT /auth/profile`, `GET /orders/my-history` | Yes | CUSTOMER |
| `/admin/*` (`AdminOnly`) | Yes | ADMIN, SUPER_ADMIN |
| `/admin/admins/*` (`SuperAdminOnly`) | Yes | SUPER_ADMIN only |
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

### POST /admin/auth/login

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/auth/login` |
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

### POST /auth/local/request-otp

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/auth/local/request-otp` |
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

### POST /auth/local/register

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/auth/local/register` |
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

### POST /auth/local/login

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/auth/local/login` |
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

### POST /auth/google

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/auth/google` |
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
- **Existing local account with same email** → the account is linked by setting `GoogleId`. No duplicate account is created. Local password login continues to work.
- **Existing Google-linked account** → login proceeds normally, no DB write required.
- JWT claims are identical to local login: `NameIdentifier` = customerId, `Role` = "CUSTOMER", 24-hour expiry.
- Error 400: `"ID token tidak boleh kosong."` — missing or empty `idToken`
- Error 400: `"Invalid Google ID token."` — token failed verification (bad signature, wrong audience, expired, or malformed)
- Error 400: `"Google token verification failed."` — network or unexpected error during verification
- The backend does NOT store the Google ID token, access token, or refresh token — only `GoogleId` (the `sub` claim) and the email.

---

### GET /auth/profile

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/auth/profile` |
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
  createtAt: string; // ⚠️ TYPO IS INTENTIONAL — ISO datetime
}
```

#### BUSINESS RULES & EDGE CASES

- ⚠️ The field is `createtAt` (NOT `createdAt`) — this is a backend typo that must be matched exactly
- Error 401: Missing/invalid token
- Error 404: Customer not found

---

### PUT /auth/profile

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/auth/profile` |
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



### POST /auth/local/request-password-reset

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/auth/local/request-password-reset` |
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

### POST /auth/local/reset-password

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/auth/local/reset-password` |
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

### GET /admin/admins

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/admins` |
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

### POST /admin/admins

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/admins` |
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

### PUT /admin/admins/{id}/role

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/admins/{id}/role` |
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

### PUT /admin/admins/{id}/password

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/admins/{id}/password` |
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

### DELETE /admin/admins/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/admin/admins/{id}` |
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

### GET /games/brands

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/games/brands` |
| Description | List all game brands for the storefront catalog |
| Auth | None (Public) |

#### REQUEST PAYLOAD

_No request body or query parameters._

#### RESPONSE PAYLOAD

```typescript
// 200 OK
type BrandsResponse = BrandItem[];

interface BrandItem {
  id: number;
  brand: string;
  slug: string; // auto-generated: brand.toLowerCase().replace(" ", "-")
  imageUrl: string | null;
  inputType: "USER_ID_ONLY" | "USER_ID_AND_ZONE_ID";
}
```

#### BUSINESS RULES & EDGE CASES

- Results ordered alphabetically by `brand`
- `slug` is derived from brand name (lowercase, spaces → hyphens)
- `inputType` determines what fields the order form must show

---

### GET /games/{brandSlug}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/games/{brandSlug}` |
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
  inputType: "USER_ID_ONLY" | "USER_ID_AND_ZONE_ID";
  imageUrl: string | null;
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

---

### GET /admin/products/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/products/` |
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

### POST /admin/products/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/products/` |
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

### POST /admin/products/{id}/upload-item-image

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/products/{id}/upload-item-image` |
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

### PUT /admin/products/{id}/margin

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/products/{id}/margin` |
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

### POST /orders/preview

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/orders/preview` |
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
- If `voucherCode` is provided, the same validation chain runs as `POST /orders/`:
  1. Code must exist → `"Kode voucher tidak ditemukan."`
  2. Must be active → `"Voucher ini sudah tidak aktif."`
  3. Must not be expired → `"Voucher ini sudah kedaluwarsa."`
  4. Quota available → `"Kuota voucher ini sudah habis."`
  5. Minimum transaction → `"Voucher ini membutuhkan minimal transaksi sebesar Rp{amount}."`
- Discount calculation is **identical** to `POST /orders/` (same shared method)
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

### POST /orders/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/orders/` |
| Description | Create a new top-up order with optional voucher |
| Auth | Optional (JWT accepted but not required) |
| Headers | `Authorization: Bearer <token>` (optional) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `productId` | `integer` | Required | ID of the GameProduct to purchase |
| `targetId` | `string` | Required | Player's User ID |
| `zoneId` | `string \| null` | **CONDITIONAL** | **REQUIRED if product's `inputType` == `"USER_ID_AND_ZONE_ID"`**. Zone/Server ID |
| `voucherCode` | `string \| null` | Optional | Discount voucher code (case-insensitive) |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface CreateOrderResponse {
  id: number;
  orderNo: string; // Format: "INV-{yyyyMMdd}-{4 random uppercase chars}"
  playerId: string; // targetId + zoneId (concatenated, NO separator)
  status: string; // Initially "PENDING"
  productName: string;
  price: number; // Original price (before discount)
  snapToken: string | null; // Midtrans Snap token for payment popup
  redirectUrl: string | null; // Midtrans redirect URL
  orderDate: string; // ISO datetime
  sn: string | null; // Serial number (null until COMPLETED)
  imageUrl: string | null;
  discountAmount: number; // 0 if no voucher
  finalPrice: number; // price - discountAmount
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
- OrderNo format: `"INV-{yyyyMMdd}-{4 random uppercase chars}"`

---

### GET /orders/my-history

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/orders/my-history` |
| Description | Get paginated order history for authenticated customer |
| Auth | JWT Required (CUSTOMER) |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `page` | `integer` | Optional | Default = 1 |
| `limit` | `integer` | Optional | Default = 20 |

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
  snapToken: string | null;
  redirectUrl: string | null;
  orderDate: string;
  sn: string | null;
  imageUrl: string | null;
  discountAmount: number;
  finalPrice: number;
}
```

#### BUSINESS RULES & EDGE CASES

- Only returns orders belonging to the authenticated customer
- Ordered by `orderDate` descending (newest first)
- **Transaction Snapshot**: `productName`, `price`, and `imageUrl` are frozen at order creation time. Even if the product is later renamed, repriced, or deleted, historical orders retain the original values.

---

### GET /admin/orders/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/orders/` |
| Description | Get all orders (admin view) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD (Query Parameters)

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `page` | `integer` | Optional | Default = 1 |
| `limit` | `integer` | Optional | Default = 20 |

#### RESPONSE PAYLOAD

```typescript
// 200 OK — same structure as /orders/my-history
interface AdminOrdersResponse {
  data: OrderResponse[];
  totalItems: number;
  totalPages: number;
  currentPage: number;
}
```

#### BUSINESS RULES & EDGE CASES

- Returns ALL orders regardless of customer
- Same `OrderResponse` shape as customer endpoint

---

### POST /admin/orders/{orderNo}/retry

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/orders/{orderNo}/retry` |
| Description | Retry failed Digiflazz fulfillment for an order |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `orderNo` (path) | `string` | Required | Order number (e.g. `"INV-20260614-ABC1"`) |

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

### POST /admin/orders/{orderNo}/refund

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/orders/{orderNo}/refund` |
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

### GET /banners/

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/banners/` |
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

### POST /admin/banners/upload

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/banners/upload` |
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

### PUT /admin/banners/{id}/toggle

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/banners/{id}/toggle` |
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

### DELETE /admin/banners/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/admin/banners/{id}` |
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

### GET /admin/dashboard/stats

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/dashboard/stats` |
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
  totalProfitToday: number; // Sum of customMargin for COMPLETED orders today
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

### GET /admin/dashboard/chart

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/dashboard/chart` |
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

## 6. System / Admin Operations

---

### POST /admin/system/sync-digiflazz

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/system/sync-digiflazz` |
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

### DELETE /admin/system/clean-dummy-products

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/admin/system/clean-dummy-products` |
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

### GET /admin/system/midtrans-expiry

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/system/midtrans-expiry` |
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

### PUT /admin/system/midtrans-expiry

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/system/midtrans-expiry` |
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

### GET /admin/system/game-templates

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/system/game-templates` |
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

### GET /admin/system/metadata

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/system/metadata` |
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
  inputType: "USER_ID_ONLY" | "USER_ID_AND_ZONE_ID";
  serverList: string | null;
  imageUrl: string | null;
}
```

#### BUSINESS RULES & EDGE CASES

- Ordered alphabetically by brand

---

### PUT /admin/system/metadata/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/system/metadata/{id}` |
| Description | Update game metadata settings (input type, server list) |
| Auth | Admin JWT Required |
| Headers | `Authorization: Bearer <token>` |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `id` (path) | `integer` | Required | GameMetadata ID |
| `inputType` | `string` | Required | `"USER_ID_ONLY"` or `"USER_ID_AND_ZONE_ID"` |
| `serverList` | `string \| null` | Optional | Comma-separated server list or null |

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

### POST /admin/system/metadata/{id}/upload-game-cover

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/system/metadata/{id}/upload-game-cover` |
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

### GET /admin/settings/maintenance

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/settings/maintenance` |
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

### PUT /admin/settings/maintenance

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/settings/maintenance` |
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
  - `/admin/*` — admin panel
  - `/auth/local/login` — user login
  - `/auth/google` — Google OAuth
  - `/auth/local/request-password-reset` — password recovery
  - `/auth/local/reset-password` — password recovery
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

### GET /search/games

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/search/games` |
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
  inputType: "USER_ID_ONLY" | "USER_ID_AND_ZONE_ID";
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
GET /search/games?q=mobile
GET /search/games?q=free
GET /search/games?q=genshin&limit=5
GET /search/games?q=        → returns []
```

---

## 9. Voucher Management

> Voucher is a **first-class feature** with its own admin CRUD, validation pipeline, and usage tracking.  
> Vouchers are applied during order creation (`POST /orders/`) via the `voucherCode` field — there is no separate "apply voucher" endpoint.  
> See **Section 3 — POST /orders/** for the customer-facing voucher validation chain.

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

### GET /admin/vouchers

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/vouchers` |
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
GET /admin/vouchers?search=HEMAT&status=Active&sortBy=createdAt&sortOrder=desc&page=1&limit=20
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

### GET /admin/vouchers/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/vouchers/{id}` |
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
GET /admin/vouchers/1
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

### POST /admin/vouchers

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/vouchers` |
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

### PUT /admin/vouchers/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PUT` |
| Path | `/admin/vouchers/{id}` |
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

### PATCH /admin/vouchers/{id}/status

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `PATCH` |
| Path | `/admin/vouchers/{id}/status` |
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

### DELETE /admin/vouchers/{id}

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `DELETE` |
| Path | `/admin/vouchers/{id}` |
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
- **Soft approach**: If a voucher has been used, deletion is **blocked**. Admin must disable it instead via `PATCH /admin/vouchers/{id}/status`
- Deletion is **permanent** and irreversible for vouchers with zero usage
- Existing orders that referenced the voucher code retain their `voucherCode` and `discountAmount` — deletion does NOT affect completed orders

#### EXAMPLE REQUEST

```
DELETE /admin/vouchers/5
```

#### EXAMPLE RESPONSE

```json
{ "message": "Voucher berhasil dihapus." }
```

---

### GET /admin/vouchers/{id}/usage

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `GET` |
| Path | `/admin/vouchers/{id}/usage` |
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
GET /admin/vouchers/1/usage?page=1&limit=10
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
| `transaction_status` | `string` | Required | `"settlement"` \| `"capture"` \| `"expire"` \| `"cancel"` \| `"deny"` |
| `fraud_status` | `string` | Required | Fraud check result |
| `signature_key` | `string` | Required | `SHA512(order_id + status_code + gross_amount + ServerKey).ToLower()` |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface MidtransWebhookResponse {
  message: "Notification received";
}
```

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
| 12 | **Concurrency protection** | Quota decrement is protected by optimistic concurrency (row version) to prevent race conditions |
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

### Customer Validation (at Checkout — `POST /orders/`)

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
| 201 | Created (POST /admin/products/, POST /admin/banners/upload, POST /admin/vouchers) |
| 400 | Bad Request (validation failed, business rule violated) |
| 401 | Unauthorized (missing/invalid/expired token) |
| 404 | Not Found |
| 409 | Conflict (concurrency lock, voucher delete blocked by usage) |
| 429 | Too Many Requests (rate limited) |
