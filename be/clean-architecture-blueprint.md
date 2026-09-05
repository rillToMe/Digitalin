# Clean Architecture Migration Blueprint

> Audit berbasis codebase aktual (September 2026). **Belum ada kode yang diubah.**
> Prasyarat: migrasi Minimal API → Controllers sudah selesai (15 controller, `Endpoints/` dihapus, parity 62/62).
> Target dependency: **API → Application → Domain** dan **Infrastructure → Application/Domain**. Domain **tidak** boleh bergantung pada API, EF Core, Redis, atau provider eksternal.

---

## 1. Current Architecture

Single project `be_digitalin` (net10.0, satu csproj, satu assembly), 153 file `.cs` termasuk Migrations. Struktur folder:

| Folder | Isi | Jumlah | Peran aktual |
|---|---|---|---|
| `Controllers/` | 15 controller | 15 | API layer (thin) |
| `Services/v1/` | 15 interface + 14 impl + GameTemplates (3) + GameCategoryRegistry | 36 | Application layer — **campuran use-case + data access + IO eksternal** |
| `DTOs/` | Requests (12) + Responses (16) | 28 | Application contracts (bersih) |
| `Domain/` | Pricing, Slug, Orders/OrderStateTransition | 3 | Domain (seeds yang sudah benar) |
| `Models/` | 10 entity POCO murni | 10 | Persistence entities — **tanpa dependency EF** (hanya `[Key]` DataAnnotations; Fluent config di DbContext) |
| `Data/` | DigitalinDbContext + Migrations | 27 | Infrastructure |
| `Providers/` | Digiflazz, Midtrans, Telegram, CloudflareR2, Email, Google, Recaptcha | 7 | Infrastructure (adapter eksternal) — **semua class concrete, tanpa interface** |
| `Infrastructure/` | Redis (3), OpenApi (1), Serialization (1), Storage (1) | 6 | Infrastructure |
| `Workers/` | OrderSweepWorker, DigiflazzSyncWorker (dikomentari) | 2 | Infrastructure (hosted services) — di root, bukan di `Infrastructure/` |
| `Middleware/` | MaintenanceMiddleware | 1 | API/Infrastructure |
| `Config/` | 8 settings POCO | 8 | Infrastructure |
| `Constants/` | AppConstants, OtpPurpose | 2 | Shared kernel (dipakai semua layer) |
| `Program.cs` | wiring | 1 | Composition root |

### Hasil audit per lapisan

**Controllers — sudah thin ✅ (tidak perlu refactor).**
- `AuthController`, `OrderController`, `AdminSystemController`, `CatalogController` diverifikasi: hanya orkestrasi (ekstraksi klaim, mapping hasil → status HTTP, projection kecil).
- Satu-satunya "logika" di controller: clamp pagination `CatalogController` (max 60), projection `GameTemplateFactory.All → GameTemplateResponse` — presentasi, bukan business rule.
- **3 controller masih menyentuh `Models` langsung**: `BannerController`/`AdminBannerController` (return entity `Banner`), `AdminProductController` (bind raw `GameProduct` — keputusan parity Fase 4 yang didokumentasikan).

**Services — fat, dan terikat langsung ke semua infrastruktur ❌.**

## 2. Dependency Problems (pelanggaran target)

Diverifikasi via grep `using be_digitalin.*` + field ctor:

### P1. Application → EF Core (14 service)
Semua service implementation menyuntik `DigitalinDbContext` concrete langsung: OrderService, CheckoutService, BannerService, CustomerService, VoucherService, GoogleAuthService, SearchService, ProductService, AdminManagementService, DashboardService, OtpService, AdminAuthService, PasswordResetService, AppSettingsService.

### P2. Application → provider eksternal concrete (8 service)
| Service | Provider concrete |
|---|---|
| OrderService | `MidtransProvider`, `DigiflazzProvider`, `TelegramProvider` |
| ProductService | `CloudflareR2Provider`, `DigiflazzProvider` |
| BannerService | `CloudflareR2Provider` |
| CustomerService | `EmailProvider` |
| PasswordResetService | `EmailProvider` |
| GoogleAuthService | `GoogleProvider` |
| RecaptchaService | `RecaptchaProvider` |
| DashboardService | `DigiflazzProvider` |

