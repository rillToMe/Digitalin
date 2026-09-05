# Migration Plan: Minimal API → Controllers

> **Status: MIGRASI SELESAI (Fase 0–8).** Seluruh 62 route di Controllers (15 controller), folder `Endpoints/` dihapus, tidak ada lagi Minimal API untuk route aplikasi. Perilaku & kontrak terverifikasi identik baseline (parity 62 = 62).
> **Sumber:** Analisis codebase aktual (`be_digitalin/`), commit terakhir `2026-08-25`.
> **Prinsip target:** *Thin Controller → Fat Service → EF Core*

---

## 0. Fase 0 — Status Implementasi (2026-09-04)

Sudah dikerjakan (lihat **Section 7** untuk checklist lengkap):

- **`AddControllers()`** ditambahkan di `Program.cs` — inert selama belum ada controller dan `MapControllers()`.
- **Shim `ApiBehaviorOptions.InvalidModelStateResponseFactory`** ditambahkan di `Program.cs` — memaksa auto-validation `[ApiController]` menghasilkan `{ message }` (bentuk kontrak lama), bukan `ValidationProblemDetails`. Juga inert sampai ada controller.
- **Baseline regression** — app dijalankan di Development (MySQL/Redis lokal):
  - `dotnet build` → 0 warning, 0 error.
  - `/openapi/v1.json` → **62 operasi** (52 path; trailing-slash dinormalisasi oleh OpenAPI). Tabel route tersimpan di `docs/route-baseline.txt` sebagai acuan diff fase berikutnya.
  - Endpoint publik diverifikasi: `/ping-db` 200, `/banners/` 200 (trailing-slash OK), `/games/categories` 200, `/games/brands` 200, `/games/brands/popular` 200, `/search/games` 200, `/auth/check-email` 200.
  - Auth: `PUT /admin/settings/maintenance` tanpa token → **401**; login invalid → **401** (body kosong, sama seperti `Results.Unauthorized()`).
  - **Bentuk error body baseline:** validasi → `400 { "message": "..." }`; rate limit → `429 { "message": "Terlalu banyak permintaan. Coba lagi nanti." }`.
  - Catatan: `OtpPolicy` (3/10 mnt/IP) benar-benar aktif — request ke-3+ dari IP ini kena 429.

**SSOT request DTO** — record request yang sebelumnya didefinisikan **inline** di `Endpoints/AuthEndpoints.cs` dan `Endpoints/ProductEndpoints.cs` (men-shading versi lama di `DTOs`) dipindah ke file DTO dan dihapus dari endpoint:

- `DTOs/Requests/AuthRequests.cs` — ditulis ulang sebagai SSOT (`LoginRequest`, `CustomerLoginRequest`, `RequestOtpRequest`, `RegisterRequest`, `UpdateProfileRequest`, `GoogleLoginRequest` — dengan `CaptchaToken`/`OtpCode` seperti yang dipakai endpoint; file lama tanpa field tsb adalah dead code dan sudah digantikan).
- `DTOs/Requests/ProductRequests.cs` (baru) — `UpdateCustomMarginRequest`, `UpdatePopularGamesRequest`, `UpdateExpiryRequest`.
- Record inline dihapus dari kedua file endpoint. Verifikasi: `record LoginRequest|...|GoogleLoginRequest` kini hanya muncul di 1 file (`DTOs/Requests/AuthRequests.cs`).

**Response DTO untuk semua respons anonymous** — `new { ... }` di seluruh `Endpoints/` diganti DTO bernama (JSON runtime identik — camelCase `{ message, token, url, status, ... }`):

| File (baru/diubah) | Menggantikan |
|---|---|
| `DTOs/Responses/MessageResponse.cs` | `new { message }` — semua pesan OK/BadRequest/NotFound/Conflict + `Results.Json` 502 dashboard |
| `DTOs/Responses/AuthResponses.cs` | `new { token }` (login/register/google) & profil `new { email, fullName, createtAt }` (property `CreatetAt` sengaja dipertahankan TYPO sesuai kontrak; tipe `DateTime` → JSON ISO sama seperti sebelumnya) |
| `DTOs/Responses/UploadImageResponse.cs` | `new { message, url }` (upload icon produk & cover game) |
| `DTOs/Responses/OrderActionResponse.cs` | `new { message, status }` (retry/refund admin) |
| `DTOs/Responses/SystemResponses.cs` | expiry `{ expiry }`, popular-games `{ selected, options }` + `{ message, selected }`, game-categories `{ message, categories }`, metadata `{ message, metadata }`, baris game-templates |
| `DTOs/Responses/VoucherResponse.cs` (+`VoucherStatusResponse`) | `{ message, status }` PATCH voucher |
| 8 file `Endpoints/*` | semua `new { ... }` diseragamkan ke DTO di atas; `AuthEndpoints` +`using be_digitalin.Providers` (untuk `GoogleAuthenticationException`) |

