# Migration Plan: Minimal API → Controllers

> **Status:** Audit only — belum ada kode yang diubah.
> **Sumber:** Analisis codebase aktual (`be_digitalin/`), commit terakhir `2026-08-25`.
> **Prinsip target:** *Thin Controller → Fat Service → EF Core*

---

## 1. Current Architecture

**Stack:** .NET 10 Minimal API, EF Core 9 (MySQL/Pomelo), JWT Bearer, Scalar/OpenAPI, Redis, rate limiting (per-IP fixed window), custom `MaintenanceMiddleware`, CORS.

**Lapisan saat ini (sudah mendekati target):**

```
Endpoints/  (thin, 1.172 LOC, 9 files)   ← Minimal API Map* handlers
Services/v1 (fat, 4.372 LOC, 30 files)   ← business logic ✅ "Fat Service"
DTOs/       (Requests/Responses, 334 LOC)
Models/     (10 entities, 260 LOC)
Providers/  (7 external integrations)  Workers/  Middleware/  Infrastructure/
Domain/     (Pricing, Slug, Orders)   Config/  Constants/
```

**Fakta kunci:** codebase *sudah* berpola "Thin Endpoint → Fat Service". `ProductService` (1.232 LOC), `OrderService` (968 LOC), `VoucherService` (343 LOC) memegang seluruh business logic — query EF Core (LINQ), hashing, integrasi provider, validasi magic-bytes, dsb. Migrasi ini **hampir murni rewrite lapisan presentasi**: service, entity, database, provider, dan worker **tidak boleh ikut berubah**.

**Wiring saat ini** (`Program.cs`): satu group ber-version `api.MapGroup(AppConstants.Api.Prefix)` (`/api/v1`) berisi 8 panggilan `Map*Endpoints()`, webhook dipasang di root (`/webhooks`, tanpa prefix versi), plus `/ping-db` inline untuk testing koneksi DB. **Tidak ada satu pun Controller** di codebase (verified: 0 match `Controller`, `AddControllers`, `[ApiController]`).

**Catatan arsitektur lain yang ditemukan saat audit:**

- **DTO request terduplikasi.** `DTOs/Requests/AuthRequests.cs` mendefinisikan `LoginRequest`/`CustomerLoginRequest`/`RegisterRequest` **tanpa** field `CaptchaToken`/`OtpCode`, sedangkan endpoint yang benar-benar dipakai mendefinisikan record **inline** di `Endpoints/AuthEndpoints.cs` (dengan `CaptchaToken`, `OtpCode`). Karena tipe di namespace `be_digitalin.Endpoints` menang atas `using be_digitalin.DTOs.*`, file DTO tersebut praktis **dead code** — harus dikonsolidasi saat migrasi (SSOT).
- **Validasi manual** via `Validator.TryValidateObject` di `AuthEndpoints` (request-password-reset, reset-password) dan `SettingsEndpoints` (maintenance) — Minimal API tidak auto-validate DataAnnotations, sehingga dilakukan manual di endpoint.
- **Hasil binding entity mentah**: `POST /admin/products` mengikat `GameProduct` langsung dari body (over-posting risk; sebaiknya pakai request DTO).
- **Cek file upload (null/empty + 2 MB) diduplikasi** di 3 tempat (`ProductEndpoints` ×2, `BannerEndpoints`) padahal `ImageFileValidator` (magic bytes) sudah dipakai di service. Cek ukuran/null belum dipindah ke satu tempat.
- **Tidak ada test sama sekali** (0 test project/file; solution berisi 1 proyek).
- **Tidak ada global exception handler** — semua error ditangani per-action try/catch + helper mapping (mis. `MapOrderActionResult`, `MapAdminActionResult`, heuristik `ex.Message.Length < 100` di `POST /orders/`).

---

## 2. Migration Scope

### 2.1 Endpoints: 62 total (61 API + 1 infra)