### P3. Application → namespace `Infrastructure.*` (5 service)
- `ProductService`, `CustomerService` → `Infrastructure.Redis` (`IRedisService`, `RedisCacheKeys`)
- `OrderService`, `ProductService` → `Infrastructure.Serialization` (`GameInputSchemaSerializer`)
- `ProductService`, `BannerService` → `Infrastructure.Storage` (`ImageFileValidator`)

### P4. Application menerima tipe API layer
`ProductService.UpdateGameCoverAsync` / `UpdateProductIconAsync` dan `BannerService.UploadBannerAsync` menerima `IFormFile` (Microsoft.AspNetCore.Http) — Application bergantung pada ASP.NET.

### P5. Infra concern di Application
- `TokenService` (JWT signing) ada di `Services/v1/Auth/`.
- `AppSettingsService` (DB read + memory cache) ada di Application tapi secara peran adalah infra (middleware memakainya via `IDbContextFactory<DigitalinDbContext>`).

### P6. Kosmetik
- `Workers/` di root, bukan `Infrastructure/`.
- Namespace service tidak konsisten: file di `Services/v1/Admin` bernamespace `be_digitalin.Services.Admin`, `Services/v1/Auth` → `be_digitalin.Services.Auth`, dst. (folder `v1` tidak tercermin di namespace).
- Record domain (`GameCategoryEntry`) dan `GameCategoryRegistry` tinggal di `Services/v1/Games/` — bukan `Domain/`.
- `Strukture.md` basi (masih menyebut `Endpoints/` dan PostgreSQL).

### Yang SUDAH benar (tidak disentuh)
- Controllers → hanya interface service. ✅
- DTOs murni kontrak, tanpa EF. ✅
- `Domain/` seeds (Pricing, Slug, OrderStateTransition) murni, tanpa IO. ✅
- Providers/Config/Data/Infrastructure bersih dari logika bisnis. ✅
- `OrderStateTransition` → `Constants` (shared kernel, bukan EF/Redis/provider — **kompatibel dengan target**).
- Middleware → `IAppSettingsService` (interface). ✅
- Workers → `IServiceScopeFactory` + `IOrderService` (interface). ✅

---

## 3. Target Architecture

**Keputusan: satu project, folder + namespace sebagai lapisan** (bukan split multi-csproj). Alasan di Final Recommendation.

```
be_digitalin/
├── Controllers/            # API   (15, tidak berubah isi)
├── Middleware/             # API
├── Program.cs              # Composition root
│
├── Application/            # (praktis: Services/ + DTOs/ — dipertahankan)
│   ├── Ports/              # BARU: IDigiflazzProvider, IMidtransProvider, ITelegramProvider,
│   │                       #       ICloudflareR2Provider, IEmailProvider, IGoogleProvider,
│   │                       #       IRecaptchaProvider, ITempStateStore, IApplicationDbContext
│   └── Services/           # use-case; hanya bergantung Ports + Domain + DTOs
│
├── Domain/                 # + Models/ (entity POCO), GameTemplate*, GameCategoryRegistry,
│   │                       #   OrderTargetRenderer, VoucherRules, GameInputSchemaSerializer
│   └── Entities/           # (Models dipindah, namespace be_digitalin.Domain.Entities)
│
└── Infrastructure/         # + Providers (implement Ports), TokenService, AppSettingsService,
    └── ...                 #   Workers/, Redis/ (impl saja), Storage/, Serialization/ (sisanya)
```

### Peta pemetaan