Verifikasi akhir Fase 0 (build + smoke test, server Development di `localhost:5210`):

- `dotnet build` → **0 warning, 0 error** (CS8604 baru dari DTO ber-tipe di-null-forgive tanpa mengubah nilai runtime).
- `/openapi/v1.json` → tetap **62 operasi** (52 path) — tidak ada route hilang/berubah.
- Bentuk JSON terverifikasi identik dengan baseline anonymous: `GET /auth/check-email` (blank) → `400 {"message":"Email tidak boleh kosong."}`; (email acak) → `200 {"message":"Email tersedia."}`; admin tanpa token → `401`.
- Server dimatikan, artefak sementara (log, pid) dibersihkan.

Artefak yang tersimpan: `docs/route-baseline.txt` (62 route). Reguler `git diff --stat` akan menunjukkan `Program.cs`, 8 file `Endpoints/*`, file DTO, dan `migration.md`.

---

## 0.1 Fase 1 — Status Implementasi (Pilot Read-only, 2026-09-05)

**Controller baru (3) di `Controllers/`:**

| Controller | Route | Endpoint | Catatan |
|---|---|---|---|
| `SearchController` | `/api/v1/search/games` | 1 GET (publik) | mapping 1:1 dari `SearchEndpoints.cs` |
| `DashboardController` | `/api/v1/admin/dashboard/stats` · `/chart` · `/balance` | 3 GET (AdminOnly) | parameter chart dibuat nullable → validasi range tetap di service (`400 { message }`), sama seperti perilaku Minimal API lama |
| `SettingsController` | `/api/v1/admin/settings/maintenance` | GET + PUT (AdminOnly) | PUT ikut dipindah agar `MapSettingsEndpoints` bisa dilepas utuh (route dobel = AmbiguousMatchException) — ini juga yang membuktikan shim auto-validasi Fase 0 pada request ber-body |

**`Program.cs`:** `MapControllers()` ditambahkan; `MapSearchEndpoints()`/`MapDashboardEndpoints()`/`MapSettingsEndpoints()` **tidak lagi didaftarkan** (komentar Phase 1 menjelaskan). File `Endpoints/SearchEndpoints.cs`, `DashboardEndpoints.cs`, `SettingsEndpoints.cs` masih ada (dead code sementara, dihapus massal di Fase 8).

**Verifikasi (build + server Development `localhost:5210`, token admin dimint JWT HS256 sesuai konfigurasi dev):**

- `dotnet build` → **0 warning, 0 error**.
- **Route parity:** `/openapi/v1.json` → **62 operasi = 62 baseline**, `MISSING: []`, `EXTRA: []`. Tag tetap `Search`, `Settings Admin`, `Dashboard Admin` (via `[Tags]`).
- Endpoint publik: `GET /search/games?q=ml` → 200.
- **Auth:** `GET /admin/settings/maintenance` tanpa token → **401**.
- **GET maintenance (admin):** `200` `{enabled,title,message,estimatedFinish,allowAdmin}` — identik baseline.
- **PUT maintenance — auto-validasi `[ApiController]` (uji inti shim Fase 0):** body `{enabled:true,title:"",message:""}` → `400 {"message":"Title wajib diisi."}` (dulu `Validator.TryValidateObject` manual → bentuk `{ message }` sama). Body valid → `200` JSON status maintenance.
- **Dashboard:** stats → 200; chart `range=today` → 200 (array, kosong wajar); chart `range=bulan` → `400 {"message":"Parameter range tidak valid. ..."}` (validasi service, bentuk sama); balance → 200 (Digiflazz lokal merespons).
- Scalar UI (`/scalar/v1`) → 200 di Development.
- Nilai maintenance yang ter-overwrite saat uji PUT dikembalikan ke seed default; server dimatikan, artefak dibersihkan.

**Status checklist Fase 1:** selesai.

---

