RULES - BACA SEBELUM IMPLEMENTASI APA PUN

ROLE & GENERAL PRINCIPLES:
- Act as an expert Senior Fullstack Developer.
- Adhere to CLEAN CODE (SOLID, DRY, KISS).
- Single Source of Truth (SSOT): No overlapping systems or duplicate logic.

BACKEND ARCHITECTURE (.NET Core Minimal API):
- Use Minimal API structure, NOT traditional Controllers.
- Keep endpoint mappings clean and thin.
- Centralize all business logic inside Service files (Fat Services / Thin Endpoints).

DATA ACCESS & PERSISTENCE (EF Core):
- Use Entity Framework Core for all database operations.
- NEVER hardcode raw SQL queries; rely strictly on EF Core LINQ / Lambda.

PRIORITAS
1. RULES.txt
2. API_CONTRACT.md
3. Arsitektur project yang ada
4. Permintaan user

SEBELUM MULAI
- Baca RULES.txt, API_CONTRACT.md
- Pahami struktur project dan dependency yang terdampak
- Jangan buat asumsi, jangan langsung coding

ANALISIS
- Gunakan Caveman untuk baca source code (hemat token)
- Gunakan MCP Context7 untuk library/framework yang baru atau sering berubah (Next.js, EF Core, Stripe, dll)

API CONTRACT
- API_CONTRACT.md adalah sumber kebenaran semua API
- Jangan buat endpoint yang sudah ada
- Jangan karang endpoint baru tanpa cek API_CONTRACT.md
- Setiap perubahan API wajib update API_CONTRACT.md, task belum selesai jika tidak diupdate

IMPLEMENTASI
- Ikuti coding style, naming convention, dan pola arsitektur project
- Reuse kode yang sudah ada
- Jangan overengineering, jangan tambah abstraksi/dependency tanpa alasan jelas
- Jangan ubah file yang tidak berkaitan

DATABASE
- Buat Entity, ubah DbContext, ubah Fluent Configuration jika perlu
- Jangan buat Migration.cs, Designer.cs, atau ModelSnapshot.cs manual
- Serahkan migration ke EF Core:
  dotnet ef migrations add <NamaMigration>
  dotnet ef database update

SECURITY
- Pakai hashing yang sudah dipakai project
- Jangan simpan password plaintext
- Jangan hardcode credential

OUTPUT SETIAP TASK
1. Ringkasan analisis
2. File yang diubah + alasan + dampak
3. Langkah verifikasi
4. Update API_CONTRACT.md jika API berubah

GOLDEN RULE
Ikuti codebase yang ada. Jangan paksa project mengikuti pola favorit agent.