| # | Domain | Jumlah Endpoint | Auth | File hari ini |
|---|--------|------------------|------|---------------|
| 1 | Customer Auth | 9 | 2 publik + rate-limit, 2 `[Authorize]` | `AuthEndpoints.cs` |
| 2 | Admin Auth | 1 | Anonymous + `AdminLoginPolicy` | `AuthEndpoints.cs` |
| 3 | Catalog (games publik) | 4 | Public | `ProductEndpoints.cs` |
| 4 | Admin Products | 4 | `AdminOnly` | `ProductEndpoints.cs` |
| 5 | Admin System | 12 | `AdminOnly` | `ProductEndpoints.cs` |
| 6 | Orders (publik) | 3 | 1 anonymous, 1 optional-auth, 1 `[Authorize]` | `OrderEndpoints.cs` |
| 7 | Admin Orders | 3 | `AdminOnly` | `OrderEndpoints.cs` |
| 8 | Webhooks | 2 | Anonymous (validasi signature) | `OrderEndpoints.cs` |
| 9 | Banners (publik) | 1 | Public | `BannerEndpoints.cs` |
| 10 | Admin Banners | 4 | `AdminOnly` | `BannerEndpoints.cs` |
| 11 | Admin Dashboard | 3 | `AdminOnly` | `DashboardEndpoints.cs` |
| 12 | Search | 1 | Public | `SearchEndpoints.cs` |
| 13 | Admin Settings | 2 | `AdminOnly` | `SettingsEndpoints.cs` |
| 14 | Admin Vouchers | 7 | `AdminOnly` | `VoucherEndpoints.cs` |
| 15 | Admin Management | 5 | `SuperAdminOnly` | `AdminManagementEndpoints.cs` |
| — | Infra | 1 (`/ping-db`) | — | `Program.cs` |

Rincian route per domain ada di **Lampiran A**.

### 2.2 Files: create / modify / delete

| Aksi | Jumlah | Detail |
|------|--------|--------|
| **Buat** | **15 Controller** + 0–3 helper | 1 Controller per batas `MapGroup` saat ini. Opsional: response DTO untuk bentuk anonymous, shim `ApiBehaviorOptions`, exception filter |
| **Ubah** | 1–2 | `Program.cs` (ganti `Map*Endpoints()` → `MapControllers()`); opsional folder baru `Controllers/` + `Filters/` |
| **Hapus** | **9** | seluruh isi `Endpoints/` |

### 2.3 Controller yang direkomendasikan

| Controller | Route base (`/api/v1`) | Jumlah Endpoint |
|------------|------------------------|-----------------|
| `AuthController` | `/auth` | 9 |
| `AdminAuthController` | `/admin/auth` | 1 |
| `CatalogController` | `/games` | 4 |
| `AdminProductController` | `/admin/products` | 4 |
| `AdminSystemController` | `/admin/system` | 12 |
| `OrderController` | `/orders` | 3 |
| `AdminOrderController` | `/admin/orders` | 3 |
| `WebhookController` | `/webhooks` (root, tanpa versi) | 2 |
| `BannerController` | `/banners` | 1 |
| `AdminBannerController` | `/admin/banners` | 4 |
| `DashboardController` | `/admin/dashboard` | 3 |
| `SearchController` | `/search` | 1 |
| `SettingsController` | `/admin/settings` | 2 |
| `VoucherController` | `/admin/vouchers` | 7 |
| `AdminManagementController` | `/admin/admins` | 5 |

> Controller publik vs admin **dipisah** supaya otorisasi bisa di level class (`[Authorize(Policy = "AdminOnly")]` / `"SuperAdminOnly"`), persis seperti `RequireAuthorization` per group hari ini. Pemetaan 1:1 dengan `MapGroup` saat ini = risiko routing paling kecil.

---

## 3. What Changes

### 3.1 Terjemahan mekanis (aman, 1:1)