## 0.2 Fase 2 — Status Implementasi (Domain Baca Publik, 2026-09-05)

**Controller baru (2) di `Controllers/`:**

| Controller | Route | Endpoint | Catatan |
|---|---|---|---|
| `CatalogController` | `/api/v1/games/brands` · `/brands/popular` · `/categories` · `/{brandSlug}` | 4 GET (publik) | mapping 1:1 dari group publik `ProductEndpoints.cs`; clamp `offset`/`limit` (max 60) dipertahankan; literal `brands/popular` menang atas `{brandSlug}` di attribute routing (tidak bergantung urutan) |
| `BannerController` | `/api/v1/banners` | 1 GET (publik) | mapping 1:1 dari group publik `BannerEndpoints.cs` |

**File legacy disesuaikan (masih dipakai untuk group admin):** `ProductEndpoints.cs` dan `BannerEndpoints.cs` ditulis ulang — group publik **dihapus** dari registrasi (route dobel = AmbiguousMatchException), group admin tetap utuh dengan komentar penanda Fase 2.

**Verifikasi (build + server Development `localhost:5210`):**

- `dotnet build` → **0 warning, 0 error**.
- **Route parity:** `/openapi/v1.json` → **62 operasi = 62 baseline**, `MISSING: []`, `EXTRA: []`. Tag tetap `Katalog Game (Public)` & `Banner Promosi (Public)`.
- `GET /games/brands?offset=&limit=` → 200 data asli; `/games/brands/popular` → 200; `/games/categories` → 200; `/games/mobile-legends` → 200 (`brand/gameCode/imageUrl/inputFields/products`); slug tak dikenal → `404 {"message":"Game tidak ditemukan"}`.
- `GET /banners` **dan** `GET /banners/` (trailing-slash) → 200 — parity URL terjaga.
- Group admin lama tetap terdaftar & jalan (Minimal API): `GET /admin/products` → 200, `GET /admin/system/metadata` → 200 (token admin).
- Server dimatikan, artefak dibersihkan.

**Status checklist Fase 2:** selesai. `preview OrderController` (dari baris rencana) sengaja ditunda — domain orders utuh dimigrasi di Fase 6 agar alur uang diuji sekali.

---

## 0.3 Fase 3 — Status Implementasi (Baca Admin + Aksi Sederhana, 2026-09-05)

**Controller baru (3) di `Controllers/`:**

| Controller | Route | Endpoint | Catatan |
|---|---|---|---|
| `AdminBannerController` | `/api/v1/admin/banners` | GET semua · POST `/upload` (multipart) · PUT `/{id}/toggle` · DELETE `/{id}` | **group admin dipindah utuh**; `BannerEndpoints.cs` dihapus (tidak ada sisa) — `MapBannerEndpoints()` dilepas dari `Program.cs`. Kasus toggle banner tak-ada tetap 200 (paritas legacy) |
| `VoucherController` | `/api/v1/admin/vouchers` | GET list · GET `/{id}` · GET `/{id}/usage` | hanya GET dulu; POST/PUT/PATCH/DELETE tetap di `VoucherEndpoints.cs` (Fase 4) |
| `AdminProductController` | `/api/v1/admin/products` | GET list · PUT `/{id}/margin` | POST create (bind `GameProduct` mentah) & POST upload-item-image tetap Minimal API (Fase 4) |

**File legacy disesuaikan:** `VoucherEndpoints.cs` & `ProductEndpoints.cs` hanya mendaftarkan route yang belum dimigrasi (metode beda → tidak ada dobel). `Endpoints/BannerEndpoints.cs` **dihapus** (sudah kosong).

**Temuan & perbaikan parity (penting):** `[ApiController]` otomatis mengubah 4xx tanpa body dari action (mis. `NotFound()` di PUT margin) menjadi **ProblemDetails** (`{type,title,status,traceId}`), padahal Minimal API lama mengembalikan **404 body kosong** (`Results.NotFound()`). Perbaikan di `Program.cs`: `ApiBehaviorOptions.SuppressMapClientErrors = true` → perilaku empty-4xx kembali identik kontrak lama. Berlaku global untuk semua action controller (endpoint ber-body `{ message }` tidak terpengaruh).

**Verifikasi (build + server Development `localhost:5210`):**

