# Clean Architecture Migration Blueprint — FINAL (post-review)

> Audit + review berbasis codebase aktual (September 2026). **Belum ada kode yang diubah.**
> Prasyarat: migrasi Minimal API → Controllers selesai (15 controller, `Endpoints/` dihapus, parity 62/62).
> Target dependency: **API → Application → Domain**, **Infrastructure → Application/Domain**.
> Domain **tidak** boleh bergantung pada API/ASP.NET, EF Core, Redis, atau provider eksternal.
> Prinsip migrasi: **behavior first → dependency direction → folder/namespace last**.

---

## 1. Review Verdict

**GO, dengan koreksi.** Arah blueprint lama benar (ports + rewiring incremental, satu project, 0 API/schema change), tetapi ada 5 koreksi substantif hasil verifikasi kode:

| # | Klaim blueprint lama | Hasil verifikasi | Koreksi |
|---|---|---|---|
| K1 | `GameInputSchemaSerializer` dipindah ke `Domain/` | Isi file: murni `System.Text.Json`, SSOT format **kolom DB** `GameMetadata.InputSchemaJson`; beroperasi di `Models.GameInputField`. Ini technical serialization, bukan business rule | **JANGAN ke Domain** → `Application/Serialization/` (helper teknis konsumen use-case; sekaligus menutup P3 tanpa port) |
| K2 | `IApplicationDbContext` = DbSet + `SaveChangesAsync` + `Database` | Usage aktual service: `SaveChangesAsync` (semua), `_db.Database.BeginTransactionAsync()` ×2 (`OrderService:130,275`), `_db.Entry(order).ReloadAsync()` (`OrderService:624`), `_db.Entry(customer).State = Detached` (`GoogleAuthService:60`) | Interface harus expose **`Entry<T>` juga** (bukan hanya DbSet/Database); EF-typed interface **acceptable** sebagai pragmatic boundary (detail di §6) |
| K3 | `GameCategoryRegistry` pindah ke `Domain/Games` | `Merge()` (baris 125–145) memetakan **`GameCategoryInfo` DTO** — referensi `be_digitalin.DTOs` nyata | **TETAP di Application** (memetakan kontrak Application). Move ke Domain hanya setelah dipisah mapping-nya — tidak sekarang |
| K4 | `GameTemplate*` pindah di Fase A (awal) | Factory/GameTemplate/Type murni (using Services hanya doc-cref), tapi **mereferensikan `be_digitalin.Models`** | Pindah di **fase namespace terakhir** (bersama `Models → Domain/Entities`), bukan fase awal |
| K5 | `AppSettingsService` termasuk 14 service P1 | Ctor aktual: `(IDbContextFactory<DigitalinDbContext>, IMemoryCache)` — **factory sudah abstraksi**, bukan inject DbContext langsung | **Tidak perlu swap port**; cukup dipindah ke Infrastructure di fase akhir (P5). P1 = 13 service, bukan 14 |

Koreksi lain: `ImageFileValidator` ternyata menerima **`IFormFile`** (ASP.NET) — jadi perbaikan P3 (Storage) **terkunci** dengan perbaikan P4 (kontrak file). Keduanya digabung jadi satu slice (Fase 4). Fase 0 (baseline gate) ditambahkan wajib sebelum Fase 1.

---

## 2. Corrections to Existing Blueprint

1. **Domain boundary diperketat** — hanya kode dengan *business meaning* boleh masuk Domain. Kode pure teknis (serializer JSON kolom DB) → Application, bukan Domain.
2. **Urutan fase dibalik sebagian** — ekstraksi/pemindahan logika murni (OrderTargetRenderer, VoucherRules, GameTemplate*) tidak lagi Fase A; jadi Fase 5–6. Fase awal hanya: ports additive → adapters → rewiring ctor.
3. **`IApplicationDbContext` + `Entry<T>`** ditambahkan ke kontrak interface (kebutuhan nyata `ReloadAsync`/`State`).
4. **Fase 0 baseline gate** ditambahkan (build + regression harness + parity 62/62 + contract snapshot + schema check) — gate wajib sebelum Fase A.
5. **`IRedisService`/`RedisCacheKeys` move** tetap di fase inti (Fase 4): ini *dependency-driven move* (namespace move itu sendiri yang menutup P3), bukan kosmetik — dikecualikan dari aturan "namespace last".
6. **File upload fix digabung**: kontrak `ImageFileData` + `ImageFileValidator` pindah ke Application (terima `Stream`+fileName, bukan `IFormFile`) — menutup P3-storage dan P4 sekaligus.

