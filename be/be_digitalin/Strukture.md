```text
be_digitalin/
├── 📁 Data/           (Tempat untuk koneksi database & Migrasi)
├── 📁 Models/         (Tempat untuk struktur tabel database)
├── 📁 DTOs/           (Tempat untuk format data Request/Response)
├── 📁 Services/       (Tempat untuk logika bisnis)
├── 📁 Endpoints/      (Tempat untuk mengatur rute/URL API)
├── appsettings.json
├── Program.cs
└── ...

```

### Rincian Fungsi Tiap Folder:

**1. `Models/` (atau `Entities/`)**
Hanya berisi *class* C# murni yang mencerminkan tabel di dalam PostgreSQL.

* **Isi file nanti:** `GameProduct.cs`, `TopupOrder.cs`, `User.cs`.
* *Aturan:* File di sini tidak boleh berisi logika perhitungan, murni hanya struktur properti (ID, Nama, Harga, dll).

**2. `Data/**`
Pusat kontrol *database* dan tempat bernaungnya Entity Framework Core.

* **Isi file nanti:** `DigitalinDbContext.cs`.
* *Catatan:* Saat Anda menjalankan perintah migrasi nanti, EF Core secara otomatis akan membuat sub-folder `Migrations/` di dalam folder ini.

**3. `DTOs/` (Data Transfer Objects)**
Sangat krusial untuk keamanan. DTO adalah bentuk data yang dikirim oleh *user* (Front-end) atau dikembalikan oleh API Anda. Kita tidak boleh mengirimkan struktur tabel *database* asli ke klien.

* **Isi file nanti:** `CreateOrderRequest.cs`, `TopupResponse.cs`.

**4. `Services/**`
Ini adalah "otak" dari *backend* Anda. Semua proses logika perhitungan ditaruh di sini agar API Anda tetap bersih.

* **Isi file nanti:** `OrderService.cs` (berisi logika: cek stok $\rightarrow$ potong saldo $\rightarrow$ simpan ke DB), `PaymentService.cs` (berisi logika pembuatan *invoice* tagihan).

**5. `Endpoints/**`
Karena Anda menggunakan Minimal API, jika semua *route* (`app.MapGet`, `app.MapPost`) ditaruh di `Program.cs`, filenya akan memanjang hingga ribuan baris. Folder ini digunakan untuk memecah *route* tersebut.

* **Isi file nanti:** `OrderEndpoints.cs`, `ProductEndpoints.cs`.

---