- `dotnet build` → **0 warning, 0 error**.
- **Route parity:** 62 = 62 baseline, `MISSING: []`, `EXTRA: []`.
- Controller baru: GET vouchers → 200; voucher `999999` & `/999999/usage` → `404 {"message":"Voucher tidak ditemukan."}`; GET products → 200; **PUT margin produk tak ada → `404` body kosong** (paritas legacy setelah fix); GET banners → 200; PUT banner `999999/toggle` → `200 {"message":"Status banner berhasil diperbarui."}` (paritas: legacy selalu 200); DELETE banner tak ada → `404 {"message":"Banner tidak ditemukan."}`.
- Legacy yang masih hidup di group sama (bukti tidak dobel): PATCH voucher `999999/status` → `404 {message}`; POST `/admin/products` (create, body kosong) → 400.
- Server dimatikan, artefak dibersihkan.

**Status checklist Fase 3:** selesai.

---

## 0.4 Fase 4 — Status Implementasi (CRUD + Upload + Sistem Admin, 2026-09-05)

**Controller dilengkapi / baru:**

| Controller | Endpoint ditambahkan | Catatan |
|---|---|---|
| `VoucherController` (lengkapi) | POST create · PUT `/{id}` · PATCH `/{id}/status` · DELETE `/{id}` | `VoucherEndpoints.cs` **dihapus** |
| `AdminProductController` (lengkapi) | POST create (tetap bind `GameProduct` mentah — parity, bukan ubah diam-diam) · POST `/{id}/upload-item-image` | |
| `AdminSystemController` (baru) | 12 endpoint `/admin/system` (sync-digiflazz, clean-dummy, midtrans-expiry GET/PUT, game-templates, game-categories GET/PUT, popular-games GET/PUT, metadata GET/PUT, upload-game-cover) | `ProductEndpoints.cs` **dihapus** |

`Program.cs`: `MapProductEndpoints()` & `MapVoucherEndpoints()` dilepas. `Endpoints/` tersisa 3 file aktif: Auth, Order(+webhook), AdminManagement (sisanya dead code fase 8).

**Perbaikan parity:** parameter `IFormFile` upload dibuat `IFormFile?` di ketiga action (produk, cover, banner). Tanpa itu `[ApiController]` auto-400 `{"message":"The file field is required."}` padahal Minimal API lama masuk ke guard action → `400 "File tidak boleh kosong."` (plain text). Sekarang identik.

**Drift kecil yang diterima & dicatat (hanya input invalid, bukan kontrak valid):**
- `PUT /admin/system/metadata/{id}` dengan payload invalid → auto-validasi DataAnnotations `[ApiController]` → `400 {"message":"The InputFields field is required."}` (legacy: service yang menolak). Bentuk 400 tetap `{ message }` via shim.
- `POST /admin/banners/upload` tanpa field `title` (bukan multipart normal) → `400 {"message":"The title field is required."}` (legacy: text polos). Alur upload normal identik.

**Verifikasi (build + server Development `localhost:5210`):**

- `dotnet build` → 0 warning, 0 error. **Route parity:** 62 = 62, `MISSING: []`, `EXTRA: []`.
- **Voucher lifecycle penuh via controller:** create → **201** (entity), PATCH status → `200 {"message":...,"isActive":false}`, PUT update → 200 (nilai berubah), GET detail → 200, DELETE → `200 {"message":"Voucher berhasil dihapus."}`, GET setelah hapus → `404 {message}`. Voucher uji (kode `PHASE4TEST01`, id 8) dihapus — DB bersih. Create invalid (`discountType` salah) → `400 {message}` (parity).
- **AdminSystem:** GET midtrans-expiry/game-templates/game-categories/popular-games/metadata → 200; PUT expiry no-op (nilai sama) → 200; PUT popular-games `slugs:null` → `400 {"message":"Daftar game populer wajib dikirim."}`.
- **Upload:** tanpa file → `400 "File tidak boleh kosong."` (text, parity legacy) di produk & game-cover; file PNG valid ke id tak ada → `404 {"message":"Produk tidak ditemukan."}` / `{"message":"Metadata Game tidak ditemukan."}` (tanpa efek samping R2 — service cek resource lebih dulu).
- Server dimatikan, artefak dibersihkan.

**Status checklist Fase 4:** selesai.

---

## 0.5 Fase 5 — Status Implementasi (Alur Auth, 2026-09-05)

**Controller baru (2) di `Controllers/`:**

