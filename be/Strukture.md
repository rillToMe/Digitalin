```text
be_digitalin/                          # Satu project .NET (Controller-Based + Clean Architecture)
├── Controllers/                       # API layer — 15 controller, thin (hanya orkestrasi)
├── Middleware/                        # MaintenanceMiddleware (API)
├── Program.cs                         # Composition root (DI wiring)
│
├── Application/                       # Layer Application (kontrak + helper use-case)
│   ├── Ports/                         # Interface: 7 provider eksternal, IApplicationDbContext,
│   │                                  #           IRedisService, RedisCacheKeys
│   ├── Serialization/                 # GameInputSchemaSerializer (helper teknis JSON)
│   └── Validation/                    # ImageFileData (kontrak file) + ImageFileValidator
│
├── Services/                          # Application use-case (Fat Services, diakses lewat interface)
│   └── v1/                            # Admin, Auth, Commerce, Content, Customer, Games,
│                                      # Orders, Security, Settings — hanya interface + impl service
│
├── Domain/                            # Layer Domain — murni, tanpa IO/EF/provider
│   ├── Entities/                      # Entity POCO (dulu Models/)
│   ├── Orders/                        # OrderStateTransition, OrderTargetRenderer
│   ├── Commerce/                      # VoucherRules
│   ├── Games/GameTemplates/           # GameTemplate*, GameTemplateFactory
│   └── Pricing.cs, Slug.cs            # Seeds domain
│
├── Infrastructure/                    # Layer Infrastructure (implementasi teknis)
│   ├── Redis/                         # RedisService (impl IRedisService)
│   ├── Security/                      # TokenService (JWT)
│   ├── Settings/                      # AppSettingsService (DB + memory cache)
│   ├── Workers/                       # OrderSweepWorker, DigiflazzSyncWorker
│   └── OpenApi/                       # Konfigurasi OpenAPI/Swagger
│
├── Data/                              # DigitalinDbContext (impl IApplicationDbContext) + Migrations
├── Providers/                         # Adapter eksternal — implement Ports:
│                                      #   Digiflazz, Midtrans, Telegram, CloudflareR2,
│                                      #   Email, Google, Recaptcha
├── DTOs/                              # Requests/ + Responses/ (kontrak API, murni)
├── Config/                            # Settings POCO (appsettings binding)
├── Constants/                         # Shared kernel (AppConstants, OtpPurpose) — lintas layer
├── http/, Icons/                      # Test script HTTP & aset lokal
└── appsettings.json, Program.cs
```

### Rincian Fungsi Tiap Folder

**1. `Controllers/` (API)**
15 controller endpoint, semuanya thin: hanya memetakan request → service interface → response HTTP.
Tidak boleh berisi business logic. Satu-satunya tempat `IFormFile` boleh muncul (di-map ke `ImageFileData`).

**2. `Application/Ports/` (kontrak Application)**
Interface yang dipakai service untuk mengakses infrastruktur: `IDigiflazzProvider`, `IMidtransProvider`,
`ITelegramProvider`, `ICloudflareR2Provider`, `IEmailProvider`, `IGoogleProvider`, `IRecaptchaProvider`,
`IRedisService`, dan `IApplicationDbContext` (pragmatic persistence port). Provider & DbContext
mengimplementasikan port ini — Application tidak boleh menyentuh class concrete/Infrastructure.

**3. `Services/` (Application use-case — Fat Services / Thin Controllers)**
"Otak" backend: semua logika use-case ada di sini. Service hanya bergantung pada Ports, Domain, dan DTOs.

**4. `Domain/` (murni, tanpa dependency)**
Business rule + entity murni. Tidak boleh `using` API/ASP.NET, EF Core, Redis, atau provider.
`Constants/` (AppConstants) boleh dipakai sebagai shared kernel.

**5. `Infrastructure/` + `Data/` + `Providers/` (implementasi teknis)**
Detail teknis: EF Core DbContext + migrasi, Redis, JWT, hosted service, dan adapter provider eksternal.
Semua diakses Application lewat port di `Application/Ports/`.

**6. `DTOs/`**
Kontrak request/response API. Murni data, tanpa EF/provider.

### Aturan Dependency (target)
```text
API → Application → Domain
Infrastructure → Application/Domain
Domain → (hanya BCL + Constants + Domain itu sendiri)
```
Verifikasi perubahan besar: `dotnet build` 0/0 → parity `docs/regression/route_parity.py` (62 = 62)
→ smoke `docs/regression/smoke.sh` → `dotnet ef migrations has-pending-model-changes`.