| Hari ini (Minimal API) | Setara Controller |
|---|---|
| `api.MapGroup(prefix).MapGet(...)` | `[Route(...)]` + `[HttpGet]` |
| `.RequireAuthorization("AdminOnly")` (group) | `[Authorize(Policy = "AdminOnly")]` (class) |
| `.RequireRateLimiting("OtpPolicy")` | `[EnableRateLimiting("OtpPolicy")]` |
| `.AllowAnonymous()` | `[AllowAnonymous]` |
| `.DisableAntiforgery()` | tidak perlu — web API tanpa antiforgery default; cukup dibuang |
| `[FromBody]` / `[FromQuery]` / `[FromForm]` di handler | identik, kini di-binding MVC |
| `[FromServices] IService s` | constructor injection (boleh tetap `[FromServices]`) |
| `Results.Ok/NotFound/BadRequest/Created/Problem/Json/Unauthorized/Conflict` | `Ok/NotFound/BadRequest/Created/Problem/StatusCode/Unauthorized/Conflict` |
| `ClaimsPrincipal user` parameter | properti `User` di controller |
| `HttpContext` / `req.Body` | `HttpContext` / `Request.Body` |
| `.WithTags("...")` | `[Tags("...")]` (menjaga grouping di Scalar) |
| clamping `page`/`limit`/`MaxBrandPageSize` inline | tetap di action (web plumbing) atau pindah ke service — lihat 3.2 |
| `MapGroup(...).MapControllers()` | didukung .NET 8+; prefix `/api/v1` tetap berlaku |

### 3.2 Business logic yang masih di endpoint (wajib dipindah / dinormalisasi)