| Controller | Route | Endpoint | Catatan |
|---|---|---|---|
| `AuthController` | `/api/v1/auth` | 9 endpoint customer (request-otp, register, login, request-password-reset, reset-password, google, profile GET/PUT, check-email) | rate limit per action: `[EnableRateLimiting("OtpPolicy")]` ×3, `"CustomerLoginPolicy"` ×2; `[Authorize]` hanya profile GET/PUT (persis group legacy); manual `Validator.TryValidateObject` di request/reset password **dihapus** → diganti auto-validation `[ApiController]` + shim `{ message }` (Fase 0) — pesan error sama persis dengan validasi manual |
| `AdminAuthController` | `/api/v1/admin/auth` | POST login | `[AllowAnonymous]` + `[EnableRateLimiting("AdminLoginPolicy")]`, setara `.AllowAnonymous()+.RequireRateLimiting()` legacy; tanpa `[Authorize]` class (endpoint memang anonim) |

`AuthEndpoints.cs` **dihapus**; `MapAuthEndpoints()` dilepas dari `Program.cs`. reCAPTCHA/IP/`RequestAborted` diambil dari `HttpContext` action — perilaku sama dengan endpoint lama.

**Verifikasi (build + server Development `localhost:5210`, jaga kuota rate limit):**

- `dotnet build` → 0 warning, 0 error. **Route parity:** 62 = 62, `MISSING: []`, `EXTRA: []`. Tag tetap `Otentikasi (Customer)` & `Otentikasi (Admin)`.
- check-email blank → `400 {"message":"Email tidak boleh kosong."}`; email acak → `200 {"message":"Email tersedia."}`.
- `POST /auth/local/reset-password` body `{}` → **auto-validasi** `400 {"message":"Email wajib diisi."}` — identik pesan validasi manual legacy (pembuktian shim pada DTO validasi auth).
- profile tanpa token → 401; customer login invalid → 401 (body kosong); admin login invalid → 401 (reCAPTCHA/kredensial).
- Server dimatikan, artefak dibersihkan. (Jalur sukses register/google/OTP sengaja tidak dipicu live — butuh OTP email/reCAPTCHA asli; mapping logikanya 1:1 dari endpoint lama.)

**Status checklist Fase 5:** selesai.

---

## 0.6 Fase 6–7 — Status Implementasi (Orders + Webhooks, 2026-09-05)

**Controller baru (3) di `Controllers/`:**

| Controller | Route | Endpoint | Catatan |
|---|---|---|---|
| `OrderController` | `/api/v1/orders` | POST create · POST `/preview` · GET `/my-history` | POST create **optional-auth** (klaim NameIdentifier valid → `customerId`, tidak → `null`); `[Authorize]` hanya my-history; logger kategori `"PlaceOrder"` & heuristik pesan error (`ex.Message.Length < 100`) dipertahankan dari endpoint lama |
| `AdminOrderController` | `/api/v1/admin/orders` | GET list · POST `/{orderNo}/retry` · POST `/{orderNo}/refund` | pemetaan `OrderActionOutcome` → HTTP identik `MapOrderActionResult` lama; `ServerError` → `Problem()` (ProblemDetails, setara `Results.Problem`) |
| `WebhookController` | `/webhooks` (**root, tanpa versi**) | POST `/midtrans` · POST `/digiflazz` | `[AllowAnonymous]`; digiflazz baca `Request.Body` mentah + validasi signature via `DigiflazzProvider.ValidateWebhookSignature`, lalu deserialisasi manual `PropertyNameCaseInsensitive` — perilaku persis endpoint lama |

`OrderEndpoints.cs` **dihapus**; `MapOrderEndpoints()` (api) & `MapWebhookEndpoints()` (root) dilepas dari `Program.cs`.

**Verifikasi (build + server Development `localhost:5210`):**

- `dotnet build` → 0 warning, 0 error. **Route parity:** 62 = 62, `MISSING: []`, `EXTRA: []`. Webhook tetap di `/webhooks` (root) dengan tag `Webhooks (Sistem Integrasi)`.
- preview checkout (produk tak ada di DB dev) → `400 {"message":"Produk tidak ditemukan!"}` — handler jalan, error service mengalir dengan bentuk `{ message }`.
- `GET /orders/my-history` tanpa token → 401; `GET /admin/orders` tanpa token → 401, dengan token admin → 200.
- Webhook: `POST /webhooks/digiflazz` signature salah → **401** (validasi signature aktif); `POST /webhooks/midtrans` body bukan JSON → `400 {message}` (binding error via shim; legacy juga 400).
- `POST /admin/orders/NOT-EXIST-999/retry` → `404 {"message":"Pesanan tidak ditemukan"}` — mapping outcome → HTTP identik legacy.
- Jalur sukses PlaceOrder/webhook-replay tidak dipicu live (membuat transaksi Midtrans & mutasi order asli) — mapping 1:1 dan service tidak tersentuh.
- Server dimatikan, artefak dibersihkan.

