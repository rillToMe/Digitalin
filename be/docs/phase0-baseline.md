# Phase 0 — Baseline Gate (Clean Architecture Migration)

> Snapshot baseline sebelum migrasi Clean Architecture. Gate wajib hijau sebelum Fase 1 (ports).
> Tanggal: September 2026. Referensi commit: `978a2d1` ("pertama sebelum refactor clean").

## Hasil Gate

| Check | Hasil | Bukti |
|---|---|---|
| `dotnet build be_digitalin.csproj` | ✅ **0 Warning / 0 Error** | build log Fase 0 |
| Route parity vs `docs/route-baseline.txt` | ✅ **62 = 62, MISSING: [], EXTRA: []** | `docs/regression/route_parity.py` |
| Smoke regression (auth, admin, error shapes, rate limit) | ✅ **18/18 PASS** | `docs/regression/smoke.sh` |
| API contract sync (`API_CONTRACT.md` vs OpenAPI) | ✅ **61/62** — sisanya `GET /ping-db` (infra, sengaja di luar contract) | script sync inline |
| Migrations terpasang di DB | ✅ 16 migration, identik baseline | `dotnet ef migrations list` |
| Pending model changes | ✅ **Tidak ada** | `dotnet ef migrations has-pending-model-changes` |

## Artefak Verifikasi

| Artefak | Nilai |
|---|---|
| OpenAPI `/openapi/v1.json` (live, port 5000, Development) | sha256 `f46eb1e714c27a2c`, 98130 bytes |
| Route baseline | `docs/route-baseline.txt` (62 route, trailing-slash dinormalisasi) |
| Migrations list | `InitialMariaDb` → `AddPerformanceIndexes` (16 entry, urutan identik) |
| Role JWT | `ADMIN`, `SUPER_ADMIN` (`Constants.Roles`) |

## Perilaku yang Diverifikasi (smoke)

- **Public**: ping-db 200, banners 200, search 200, games/brands 200
- **Error shape `{message}`**: check-email blank → `400 {"message":"Email tidak boleh kosong."}`; voucher missing → `404 {message}`; order retry missing → `404 {message}`; rate limit → `429 {message}`
- **Empty 4xx**: margin produk missing → `404` body kosong (efek `SuppressMapClientErrors`)
- **Auth**: profile tanpa token → 401; admin tanpa token → 401; ADMIN → 200; SuperAdminOnly dengan ADMIN → **403**
- **Webhook**: signature digiflazz salah → 401
- **Rate limit**: admin login 6× → 429 (AdminLoginPolicy 5/5 menit/IP)

## Cara Pakai Harness (tiap fase migrasi)

```bash
# 1. Build 0/0
cd be_digitalin && dotnet build be_digitalin.csproj

# 2. Start server Development (bukan dotnet run — exe langsung, port 5000)
cd be_digitalin && (ASPNETCORE_ENVIRONMENT=Development ./bin/Debug/net10.0/be_digitalin.exe \
    > /tmp/phase0-server.log 2>&1 & echo $! > /tmp/phase0.pid)

# 3. Parity route
python docs/regression/route_parity.py \
    --openapi http://localhost:5000/openapi/v1.json --baseline docs/route-baseline.txt

# 4. Smoke (18 checks)
BASE=http://localhost:5000 bash docs/regression/smoke.sh

# 5. Schema
cd be && dotnet tool run dotnet-ef migrations list --project be_digitalin --no-build
cd be && dotnet tool run dotnet-ef migrations has-pending-model-changes --project be_digitalin --no-build

# 6. Matikan server
taskkill //PID $(cat /tmp/phase0.pid) //F
```

## Catatan Perubahan Selama Gate

- `docs/regression/route_parity.py`, `docs/regression/mint_jwt.py`, `docs/regression/smoke.sh` — harness baru (persisted untuk dipakai tiap fase).
- `dotnet-tools.json` — manifest local tool `dotnet-ef` 10.0.11 (baru, di root repo).
- `API_CONTRACT.md` — 2 endpoint yang belum terdokumentasi ditambahkan: `GET /api/v1/auth/check-email` dan `GET /api/v1/admin/banners`. Tidak ada perubahan kontrak (endpoint sudah ada, hanya dokumentasi yang dilengkapi).
- `docs/route-baseline.txt` — tidak berubah (sudah ada dari migrasi controller).
- **Tidak ada perubahan kode sumber** (Controllers/Services/Models/DTOs/Program.cs) selama gate.