---

## 3. Final Target Architecture

Satu project, folder + namespace sebagai lapisan (bukan split csproj — alasan di §13).

```
be_digitalin/
├── Controllers/              # API (15, tidak berubah isi; hanya mapping file contract di upload)
├── Middleware/               # API
├── Program.cs                # Composition root
│
├── Application/              # = Services/ + DTOs/ + Ports/ (dipertahankan)
│   ├── Ports/                # BARU: IDigiflazzProvider, IMidtransProvider, ITelegramProvider,
│   │                         #       ICloudflareR2Provider, IEmailProvider, IGoogleProvider,
│   │                         #       IRecaptchaProvider, IApplicationDbContext, IRedisService(+Keys)
│   ├── Serialization/        # GameInputSchemaSerializer (pindah dari Infrastructure — helper teknis)
│   ├── Validation/           # ImageFileValidator (pindah, terima Stream+fileName) + ImageFileData
│   └── Services/             # use-case; hanya bergantung Ports + Domain + DTOs
│
├── Domain/                   # + Models (entity POCO) + GameTemplate* (di fase terakhir)
│   └── Entities/             # (Models dipindah, namespace be_digitalin.Domain.Entities)
│   └── Orders/               # OrderStateTransition (tetap) + OrderTargetRenderer (opsional F5)
│   └── Commerce/             # VoucherRules (opsional F5)
│
└── Infrastructure/           # + Providers (implement Ports), TokenService, AppSettingsService,
    └── ...                   #   Workers/, Redis/ (impl saja), Storage/, Serialization/ (kosong → dihapus)
```

### Peta pemetaan final

| Bagian | Layer | Aksi |
|---|---|---|
| Controllers (15) | API | **Tetap** (sudah thin); 3 controller upload: map `IFormFile` → `ImageFileData` |
| Middleware | API | Tetap |
| DTOs (28) | Application | **Tetap** |
| Services/v1 (36) | Application | **Ubah**: ctor swap concrete → port (13 service + AppSettingsService tanpa swap) |
| Domain/ (3) | Domain | Tetap |
| Models/ (10) | Domain/Entities | **Pindah** (fase terakhir) |
| Providers/ (7) | Infrastructure | **Ubah**: implementasi port baru (body tidak berubah) |
| Data/ (DbContext) | Infrastructure | Tetap (+ implement `IApplicationDbContext`) |
| Infrastructure/ | Infrastructure | Redis impl tetap; interface+keys pindah ke Ports; serializer pindah ke Application; validator pindah |
| Workers/ (2) | Infrastructure | Pindah ke `Infrastructure/Workers` (fase terakhir) |
| TokenService, AppSettingsService | Infrastructure | Pindah (fase terakhir; tanpa swap port) |
| GameCategoryRegistry | **Application** | **Tetap** (koreksi K3) |
| GameTemplate* (3) | Domain/Games | Pindah (fase terakhir, bersama Models) |
| Config/ (8), Constants/ | Shared/Infra | Tetap (Constants = shared kernel) |

---

## 4. Domain vs Application vs Infrastructure Decision

Aturan klasifikasi (dipakai untuk seluruh file):

| Kriteria | → Layer |
|---|---|
| Business rule / invariant / policy storefront yang murni, tanpa IO | **Domain** |
| Use-case orchestration, mapping, kontrak request/response, helper teknis yang dipakai use-case | **Application** |
| IO eksternal, DB, cache, JWT signing, hosting, format kolom persisten | **Infrastructure** |
| Konstanta lintas layer tanpa dependency terlarang | Shared kernel (`Constants/`) |

Hasil per komponen (semua diverifikasi):