1. **Validasi manual DataAnnotations** — `Validator.TryValidateObject` di `AuthEndpoints` (request-password-reset, reset-password) dan `SettingsEndpoints` (maintenance). Dengan `[ApiController]` validasi jadi otomatis — **tapi bentuk error berubah** (lihat Risiko #1). Pesan error dari atribut harus dipertahankan.
2. **Precondition cek file upload** — null/empty + >2 MB diduplikasi di `ProductEndpoints` (×2) dan `BannerEndpoints`. Validasi magic-bytes sudah di `ImageFileValidator` (dipakai service). Pindahkan cek ukuran/null ke helper bersama (atau ke service) agar 3 jalur upload konsisten.
3. **Ekstraksi `customerId` dari claims** — diulang di 4 tempat (`orders/`, `orders/my-history`, `auth/profile` GET & PUT): buat helper `GetCustomerId()` / controller base.
4. **`POST /admin/products` mengikat `GameProduct` mentah** — ganti dengan request DTO (`CreateProductRequest`) untuk mencegah over-posting; signature service tidak berubah (mapping cukup di controller).
5. **Validasi signature webhook Digiflazz** — tetap di controller sebagai plumbing HTTP, atau dipindah ke filter agar `WebhookController` tetap tipis.
6. **Objek respons anonymous** (`new { message, url }`, `new { token }`, `new { customer.Email, FullName, CreatetAt }`, pesan retry/refund) — ganti response DTO agar schema OpenAPI selamat (JSON runtime tetap identik).
7. **Helper mapping** `MapOrderActionResult` / `MapAdminActionResult` — tetap di controller (mapping HTTP = urusan web), atau naikkan ke filter tipis nanti.

### 3.3 Perubahan `Program.cs`

- `builder.Services.AddControllers();` — daftarkan MVC. `AddEndpointsApiExplorer()` + `AddOpenApi()` tetap dipertahankan (melayani controller juga).
- Ganti 8 panggilan `api.Map*Endpoints()` + `app.MapWebhookEndpoints()` dengan:
  - `var api = app.MapGroup(AppConstants.Api.Prefix); api.MapControllers();`
  - `app.MapGroup("webhooks").MapControllers();`
- **Pipeline tidak berubah:** CORS → security headers → `UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `UseMaintenanceMode` → endpoints. Tambah `UseExceptionHandler` **hanya jika** mau global error handler; selain itu pertahankan try/catch per-action 1:1.
- `/ping-db` → pindah ke `HealthController` kecil (atau biarkan; di luar kontrak API) supaya tidak ada lagi Minimal API yang tersisa.
- Registrasi DI service (blok `AddScoped`) **tidak berubah**.

---

## 4. What Stays Unchanged

| Area | Files | Alasan aman |
|------|-------|-------------|
| **Services** | seluruh `Services/v1/**` (30 file) | sudah "Fat Service"; controller hanya mengganti pemanggil |
| **Models / entities** | seluruh `Models/` (10 file) | schema DB tidak disentuh |
| **Data / DbContext** | `Data/DigitalinDbContext.cs` | schema tidak berubah |
| **Migrations** | `Migrations/**` | tidak ada perubahan schema |
| **Providers** | seluruh `Providers/` (7 file) | integrasi eksternal tidak disentuh |
| **Workers** | `Workers/*` | background jobs tidak disentuh |
| **Middleware** | `MaintenanceMiddleware.cs` | beroperasi pada path mentah, independen dari gaya endpoint |
| **Authorization policies + JWT** | blok policy di `Program.cs` | nama `AdminOnly`/`SuperAdminOnly` dipakai ulang apa adanya |
| **Rate limiter policies** | `OtpPolicy`, `CustomerLoginPolicy`, `AdminLoginPolicy` | dipakai ulang via `[EnableRateLimiting]` |
| **OpenAPI transformer** | `BearerSecuritySchemeTransformer.cs` | level dokumen, tidak terpengaruh |
| **Infrastructure** | Redis, Serialization, `ImageFileValidator` | tidak tersentuh |
| **Constants / Config / Domain** | `AppConstants`, settings, `Pricing`, `Slug`, dll. | tidak tersentuh |
| **DTOs/Responses** | seluruh file | kontrak respons dipertahankan |
| **Registrasi DI (services)** | blok `AddScoped` di `Program.cs` | tidak berubah |

**Satu cleanup yang digabung (tidak wajib):** `DTOs/Requests/AuthRequests.cs` adalah **dead code** — definisi `LoginRequest`/`CustomerLoginRequest`/`RegisterRequest` tanpa `CaptchaToken`/`OtpCode` (endpoint yang hidup memakai record inline di `Endpoints/AuthEndpoints.cs` yang men-shading namespace DTO). Saat migrasi: pindahkan record inline ke `DTOs/Requests` dan hapus file lama (SSOT sesuai AGENTS.md).

---

## 5. Risks

| # | Risiko | Severity | Mitigasi |
|---|--------|----------|----------|
| 1 | **`[ApiController]` auto-400 mengubah bentuk error.** Request invalid hari ini dikembalikan `{ message: "..." }`; controller mengembalikan `ValidationProblemDetails` (`{ title, status, errors }`). **Ini break behavioral terbesar.** | 🔴 High | Konfigurasi `ApiBehaviorOptions.InvalidModelStateResponseFactory` agar tetap emit `{ message = firstError }` dengan pesan DataAnnotations yang sama. Verifikasi tiap jalur error via file `.http`. |
| 2 | **Tidak ada test sama sekali** (0 test project). Tidak ada safety net untuk rewrite 62 endpoint. | 🔴 High | Pakai suite `http/*.http` yang ada sebagai harness regression manual; idealnya buat proyek test kontrak/replay **sebelum** migrasi jika anggaran memungkinkan. |
| 3 | **Parity routing** — URL trailing-slash di kontrak & `.http` (`/admin/products/`, `/banners/`, `/admin/orders/`, `/admin/vouchers/`). Endpoint routing umumnya toleran trailing slash, tapi setiap path wajib diverifikasi ulang. | 🟠 Medium | Setelah tiap fase: diff tabel route; jalankan semua file `.http`; cek 404 duluan. |
| 4 | **Order creation = optional-auth + menyentuh uang** (Snap Midtrans, race quota voucher, alur status). Properti `User` di action tanpa `[Authorize]` berperilaku sama, tapi jalur `customerId = null` wajib diverifikasi. | 🟠 Medium | Migrasi orders di fase akhir; uji alur login & anonymous; service tidak tersentuh. |
| 5 | **Drift schema OpenAPI/Scalar** — tipe respons anonymous kehilangan schema-nya di bawah controller tanpa `[ProducesResponseType]`/DTO. Tags harus dipasang per controller. Transformer security scheme tetap jalan. | 🟡 Medium | Tambah response DTO untuk ±10 bentuk anonymous; tambah `[Tags]`; spot-check Scalar tiap fase. |
| 6 | **Kontrak webhook eksternal** — dashboard Digiflazz/Midtrans menunjuk `/webhooks/*`; parsing raw-body + signature harus byte-identik. | 🟡 Medium | Migrasi paling akhir; replay payload asli; perilaku 200 short-circuit dipertahankan. |
| 7 | **Global exception handling** — belum ada hari ini; pilihan try/catch per-action (mis. heuristik `ex.Message.Length < 100` di `POST /orders/`) harus disalin persis, atau diganti `IExceptionHandler` yang mereproduksi pesan sama. | 🟡 Medium | Putuskan eksplisit: pertahankan try/catch per-action 1:1 (aman) vs global handler (lebih bersih, butuh tes kompatibilitas pesan). |
| 8 | **`POST /admin/products` tetap bind `GameProduct`** — bekerja, tapi melanggengkan risiko over-posting jika disentuh ceroboh. | 🟢 Low | Perkenalkan `CreateProductRequest` DTO; signature service tetap; mapping cukup di controller. |

---

## 6. Recommended Migration Strategy

**Rekomendasi: INKREMENTAL per domain, dalam satu jendela rilis pendek** — bukan big-bang murni, bukan hybrid jangka panjang.

**Kenapa bukan big-bang:** 62 endpoint, nol test otomatis, satu kontrak webhook eksternal yang live, dan satu perubahan bentuk validasi yang harus dibuktikan. Commit tunggal ~3.000 baris yang membalik seluruh app tidak punya cerita rollback selain revert menyeluruh.

**Kenapa bukan hybrid jangka panjang:** Minimal API + Controller **bisa** hidup berdampingan (keduanya teregistrasi), tetapi keduanya punya perilaku validasi/error yang beda di sisi yang sama, dan dua gaya penulisan route — beban kognitif tinggi, mudah bocor. Hybrid wajib **dibatasi fase dan pendek**.

**Bentuk fase:** tiap fase menjaga app tetap compile, mempertahankan *kedua* gaya sementara, dan diakhiri hijau (build + regression `.http` penuh + diff route/kontrak). Urutan aman = **read-only → CRUD dengan body → auth → uang → webhook eksternal**, sehingga domain paling berisiko mendapat verifikasi paling banyak:

| Fase | Scope | Verifikasi |
|------|-------|------------|
| **0. Prep** | `AddControllers()`, shim bentuk validasi, response DTO, `ApiBehaviorOptions` factory, baseline `.http` + dump route | build + baseline |
| **1. Pilot (read-only, tanpa binding body)** | `SearchController`, `SettingsController`, `DashboardController` | `.http` 04/05 + Scalar |
| **2. Domain baca publik** | `CatalogController`, `BannerController`, preview `OrderController` | `.http` 02/03 |
| **3. Baca admin + aksi sederhana** | `AdminBannerController`, `VoucherController` (GET dulu), `AdminProductController` | `.http` 03/04 |
| **4. CRUD + upload file** | `VoucherController` penuh, upload `AdminProductController`, `AdminSystemController` (12), upload `BannerController` | `.http` + jalur true/false |
| **5. Alur auth** | `AuthController` (9), `AdminAuthController` — rate limit, reCAPTCHA, Google, OTP | `.http` 01 + rate-limit + profile |
| **6. Jalur uang** | `OrderController` (create/history), `AdminOrderController` | `.http` 03 + optional-auth + Midtrans |
| **7. Webhooks** | `WebhookController` (midtrans, digiflazz) | replay payload asli |
| **8. Cleanup + lock-in** | hapus 9 `Endpoints/*.cs`, buang panggilan `Map*Endpoints()`, pindah `/ping-db`, full build + sweep `.http`, update `API_CONTRACT.md` jika bentuk error berubah | regression penuh + `git diff --stat` |

**Rollback** kapan saja = revert commit fase; service/entity tidak pernah berpindah, jadi tidak ada yang rusak di hilir.

---

## 7. Implementation Checklist

**Prep / infra**
- [ ] Buat folder `Controllers/`; tambah `builder.Services.AddControllers();`
- [ ] Konfigurasi `ApiBehaviorOptions.InvalidModelStateResponseFactory` → bentuk `{ message }` (pertahankan kontrak)
- [ ] Ekstrak record request inline (`AuthEndpoints`, `ProductEndpoints`) ke `DTOs/Requests`; hapus `AuthRequests.cs` lama
- [ ] Tambah response DTO untuk semua respons anonymous `new { ... }`
- [ ] Baseline: `dotnet build`, catat tabel route, jalankan semua `http/*.http`, simpan diff respons

**Per domain (Fase 1–7)**
- [ ] Buat controller dengan `[Route(...)]` + `[Authorize]`/`[EnableRateLimiting]`/`[Tags]` setia pada group saat ini
- [ ] Terjemahkan tiap action 1:1 (`Results.X` → `ActionResult`, helper claims, cek file)
- [ ] Pertahankan group endpoint lama tetap terdaftar (fase hybrid hijau)
- [ ] Build + jalankan file `.http` domain; diff respons vs baseline (body, status, headers)

**Domain uang & webhook**
- [ ] Orders: verifikasi alur anonymous (`customerId = null`) dan logged-in; konfirmasi rate-limit dan payload Midtrans
- [ ] Webhooks: parsing raw-body + signature identik; perilaku 200 short-circuit identik

**Finalisasi**
- [ ] Buang semua panggilan `Map*Endpoints()`; hapus `Endpoints/` (9 file)
- [ ] Pindah `/ping-db` ke controller (atau biarkan — di luar kontrak)
- [ ] Full build, full sweep `.http`, diff tabel route (62 == 62, nol 404)
- [ ] Konfirmasi policy, rate-limit, CORS, maintenance middleware, Scalar + bearer auth tidak berubah
- [ ] Update `API_CONTRACT.md` **hanya jika** bentuk error validasi atau schema benar-benar berubah (tujuan: tidak ada perubahan)
- [ ] Commit per fase (diff kecil, mudah di-revert)

---

## 8. Kesimpulan

Ini adalah migrasi lapisan presentasi, bukan bisnis logic: **services (4.372 LOC), entity, DB, provider, dan worker semuanya tetap di tempat**. Dua hal yang benar-benar bisa mematahkan kontrak adalah (1) auto-validation `[ApiController]` yang mengubah bentuk error body dan (2) parity route/trailing-slash — keduanya terkendali dengan shim dan regression per-fase yang ketat. Migrasi **inkremental berbasis domain dengan pilot read-only** adalah jalur teraman mengingat nol test otomatis.

---

## Lampiran A — Daftar Route per Domain (62)

### 1. Customer Auth (9) — `AuthController`
| Method | Path | Auth / Rate Limit |
|--------|------|-------------------|
| POST | `/api/v1/auth/local/request-otp` | `OtpPolicy` (3/10 mnt/IP) |
| POST | `/api/v1/auth/local/register` | — |
| POST | `/api/v1/auth/local/login` | `CustomerLoginPolicy` (10/5 mnt/IP) |
| POST | `/api/v1/auth/local/request-password-reset` | `OtpPolicy` |
| POST | `/api/v1/auth/local/reset-password` | `OtpPolicy` |
| POST | `/api/v1/auth/google` | `CustomerLoginPolicy` |
| GET | `/api/v1/auth/profile` | `[Authorize]` |
| PUT | `/api/v1/auth/profile` | `[Authorize]` |
| GET | `/api/v1/auth/check-email` | — |

### 2. Admin Auth (1) — `AdminAuthController`
| Method | Path | Auth |
|--------|------|------|
| POST | `/api/v1/admin/auth/login` | `[AllowAnonymous]` + `AdminLoginPolicy` |

### 3. Catalog Publik (4) — `CatalogController`
| Method | Path |
|--------|------|
| GET | `/api/v1/games/brands?category=&offset=&limit=` |
| GET | `/api/v1/games/brands/popular` |
| GET | `/api/v1/games/categories` |
| GET | `/api/v1/games/{brandSlug}` |

### 4. Admin Products (4) — `AdminProductController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/products/?search=&brand=&page=&limit=` |
| POST | `/api/v1/admin/products/` (⚠ bind `GameProduct` mentah) |
| POST | `/api/v1/admin/products/{id}/upload-item-image` |
| PUT | `/api/v1/admin/products/{id}/margin` |

### 5. Admin System (12) — `AdminSystemController`
| Method | Path |
|--------|------|
| POST | `/api/v1/admin/system/sync-digiflazz` |
| DELETE | `/api/v1/admin/system/clean-dummy-products` |
| GET | `/api/v1/admin/system/midtrans-expiry` |
| PUT | `/api/v1/admin/system/midtrans-expiry` |
| GET | `/api/v1/admin/system/game-templates` |
| GET | `/api/v1/admin/system/game-categories` |
| PUT | `/api/v1/admin/system/game-categories` |
| GET | `/api/v1/admin/system/popular-games` |
| PUT | `/api/v1/admin/system/popular-games` |
| GET | `/api/v1/admin/system/metadata` |
| PUT | `/api/v1/admin/system/metadata/{id}` |
| POST | `/api/v1/admin/system/metadata/{id}/upload-game-cover` |

### 6. Orders Publik (3) — `OrderController`
| Method | Path | Auth |
|--------|------|------|
| POST | `/api/v1/orders/` | Optional (customerId dari JWT jika ada) |
| POST | `/api/v1/orders/preview` | Public |
| GET | `/api/v1/orders/my-history?page=&limit=` | `[Authorize]` |

### 7. Admin Orders (3) — `AdminOrderController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/orders/` |
| POST | `/api/v1/admin/orders/{orderNo}/retry` |
| POST | `/api/v1/admin/orders/{orderNo}/refund` |

### 8. Webhooks (2) — `WebhookController` (root, tanpa prefix versi)
| Method | Path | Keterangan |
|--------|------|------------|
| POST | `/webhooks/midtrans` | signature divalidasi di service (`HandleNotificationAsync`) |
| POST | `/webhooks/digiflazz` | raw-body + header `x-hub-signature` divalidasi di endpoint via `DigiflazzProvider.ValidateWebhookSignature` |

### 9. Banners Publik (1) — `BannerController`
| Method | Path |
|--------|------|
| GET | `/api/v1/banners/` |

### 10. Admin Banners (4) — `AdminBannerController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/banners/` |
| POST | `/api/v1/admin/banners/upload` |
| PUT | `/api/v1/admin/banners/{id}/toggle` |
| DELETE | `/api/v1/admin/banners/{id}` |

### 11. Admin Dashboard (3) — `DashboardController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/dashboard/stats` |
| GET | `/api/v1/admin/dashboard/chart?range=&startDate=&endDate=` |
| GET | `/api/v1/admin/dashboard/balance` (502 via `Results.Json` saat supplier gagal) |

### 12. Search (1) — `SearchController`
| Method | Path |
|--------|------|
| GET | `/api/v1/search/games?q=&limit=` |

### 13. Admin Settings (2) — `SettingsController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/settings/maintenance` |
| PUT | `/api/v1/admin/settings/maintenance` |

### 14. Admin Vouchers (7) — `VoucherController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/vouchers/?search=&status=&sortBy=&sortOrder=&page=&limit=` |
| GET | `/api/v1/admin/vouchers/{id}` |
| POST | `/api/v1/admin/vouchers/` |
| PUT | `/api/v1/admin/vouchers/{id}` |
| PATCH | `/api/v1/admin/vouchers/{id}/status` |
| DELETE | `/api/v1/admin/vouchers/{id}` (404 / 409) |
| GET | `/api/v1/admin/vouchers/{id}/usage?page=&limit=` |

### 15. Admin Management (5) — `AdminManagementController`
| Method | Path |
|--------|------|
| GET | `/api/v1/admin/admins/` |
| POST | `/api/v1/admin/admins/` |
| PUT | `/api/v1/admin/admins/{id}/role` |
| PUT | `/api/v1/admin/admins/{id}/password` |
| DELETE | `/api/v1/admin/admins/{id}` |

### Infra (1)
| Method | Path | Keterangan |
|--------|------|------------|
| GET | `/ping-db` | testing koneksi DB, di luar kontrak API |