| Bagian | Layer | Aksi |
|---|---|---|
| Controllers (15) | API | **Tetap** (sudah thin) |
| Middleware | API | Tetap |
| DTOs (28) | Application | **Tetap** |
| Services/v1 (36) | Application | **Ubah**: ctor swap concrete → port |
| Domain/ (3) | Domain | Tetap |
| Models/ (10) | Domain/Entities | **Pindah** (namespace) |
| Providers/ (7) | Infrastructure | **Ubah**: implementasi port baru |
| Data/ (DbContext) | Infrastructure | Tetap (+ implement `IApplicationDbContext`) |
| Infrastructure/ (6) | Infrastructure | **Ubah**: interface Redis pindah ke Ports; serializer pindah ke Domain; ImageFileValidator dibungkus port |
| Workers/ (2) | Infrastructure | Pindah ke `Infrastructure/Workers` |
| TokenService, AppSettingsService | Infrastructure | Pindah |
| Config/ (8) | Infrastructure | Tetap |
| Constants/ | Shared kernel | Tetap (kompatibel target) |

---

## 4. Files to Move

| # | File | Dari → Ke | Alasan |
|---|---|---|---|
| 1 | `Models/*.cs` (10) | `Models/` → `Domain/Entities/`, namespace `be_digitalin.Domain.Entities` | Entity murni tanpa EF deps — aman jadi domain entity |
| 2 | `GameTemplate.cs`, `GameTemplateType.cs`, `GameTemplateFactory.cs` (3) | `Services/v1/Games/GameTemplates/` → `Domain/Games/GameTemplates/` | Logika murni (preset registry), tanpa IO |
| 3 | `GameCategoryRegistry.cs` (1) | `Services/v1/Games/` → `Domain/Games/` | Logika murni (registry + validasi), tanpa DB |
| 4 | `TokenService.cs` (1) | `Services/v1/Auth/` → `Infrastructure/Security/` | JWT signing = mekanisme infra; `ITokenService` tetap di Application |
| 5 | `AppSettingsService.cs` (1) | `Services/v1/Settings/` → `Infrastructure/Settings/` | DB read + memory cache = infra; `IAppSettingsService` tetap di Application |
| 6 | `OrderSweepWorker.cs`, `DigiflazzSyncWorker.cs` (2) | `Workers/` → `Infrastructure/Workers/` | Hosted service = infra |
| 7 | `IRedisService.cs`, `RedisCacheKeys.cs` (2) | `Infrastructure/Redis/` → Application `Ports/` | Kontrak Application (opsional rename `ITempStateStore`) |
| 8 | `GameInputSchemaSerializer.cs` (1) | `Infrastructure/Serialization/` → `Domain/` | Serialisasi struktur domain (GameInputField), murni BCL |

**Total move: ~21 file.** Semua namespace change → update referensi (~25 file menyentuh `Models`, 3 menyentuh `GameTemplates`, dst.).

## 5. Files to Modify

| # | File | Perubahan |
|---|---|---|
| 1 | 14 service impl | Ctor: `DigitalinDbContext` → `IApplicationDbContext`; provider concrete → port interface; `IRedisService` → port; update namespace `Models` |
| 2 | 7 Providers | Implementasi interface port (tambah `: IDigiflazzProvider` dst. — body tidak berubah) |
| 3 | `DigitalinDbContext.cs` | Tambah `: IApplicationDbContext` |
| 4 | `OrderService.cs` | Delegasi 3 method statis murni (`BuildTargetValue`, `ValidateTargetValues`, `RenderTargetTemplate`) → `Domain/Orders/OrderTargetRenderer` |
| 5 | `VoucherService.cs` | Delegasi logika murni (`ComputeStatus`, `ValidatePayload`, `CalculateDiscount`) → `Domain/Commerce/VoucherRules` |
| 6 | `ProductService.cs`, `BannerService.cs` | `IFormFile` → kontrak file Application (`ImageFileData` record) + validasi via port `IImageValidator` |
| 7 | `Program.cs` | DI: `AddHttpClient<IDigiflazzProvider, DigiflazzProvider>` dst.; `AddScoped<IApplicationDbContext, DigitalinDbContext>`; registrasi workers; binding port |
| 8 | `Strukture.md`, `AGENTS.md` | Dokumentasi arsitektur baru |
| 9 | `clean-architecture-blueprint.md` | Checklist per fase (setelah implementasi) |