| Komponen | Verdict | Alasan |
|---|---|---|
| `Domain/Pricing`, `Slug`, `OrderStateTransition` | Domain ✅ (tetap) | Business value murni, tanpa IO |
| `Models/*` (10) | Domain/Entities (fase akhir) | Entity POCO tanpa EF deps (Fluent config di DbContext) |
| `GameTemplate*` (3) | Domain/Games (fase akhir) | Preset registry murni (using Services = doc-cref saja); tapi referensi `Models` → ikut fase Models |
| `GameCategoryRegistry` | **Application** (tetap) | `Merge()` memetakan DTO Application (`GameCategoryInfo`) |
| `GameInputSchemaSerializer` | **Application/Serialization** | Technical serialization kolom DB — bukan business rule (detail §5) |
| `ImageFileValidator` | Application/Validation | Validasi teknis murni BCL (setelah lepas `IFormFile`) |
| 14 service impl | Application (tetap) | Fat Service adalah pola project; hanya ctor yang di-swap |
| 7 Providers, DbContext, Redis, Config | Infrastructure | Tetap; Providers implement port |
| TokenService, AppSettingsService, Workers | Infrastructure (fase akhir) | Mekanisme infra (JWT/DB-read+mem-cache/hosted service) |
| `OrderTargetRenderer`, `VoucherRules` (ekstraksi) | Domain (opsional F5) | Business logic murni yang saat ini menumpang di service |

---

## 5. GameInputSchemaSerializer Decision

**TETAP DI LUAR DOMAIN — pindah ke `Application/Serialization/`.**

- **Isi aktual (diverifikasi)**: static class, `System.Text.Json` camelCase + case-insensitive, `Serialize`/`Deserialize(List<GameInputField>)`. Docstring sendiri menyatakan: *SSOT (de)serialisasi `GameMetadata.InputSchemaJson`* — format **kolom DB persisten**.
- **Kenapa bukan Domain**: Domain berisi business meaning; format JSON kolom DB adalah concern persistence/teknis. Memindahnya ke Domain "karena pure" justru melanggar prinsip review — Domain jadi tempat sampah kode murni.
- **Kenapa bukan Infrastructure (tetap)**: konsumennya (`OrderService`, `ProductService`) adalah Application; membiarkan namespace `Infrastructure.*` di-import Application = pelanggaran P3 yang tersisa. Opsi port untuk static helper = over-engineering.
- **Keputusan**: pindah ke `Application/Serialization/` (namespace `be_digitalin.Application.Serialization`), 1 file + 2 using di service. Tidak ada perubahan perilaku — JSON output/input byte-identical (sudah ada bukti test sebelumnya: `GameInputSchemaSerializerTests`).
- Tandai **needs verification**: jika suatu saat format kolom ini harus dipisahkan dari model, evaluasi ulang; untuk sekarang keputusan final.

---

## 6. IApplicationDbContext Decision

**Diterima: interface EF-typed pragmatis (gaya Jason Taylor). Bukan repository, bukan UoW penuh.**

- **Kontrak minimal yang DIBUTUHKAN kode aktual** (diverifikasi via grep):
  ```csharp
  public interface IApplicationDbContext
  {
      DbSet<GameProduct> GameProducts { get; }
      DbSet<TopupOrder> TopupOrders { get; }
      DbSet<Banner> Banners { get; }
      DbSet<GameMetadata> GameMetadata { get; }
      DbSet<Customer> Customers { get; }
      DbSet<AppSetting> AppSettings { get; }
      DbSet<Voucher> Vouchers { get; }
      DbSet<CustomerOtp> CustomerOtps { get; }
      DbSet<Admin> Admins { get; }
      Task<int> SaveChangesAsync(CancellationToken ct = default);
      DatabaseFacade Database { get; }                    // BeginTransactionAsync — OrderService:130,275
      EntityEntry<TEntity> Entry<TEntity>(TEntity entity) // ReloadAsync — OrderService:624; State=Detached — GoogleAuthService:60
          where TEntity : class;
  }
  ```
