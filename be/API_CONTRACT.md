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

---

## Authentication & Authorization Patterns

### Token Format
- **Type**: Bearer JWT
- **Header**: `Authorization: Bearer <token>`

### Roles & Claims

| Role | JWT Claim | Token Lifetime | Issued By |
|------|-----------|----------------|-----------|
| CUSTOMER | `ClaimTypes.NameIdentifier` = customerId, `ClaimTypes.Role` = "CUSTOMER" | 24 hours | `POST /auth/local/login` |
| ADMIN | `ClaimTypes.Name` = username, `ClaimTypes.Role` = "ADMIN" | 12 hours | `POST /admin/auth/login` |

### Endpoint Protection Levels

| Prefix / Pattern | Auth Required | Role |
|------------------|---------------|------|
| `POST /auth/local/*`, `POST /auth/google`, `GET /games/*` | No | Public |
| `GET /banners/` | No | Public |
| `POST /orders/` | Optional | Any (customerId extracted if present) |
| `GET /auth/profile`, `PUT /auth/profile`, `GET /orders/my-history` | Yes | CUSTOMER |
| `/admin/*` | Yes | ADMIN |
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

### POST /admin/auth/login

#### ENDPOINT SUMMARY

| Property | Value |
|----------|-------|
| Method | `POST` |
| Path | `/admin/auth/login` |
| Description | Authenticate admin and receive JWT |
| Auth | None (Public) |

#### REQUEST PAYLOAD

| Field Name | Data Type | Required/Optional | Description & Constraints |
|------------|-----------|-------------------|---------------------------|
| `username` | `string` | Required | Admin username |
| `password` | `string` | Required | Admin password |

#### RESPONSE PAYLOAD

```typescript
// 200 OK
interface AdminLoginResponse {
  token: string; // JWT, expires in 12 hours
}
```

#### BUSINESS RULES & EDGE CASES

- Error 401: Invalid credentials
- JWT claims: `Name` = username, `Role` = "ADMIN"

---

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
  3. Must not be expired (`ValidUntil >= UtcNow`) → `"Voucher ini sudah kedaluwarsa."`
  4. Quota must be > 0 → `"Kuota voucher ini sudah habis."`
  5. Price must meet minimum → `"Voucher ini membutuhkan minimal transaksi sebesar Rp{amount}."`
- **Discount calculation**: `discountAmount = price × (percentage / 100)`, capped by `MaxDiscountAmount` if set
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
- Revenue = sum of `price` for COMPLETED orders
- Profit = sum of `customMargin` for COMPLETED orders
- Failed count includes both `FAILED` and `EXPIRED` statuses

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

## 7. Webhooks (Backend-to-Backend — NOT for frontend consumption)

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

```typescript
interface Voucher {
  code: string; // Unique, case-insensitive matching
  discountPercentage: number; // e.g. 10 = 10%
  minTransactionAmount: number; // Minimum order price to use voucher
  maxDiscountAmount: number | null; // null = no cap on discount
  validUntil: string; // ISO datetime
  quota: number; // Remaining uses
  isActive: boolean;
}
```

---

## Common HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success |
| 201 | Created (POST /admin/products/, POST /admin/banners/upload) |
| 400 | Bad Request (validation failed, business rule violated) |
| 401 | Unauthorized (missing/invalid/expired token) |
| 404 | Not Found |
| 409 | Conflict (concurrency lock) |
| 429 | Too Many Requests (rate limited) |