## 6. Files to Create

| # | File baru | Layer |
|---|---|---|
| 1 | `Ports/IDigiflazzProvider.cs` | Application |
| 2 | `Ports/IMidtransProvider.cs` | Application |
| 3 | `Ports/ITelegramProvider.cs` | Application |
| 4 | `Ports/ICloudflareR2Provider.cs` | Application |
| 5 | `Ports/IEmailProvider.cs` | Application |
| 6 | `Ports/IGoogleProvider.cs` | Application |
| 7 | `Ports/IRecaptchaProvider.cs` | Application |
| 8 | `Ports/IApplicationDbContext.cs` | Application — DbSet per entity + `SaveChangesAsync` + `Database` (untuk transaction) |
| 9 | `Domain/Orders/OrderTargetRenderer.cs` | Domain — ekstraksi dari OrderService |
| 10 | `Domain/Commerce/VoucherRules.cs` | Domain — ekstraksi dari VoucherService |

Opsi hardening (fase lanjut, bukan wajib): `BannerResponse` DTO + `CreateGameProductRequest` DTO untuk lepas dari `Models` di 3 controller (JSON shape identik → kontrak aman), `IImageValidator` port.

**Total create: 10–13 file.**

## 7. Files to Delete

**Tidak ada.** Semua perubahan adalah move/edit; tidak ada file yang hilang fungsinya (pola yang sama dengan migrasi controller: 0 API berubah).

## 8. What Must Stay Unchanged

- **Database schema** — Migrations (27 file) tidak disentuh; Fluent config DbContext tetap.
- **API contract** — 62 operasi, path, tag OpenAPI, bentuk error (`{message}`, 4xx kosong, ProblemDetails, 429 rate-limit) — `API_CONTRACT.md` tidak berubah.
- **Controllers** — isi action tidak berubah (hanya mungkin namespace using jika Models pindah).
- **DTOs** — tidak berubah sama sekali.
- **Domain/ (3 seeds)** — tidak berubah.
- **Middleware behavior, CORS, rate limiting, JWT auth, maintenance mode** — tidak berubah.
- **`Domain/OrderStateTransition` → `Constants`** — dipertahankan (shared kernel kompatibel target).
- **`Program.cs`** — hanya registrasi DI berubah; pipeline (`UseRateLimiter` → `UseAuthentication` → `UseAuthorization` → `UseMaintenanceMode` → `MapControllers`) identik.
- **`.http` scripts, appsettings, Docker, `docs/route-baseline.txt`** — tidak berubah.
- **Perilaku bisnis**: transisi state order, perhitungan harga, reservasi voucher, guard nominal webhook, sweep bot — semua 1:1 (hanya pindah lokasi).

## 9. Migration Scope (estimasi)

| Metrik | Jumlah |
|---|---|
| File dipindah | ~21 |
| File diubah | ~25–30 |
| File dibuat | 10–13 |
| File dihapus | 0 |
| **Total sentuh** | **~55–65 dari 153 file** (sisanya ~90 file: DTOs, Controllers, Migrations, Config, .http — tak tersentuh) |
| Endpoint berubah | 0 |
| Database berubah | 0 |

## 10. Risks