- **Mengapa acceptable**: (a) menghilangkan P1 nyata — Application tidak lagi mereferensikan `Data/` concrete; (b) semua anggota adalah kebutuhan terverifikasi, bukan spekulasi; (c) `Database`/`Entry` adalah **facade EF resmi**, bukan kebocoran implementasi internal; (d) referensi `Microsoft.EntityFrameworkCore` sudah transitif ada (satu csproj, tanpa package baru).
- **Perbandingan singkat vs repository/specific ports**: repository penuh harus mengabstraksi ulang `ReloadAsync`/transaksi/`Detached` = kebocoran lebih besar + ~9 × mapping boilerplate, untuk nilai nol (tidak ada skenario test double DB yang butuh itu). Specific persistence ports per use-case = abstraksi dini; belum ada use case yang menuntutnya.
- **Konsekuensi jujur**: Application bergantung pada package EF Core — trade-off pragmatis yang disepakati. Domain tetap murni (tidak menyentuh interface ini).
- `AppSettingsService` **tidak** ikut interface ini (pakai `IDbContextFactory` + pindah ke Infrastructure di Fase 6).

---

## 7. Repository Strategy

**TIDAK ADA repository — sekarang dan di fase mana pun dari migrasi ini.**

- `IGenericRepository<T>`: **dilarang** (hard constraint + tidak ada nilai: DbSet sudah generic).
- Specific repository/port: **tidak diperlukan** — seluruh akses data saat ini adalah LINQ langsung atas DbSet + transaksi + reload; `IApplicationDbContext` menutup semuanya. Tidak ada use case yang memerlukan test-double persisten (tidak ada unit test service saat ini; regression = smoke integration).
- Jika masa depan memerlukan (mis. unit-test service dengan fake DB, atau multi-provider storage): buat **specific port per use-case nyata** (mis. `IOrderPersistence`), bukan repository umum. Keputusan ini direvisi hanya saat ada kebutuhan aktual.

---

## 8. Revised Migration Phases

Verifikasi tiap fase (wajib, urut): `dotnet build` 0/0 → smoke regression → parity 62/62 → schema check → lanjut.

| Fase | Isi | Runtime impact | Gate khas |
|---|---|---|---|
| **0. Baseline gate** | Build; capture regression harness (smoke + parity script vs `docs/route-baseline.txt`); snapshot API contract (API_CONTRACT.md); verifikasi schema DB | Nol | build 0/0; parity 62=62; migrations list identik |
| **1. Ports (additive)** | Buat 8 port provider + `IApplicationDbContext` di `Application/Ports/` — belum dipakai siapa pun ✅ **SELESAI** (9 file, build 0/0, parity 62=62, smoke 18/18) | Nol | build 0/0 |
| **2. Adapters (additive)** | 7 provider implement port (`: IPort`, body tidak berubah); `DigitalinDbContext` implement `IApplicationDbContext` ✅ **SELESAI** (8 file, build 0/0, parity 62=62, smoke 18/18) | Nol | build 0/0 |
| **3. Rewiring per slice** | Swap ctor concrete → port + `Program.cs` DI. **Urutan: 3a Settings/Auth → 3b Content/Catalog → 3c Commerce/Customer → 3d System/Admin → 3e Orders (terakhir)** ✅ **SELESAI** — 14 service + `WebhookController` di-swap; build 0/0 tiap slice; parity 62=62, smoke 18/18, DI resolution 200 di semua domain | Nol (delegasi 1:1) | per slice: build + smoke domain itu |
| **4. P3+P4 file & cache** | `ImageFileData` record; `ImageFileValidator` → Application (Stream+fileName); 2 service + 3 controller upload di-map; `IRedisService`+`RedisCacheKeys` → Ports ✅ **SELESAI** — build 0/0, parity 62=62, smoke 18/18, smoke upload parity pesan legacy (no-file, ekstensi, magic-bytes, 404 id-missing) | Nol | smoke upload (parity pesan error legacy) |
| **5. Ekstraksi opsional** | `OrderTargetRenderer` + `VoucherRules` → Domain (delegasi murni); `GameInputSchemaSerializer` → Application/Serialization ✅ **SELESAI** — build 0/0, parity 62=62, smoke 18/18, voucher rules live (400 pesan legacy identik); **tidak ada lagi `using Infrastructure.*` di luar Infrastructure + Program.cs** | Nol | smoke order/voucher |
| **6. Normalisasi namespace (TERAKHIR)** | `Models` → `Domain/Entities`; `GameTemplate*` → `Domain/Games`; `Workers` → `Infrastructure/Workers`; `TokenService`/`AppSettingsService` → Infrastructure; update `Strukture.md`, `AGENTS.md` ✅ **SELESAI** — build 0/0, parity 62=62, smoke 18/18, `has-pending-model-changes` bersih | Nol | build + parity penuh |