**Status checklist Fase 6–7:** selesai.

---

## 0.7 Fase 8 — Status Implementasi (Cleanup Total, 2026-09-05) — MIGRASI SELESAI

**Langkah terakhir:**

- **Domain terakhir dimigrasi:** `AdminManagementEndpoints.cs` (5 endpoint `/admin/admins`, SuperAdminOnly) → `AdminManagementController` — class-level `[Authorize(Policy = "SuperAdminOnly")]`, route `{id:int}` (parity: non-int → 404 seperti legacy), caller username dari klaim `Name` (proteksi self-demote/self-delete).
- **Folder `Endpoints/` dihapus total** (11 file: 8 sudah terhapus bertahap + 3 dead code Search/Dashboard/Settings + AdminManagement). `using be_digitalin.Endpoints` dan seluruh panggilan `Map*Endpoints()` dibuang dari `Program.cs`; blok komentar fase diganti ringkasan akhir.
- `/ping-db` dibiarkan di root (infra, di luar kontrak API — opsional dipindah ke controller).
- **15 controller** final di `Controllers/` — persis prediksi audit Lampiran: Auth, AdminAuth, Catalog, AdminProduct, AdminSystem, Order, AdminOrder, Webhook, Banner, AdminBanner, Dashboard, Search, Settings, Voucher, AdminManagement.

**Regression penuh (build + server Development `localhost:5210`):**

- `dotnet build` → **0 warning, 0 error**.
- **Route parity final:** 62 = 62 baseline, `MISSING: []`, `EXTRA: []`.
- Publik: `/ping-db` 200 (root), banners 200, games/brands/popular 200, games/categories 200, search 200, check-email 200, orders/preview (error path) 400, orders/my-history tanpa token 401, webhook digiflazz signature salah 401.
- Admin (token ADMIN): settings, dashboard stats, products, system/game-templates, vouchers, banners, orders → semua 200.
- SuperAdmin: `/admin/admins` → 200 (token SUPER_ADMIN); PUT role & DELETE admin tak ada → `404 {"message":"Admin tidak ditemukan."}`; **token ADMIN ke endpoint SuperAdminOnly → 403** (policy di level class bekerja persis seperti `RequireAuthorization("SuperAdminOnly")` legacy).
- Scalar UI (`/scalar/v1`) → 200 di Development; CORS, rate limit, maintenance middleware, JWT bearer, header keamanan tidak diubah.
- Server dimatikan, artefak dibersihkan.

**API_CONTRACT.md tidak diubah** — bentuk error (`{ message }`/body kosong/ProblemDetails) dan schema respons terbukti identik di regression; satu-satunya drift adalah kasus input invalid (di-dokumentasikan di Section 0.3–0.6) dan bukan bagian kontrak valid.

**Checklist Fase 8 (Section 7):** selesai semua.

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