| Risiko | Level | Mitigasi |
|---|---|---|
| **Behavioral drift** saat service ganti dependency (port/delegasi tidak 1:1) | Tinggi | Aturan keras: port = delegasi murni, TIDAK ada perubahan logika dalam commit yang sama; gate per fase: `dotnet build` 0/0 + smoke parity 62 route + error shape |
| **OrderService** — file paling kompleks (webhook, sweep, retry, refund, concurrency) | Tinggi | Dimigrasi paling akhir, di fase terpisah, dengan smoke khusus (sweep/retry path) |
| Churn namespace `Models` → `Domain.Entities` (~25 file referensi) | Sedang | Fase terisolasi, compiler-guided, tanpa perubahan logika |
| Salah daftarkan DI (concrete vs interface, `AddHttpClient<TImpl>` vs `<TInterface, TImpl>`) | Rendah | Compile-time + smoke 401/200 per domain |
| Single project = **tidak ada enforcement compile-time** arah dependency | Sedang | Aturan tertulis di AGENTS.md + review checklist; opsi lanjut: analyzer rule atau test project (dibatalkan sebelumnya — bisa dihidupkan lagi) |
| `IFormFile` di Application (P4) — kontrak file baru | Sedang | Kontrak `ImageFileData` + mapping di controller; diuji via endpoint upload |
| `IApplicationDbContext` expose `Database` (untuk transaction) — sedikit "bocor" | Rendah | Diterima sebagai pragmatisme; repository penuh hanya jika kompleksitas order menuntut |

## 11. Migration Phases (incremental, bottom-up)

Verifikasi tiap fase: `dotnet build` → 0 warning/error + parity 62/62 (workflow yang sudah terbukti di migrasi controller).

| Fase | Isi | File | Runtime impact |
|---|---|---|---|
| **A. Domain hardening** | Ekstrak `OrderTargetRenderer` + `VoucherRules` dari service; pindah `GameTemplate*`, `GameCategoryRegistry`, `GameInputSchemaSerializer` ke `Domain`; service delegate | ~8 | Nol (delegasi murni) |
| **B. Ports** | Buat 10 interface port di Application | +10 | Nol (belum dipakai) |
| **C. Adapters & rewiring** | 7 provider implement port; DbContext implement `IApplicationDbContext`; 14 service swap ctor; `Program.cs` DI. **Urutan per domain: Settings/Auth → Content/Catalog → Voucher → System/Admin → Orders terakhir** | ~25 | Nol (1:1 delegasi) |
| **D. Pemisahan infra** | Pindah `TokenService`, `AppSettingsService`, `Workers/`, `IRedisService`/`RedisCacheKeys` → port; kontrak file upload | ~12 | Nol |
| **E. Normalisasi & docs** | `Models/` → `Domain/Entities` (namespace + ~25 referensi); opsi hardening DTO banner/product; update `Strukture.md`, `AGENTS.md`, checklist ini | ~30 | Nol |

Estimasi total: **5 fase, ~55–65 file**, tanpa perubahan API/database/behavior. Tiap fase bisa di-commit & di-deploy sendiri.

## 12. Final Recommendation

1. **Lakukan — tapi incremental, 5 fase (A→E), bukan big-bang.** Alasan: seluruh risiko adalah refactor murni (0 perubahan kontrak), dan codebase sudah punya gate parity 62/62 yang terbukti menangkap drift di migrasi sebelumnya. Big-bang hanya layak jika targetnya multi-csproj — yang justru menaikkan risiko tanpa benefit untuk tim 1 kontributor.

2. **Tetap satu project (folder-based), jangan split csproj.** Alasan: 153 file di satu deployable; split berarti churn namespace + referensi project di SEMUA file; enforcement compile-time bisa didapat belakangan via analyzer/test — nilai yang sama dengan biaya jauh lebih rendah. Jika suatu saat mau split: `be_digitalin.Domain` dulu (paling murni), lalu `be_digitalin.Application`.

3. **Prioritas nilai tertinggi = Fase A + B + C** (ports + adapters + rewiring): itu yang menghilangkan pelanggaran P1–P3 — Application tidak lagi menyentuh EF Core/Redis/provider concrete. Fase D/E kosmetik + normalisasi; boleh ditunda.

4. **OrderService tetap service fat** (per pola project: Fat Service / Thin Controller) — Clean Architecture tidak menuntut service tipis, hanya dependency direction yang benar.

5. **`Constants/AppConstants` dipertahankan sebagai shared kernel** — target dependency tidak melarangnya (bukan API/EF/Redis/provider), dan memecahnya ke 3 layer = churn besar tanpa nilai perilaku. Ditandai sebagai opsi hardening di masa depan.