**Fase 4–6 hanya dijalankan setelah 1–3; Fase 5 boleh di-skip** (nilai rendah, behavior-neutral). Fase 6 tidak boleh dijalankan sebelum arah dependency stabil (aturan "folder/namespace last").

---

## 9. Phase 0 Baseline Gate (wajib sebelum Fase 1)

```
[x] dotnet build be_digitalin.csproj  → 0 warning / 0 error          ✅ (Fase 0 selesai)
[x] Regression harness disiapkan & dijalankan:
      • script smoke → 18/18 PASS (docs/regression/smoke.sh)
      • script parity route vs docs/route-baseline.txt → 62 = 62, MISSING: [], EXTRA: [] (route_parity.py)
[x] Snapshot API contract: API_CONTRACT.md sinkron 61/62 dengan OpenAPI (hanya ping-db infra di luar); 2 endpoint sempat kurang → sudah dilengkapi
[x] Database/schema:
      • dotnet ef migrations list  → 16 migration, identik baseline (dotnet-ef 10.0.11, local tool — manifest dotnet-tools.json)
      • dotnet ef migrations has-pending-model-changes → tidak ada pending ✅
[x] Snapshot baseline → docs/phase0-baseline.md (hash OpenAPI f46eb1e714c27a2c, commit 978a2d1, hasil gate)
```

Harness: `docs/regression/{route_parity.py,mint_jwt.py,smoke.sh}` — dipakai ulang di setiap fase berikutnya (cara pakai: `docs/phase0-baseline.md`). Tidak ada test project otomatis saat ini (test project sebelumnya dihapus atas permintaan — Fase 0 memakai harness smoke/parity yang sudah terbukti). **Needs verification**: menghidupkan kembali xUnit + `Microsoft.AspNetCore.Mvc.Testing` adalah opsi hardening pasca-Fase 6, bukan prasyarat.

---

## 10. Files Affected Per Phase

| Fase | Dibuat | Diubah | Dipindah |
|---|---|---|---|
| 0 | snapshot artifacts (bukan source) | — | — |
| 1 | +9 (`Ports/` × 9) ✅ + `RecaptchaVerifyResponse` dipindah ke Ports (1 file provider di-update) | — | — |
| 2 | — | 7 providers + `DigitalinDbContext` (8) | — |
| 3 | — | 13 service ctor + `Program.cs` (≈14, per slice) | — |
| 4 | +1 (`ImageFileData`) | `ImageFileValidator` (signature), `ProductService`, `BannerService`, 3 controller upload, `IRedisService`/`RedisCacheKeys` (namespace), `CustomerService`, `Program.cs` (≈9) | — |
| 5 | +2 (Renderer, Rules) | `OrderService`, `VoucherService`, `GameInputSchemaSerializer` (namespace) + 2 using service (≈5) | — |
| 6 | — | ~25 referensi `Models` + `Program.cs` (workers, DI) | Models (10), GameTemplate* (3), Workers (2), TokenService (1), AppSettingsService (1) |
| **Total** | **12** | **~55 (dengan overlap referensi)** | **17** |

> **Catatan Fase 6 (disetujui user):** rename `be_digitalin.Models` → `be_digitalin.Domain.Entities` menyentuh 17 file migrasi (16 Designer + ModelSnapshot, 251 string nama tipe `be_digitalin.Models.`) — hanya teks snapshot, **schema DB tidak berubah**; diverifikasi `has-pending-model-changes` = bersih. `Models/` folder → `Domain/Entities/`. Opsi hardening DTO (BannerResponse/CreateGameProductRequest untuk 3 controller) tidak dijalankan — API → Domain.Entities diperbolehkan arah dependency, JSON shape tak berubah.