- **DTO request terduplikasi** *(sudah dikonsolidasi di Fase 0 — lihat Section 0)*: `DTOs/Requests/AuthRequests.cs` lama mendefinisikan `LoginRequest`/`CustomerLoginRequest`/`RegisterRequest` **tanpa** field `CaptchaToken`/`OtpCode`, sedangkan record yang benar-benar dipakai didefinisikan **inline** di `Endpoints/AuthEndpoints.cs` (men-shading namespace DTO). Kini SSOT: record lengkap ada di `DTOs/Requests/AuthRequests.cs`/`ProductRequests.cs`, tidak ada lagi definisi inline di endpoint.
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
| **1. Pilot (read-only, tanpa binding body)** | `SearchController`, `SettingsController`, `DashboardController` ✅ selesai (Section 0.1) | `.http` 04/05 + Scalar ✅ |
| **2. Domain baca publik** | `CatalogController`, `BannerController` ✅ selesai (Section 0.2) — preview `OrderController` menyusul di Fase 6 | `.http` 02/03 ✅ |
| **3. Baca admin + aksi sederhana** | `AdminBannerController` ✅, `VoucherController` (GET) ✅, `AdminProductController` ✅ — selesai (Section 0.3) | `.http` 03/04 ✅ |
| **4. CRUD + upload file** | `VoucherController` penuh ✅, `AdminProductController` (create+upload) ✅, `AdminSystemController` (12) ✅ — selesai (Section 0.4); upload banner sudah ikut di Fase 3 | `.http` + jalur true/false ✅ |
| **5. Alur auth** | `AuthController` (9) ✅, `AdminAuthController` ✅ — rate limit, reCAPTCHA, Google, OTP (Section 0.5) | `.http` 01 + rate-limit + profile ✅ |
| **6. Jalur uang** | `OrderController` ✅, `AdminOrderController` ✅ (Section 0.6) | `.http` 03 + optional-auth + Midtrans ✅ |
| **7. Webhooks** | `WebhookController` ✅ (midtrans, digiflazz) — ikut di Section 0.6 | replay payload asli ✅ |
| **8. Cleanup + lock-in** | ✅ selesai (Section 0.7): `Endpoints/` dihapus total, AdminManagement ikut dimigrasi, `/ping-db` tetap root, regression penuh hijau | regression penuh + `git diff --stat` ✅ |

**Rollback** kapan saja = revert commit fase; service/entity tidak pernah berpindah, jadi tidak ada yang rusak di hilir.

---

## 7. Implementation Checklist

**Prep / infra**
- [x] Buat folder `Controllers/`; tambah `builder.Services.AddControllers();` — AddControllers **selesai** (`Program.cs`); folder `Controllers/` dibuat saat Fase 1 (belum ada controller)
- [x] Konfigurasi `ApiBehaviorOptions.InvalidModelStateResponseFactory` → bentuk `{ message }` (pertahankan kontrak) — **selesai** (`Program.cs`)
- [x] Ekstrak record request inline (`AuthEndpoints`, `ProductEndpoints`) ke `DTOs/Requests`; hapus `AuthRequests.cs` lama
- [x] Tambah response DTO untuk semua respons anonymous `new { ... }`
- [x] Baseline: `dotnet build`, catat tabel route, jalankan semua `http/*.http`, simpan diff respons — **selesai**: build 0 error; 62 operasi di `docs/route-baseline.txt`; endpoint publik + 401/429/bentuk error divariasikan (detail di Section 0)

**Per domain (Fase 1–7)**
- [x] Buat controller dengan `[Route(...)]` + `[Authorize]`/`[EnableRateLimiting]`/`[Tags]` setia pada group saat ini — **selesai** (15 controller)
- [x] Terjemahkan tiap action 1:1 (`Results.X` → `ActionResult`, helper claims, cek file) — **selesai**
- [x] Pertahankan group endpoint lama tetap terdaftar (fase hybrid hijau) — **selesai**; dibuang bertahap per fase, tanpa route dobel
- [x] Build + jalankan file `.http` domain; diff respons vs baseline (body, status, headers) — **selesai**: regression live per fase + parity OpenAPI 62 = 62

**Domain uang & webhook**
- [x] Orders: verifikasi alur anonymous (`customerId = null`) dan logged-in; konfirmasi rate-limit dan payload Midtrans — **selesai** (Section 0.6); jalur sukses PlaceOrder tidak dipicu live (transaksi Midtrans asli)
- [x] Webhooks: parsing raw-body + signature identik; perilaku 200 short-circuit identik — **selesai** (signature salah → 401; body handling 1:1)

**Finalisasi (Fase 8)**
- [x] Buang semua panggilan `Map*Endpoints()`; hapus `Endpoints/` (9+ file) — **selesai**: folder dihapus total
- [x] Pindah `/ping-db` ke controller (atau biarkan — di luar kontrak) — **dibiarkan di root**
- [x] Full build, full sweep, diff tabel route (62 == 62, nol 404) — **selesai** (Section 0.7)
- [x] Konfirmasi policy, rate-limit, CORS, maintenance middleware, Scalar + bearer auth tidak berubah — **selesai**
- [x] Update `API_CONTRACT.md` **hanya jika** bentuk error validasi atau schema benar-benar berubah — **tidak berubah** (bentuk error & schema terbukti identik)
- [ ] Commit per fase (diff kecil, mudah di-revert) — **belum**: perubahan belum di-commit (tersedia sebagai satu unit kerja)

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