Angka kotor tetap ~55–65 file tersentuh — sama dengan estimasi awal, tetapi **sequencing-nya berubah** (ekstraksi & namespace di akhir). API contract, database schema, behavior: **0 perubahan**.

---

## 11. Risks & Mitigation

| Risiko | Level | Mitigasi |
|---|---|---|
| Behavioral drift saat swap port (delegasi tidak 1:1) | Tinggi | Aturan keras: port = delegasi murni; TIDAK ada perubahan logika dalam commit yang sama; gate per fase (build + smoke + parity) |
| `OrderService` — paling kompleks (webhook, sweep, retry, refund, concurrency, 2 transaksi) | Tinggi | Slice 3e paling akhir, fase terpisah, smoke khusus (sweep/retry path, voucher tx) |
| Churn namespace `Models → Domain.Entities` (~25 referensi) | Sedang | Fase 6 terisolasi, compiler-guided, tanpa perubahan logika |
| Salah daftarkan DI (concrete vs interface, `AddHttpClient<T>` vs `<TInterface,TImpl>`) | Rendah | Compile-time + smoke 401/200 per domain |
| Single project = tanpa enforcement compile-time arah dependency | Sedang | Aturan tertulis AGENTS.md + review checklist; opsi lanjut: analyzer rule atau test project (post-F6) |
| Upload fix (Fase 4) mengubah pesan error legacy | Sedang | Mapping `IFormFile` → `ImageFileData` di controller; smoke khusus parity pesan (`File tidak boleh kosong.`, format file) |
| `IApplicationDbContext` expose `Database`/`Entry` (EF di Application) | Rendah | Diterima sebagai pragmatisme (§6); Domain tetap murni |
| Parity script / baseline jadi basi | Rendah | Snapshot commit di Fase 0; script dipakai tiap fase |

---

## 12. Final Implementation Order

```text
Phase 0 (gate) ──▶ Phase 1 (ports) ──▶ Phase 2 (adapters) ──▶ Phase 3 (rewiring)
    │                                                     │
    │                        ┌────────────────────────────┘
    │                        ▼
    │             3a Settings/Auth → 3b Content/Catalog → 3c Commerce/Customer
    │             → 3d System/Admin → 3e Orders
    ▼
Phase 4 (file & cache: P3+P4) ──▶ [Phase 5 opsional: ekstraksi Domain]
    ▼
Phase 6 (normalisasi namespace + docs) ──▶ regression penuh + update docs
```

Setiap fase = satu unit kerja yang bisa di-commit & di-deploy sendiri. Fase 3, 4, 6 masing-masing menutup verifikasi: build 0/0, smoke, parity 62/62, schema check.

---

## 13. Go / No-Go Decision

**GO — dengan syarat:**

1. **Fase 0 gate harus hijau** sebelum Fase 1 dimulai (tidak bisa di-skip).
2. **Koreksi K1–K5 diterapkan** (serializer → Application bukan Domain; `Entry<T>` masuk kontrak; GameCategoryRegistry tetap Application; GameTemplate* di fase akhir; AppSettingsService tanpa swap).
3. **Fase 3e (Orders) tidak boleh digabung** dengan slice lain.
4. **Fase 5 (ekstraksi) & Fase 6 (namespace) opsional/terakhir** — nilai utamanya ada di Fase 1–4 yang menghilangkan P1–P3. Jika waktu terbatas: kerjakan 0–4, tunda 5–6.
5. **Tetap satu project** — split csproj (Domain dulu, lalu Application) hanya jika ada kebutuhan nyata (mis. team size naik / kebutuhan publish terpisah). Enforcement arah dependency belakangan via analyzer atau test project.
6. **No-go triggers** (hentikan dan review ulang): parity < 62, schema drift terdeteksi, atau smoke error-shape berubah di luar yang didokumentasikan.

**Alasan GO**: seluruh risiko adalah refactor murni (0 API/schema/behavior change), codebase sudah punya gate parity 62/62 yang terbukti menangkap drift, dan urutan fase baru menempatkan perubahan berisiko (Orders, namespace) di posisi paling akhir dengan isolasi penuh.