# Analisis SOLID Backend Digitalin

Tanggal analisis: 22 November 2026.

Status: analisis statis selesai. Perbaikan belum dikerjakan.

## Ruang lingkup

Analisis mengacu pada implementasi backend aktif di `be/src`, meliputi controller, Application, Domain, Infrastructure, dan registrasi dependency injection (DI).

Dokumen ini mencatat temuan dan rencana perbaikan. Pembuatan dokumen tidak mengubah implementasi, skema database, atau kontrak API. Build dan test runtime belum dijalankan untuk analisis ini.

Nomor baris mengacu pada kondisi kode saat analisis dan dapat bergeser setelah perubahan.

## Acuan prinsip

- **Single Responsibility Principle (SRP):** satu class memiliki satu alasan utama untuk berubah. Beberapa method diperbolehkan selama melayani tanggung jawab yang sama.
- **Open/Closed Principle (OCP):** perilaku dapat diperluas melalui titik ekstensi tanpa berulang kali mengubah logika inti yang sudah berjalan. Perubahan registrasi DI atau konfigurasi tetap dapat diperlukan.
- **Liskov Substitution Principle (LSP):** implementasi atau turunan pengganti harus menjaga kontrak perilaku, termasuk hasil, kegagalan, dan efek samping yang diandalkan pemakai.
- **Interface Segregation Principle (ISP):** pemakai bergantung pada kontrak sesuai kebutuhannya, tanpa membawa operasi yang tidak digunakan.
- **Dependency Inversion Principle (DIP):** kebijakan utama bergantung pada abstraksi berdasarkan kebutuhan aplikasi. Adapter menerjemahkan detail teknologi atau provider.

## Ringkasan

Backend sudah memiliki fondasi SOLID, tetapi pemisahan tanggung jawab dan abstraksinya belum tuntas.

| Prinsip | Hasil pemeriksaan |
| --- | --- |
| SRP | Fulfillment, customer, dan pengaturan produk masih mencampur tanggung jawab yang berubah secara terpisah. |
| OCP | Penambahan kanal pembayaran masih berpotensi mengubah validator, proses order, dan adapter. |
| LSP | Belum ditemukan pelanggaran konkret. Kontrak hasil provider perlu diperjelas. |
| ISP | Interface order, produk, dan Digiflazz terlalu luas bagi sebagian konsumennya. |
| DIP | Port sudah tersedia, tetapi beberapa layanan masih terikat pada class konkret dan detail provider. |

## Temuan

### 1. FulfillmentService memiliki terlalu banyak tanggung jawab

**Prioritas:** tinggi. **Prinsip:** SRP.

**Bukti:** [FulfillmentService.cs](../src/be_digitalin.Application/Features/Fulfillment/FulfillmentService.cs), khususnya baris 17, 51, 101, 180, 424, 514, 577, dan 1102.

Class ini menangani:

- Eksekusi top-up setelah pembayaran.
- Callback supplier.
- Retry dan refund admin.
- Eskalasi pembayaran terlambat.
- Rekonsiliasi background untuk pembayaran dan fulfillment.
- Penyusunan pesan HTML dan pengiriman notifikasi Telegram.

Perubahan format notifikasi atau kebijakan pembayaran terlambat menyentuh class yang juga menjaga keamanan retry top-up. Panjang 1.210 baris mendukung temuan, tetapi dasar pelanggarannya adalah tanggung jawab yang berbeda.

**Arah perbaikan:** pisahkan notifikasi operator terlebih dahulu. Pisahkan rekonsiliasi pembayaran dan supplier sesuai kebutuhan perubahan. Pertahankan aturan transisi status sebagai sumber kebenaran bersama.

**Kriteria selesai:** format notifikasi dapat diubah tanpa mengubah logika transisi fulfillment. Retry, refund, callback, dan sweep tetap menjaga perilaku serta pengamanan konkurensi sebelumnya.

### 2. Facade membuat dependency konkret sendiri

**Prioritas:** tinggi. **Prinsip:** DIP dan OCP.

**Bukti:**

- [OrderService.cs](../src/be_digitalin.Application/Features/Orders/OrderService.cs), baris 28–44, membuat `FulfillmentService`, `OrderPlacementService`, `OrderQueryService`, dan `PaymentWebhookService` secara langsung.
- [ProductService.cs](../src/be_digitalin.Application/Features/Catalog/ProductService.cs), baris 29–41, membuat layanan storefront, sinkronisasi, admin, dan cache.
- [PaymentWebhookService.cs](../src/be_digitalin.Application/Features/Payments/PaymentWebhookService.cs), baris 24–28, menerima `FulfillmentService` konkret.

Pemecahan class sudah dilakukan, tetapi facade masih menentukan implementasi internalnya. Mengganti layanan atau menambahkan dekorator membutuhkan perubahan facade. Pengujian terisolasi juga sulit mengganti dependency tersebut dengan fake.

**Arah perbaikan:** injeksikan kontrak layanan sesuai kebutuhan konsumen. Pindahkan pembuatan objek dan pemilihan implementasi ke registrasi DI. Tambahkan interface pada batas perilaku yang perlu diganti atau diuji terpisah.

**Kriteria selesai:** layanan terkait dapat diganti melalui registrasi DI tanpa mengubah facade atau pemanggil use case.

### 3. Interface terlalu luas bagi konsumennya

**Prioritas:** tinggi. **Prinsip:** ISP.

**Bukti:**

- [IOrderService.cs](../src/be_digitalin.Application/Features/Orders/IOrderService.cs), baris 23, menggabungkan pembuatan order, query, webhook, retry, refund, dan sweep.
- [IProductService.cs](../src/be_digitalin.Application/Features/Catalog/IProductService.cs), baris 8, menggabungkan storefront, pengelolaan produk, sinkronisasi, gambar, dan expiry pembayaran.
- [IDigiflazzProvider.cs](../src/be_digitalin.Application/Abstractions/Ports/IDigiflazzProvider.cs), baris 61, menggabungkan top-up, cek username, daftar harga, saldo, signature, dan status transaksi.
- [OrderSweepWorker.cs](../src/be_digitalin.Infrastructure/Workers/OrderSweepWorker.cs), baris 27–29, hanya membutuhkan sweep tetapi menerima seluruh `IOrderService`.
- [DigiflazzSyncWorker.cs](../src/be_digitalin.Infrastructure/Workers/DigiflazzSyncWorker.cs), baris 41–47, hanya membutuhkan sinkronisasi tetapi menerima seluruh `IProductService`.

Pemakai dan fake pengujian membawa kontrak lebih luas daripada kebutuhannya. Perubahan interface dapat merambat ke komponen yang tidak menggunakan operasi baru.

**Arah perbaikan:** bentuk kontrak berdasarkan kebutuhan konsumen, misalnya `IOrderQueryService`, `IOrderReconciliationService`, dan `ICatalogSyncService`. Pisahkan kemampuan provider bila konsumennya berbeda. Nama akhir mengikuti pola repository.

Satu implementasi boleh memenuhi beberapa interface kecil. ISP tidak mewajibkan satu class untuk setiap interface.

**Kriteria selesai:** worker, controller, dan layanan hanya menerima operasi yang diperlukan untuk tugasnya.

### 4. Ekstensi pembayaran tersebar di beberapa lapisan

**Prioritas:** tinggi. **Prinsip:** OCP dan DIP.

**Bukti:**

- [PaymentMethodRules.cs](../src/be_digitalin.Domain/Payments/PaymentMethodRules.cs), baris 26–61, memiliki daftar metode dan subkanal yang ditulis langsung.
- [OrderPlacementService.cs](../src/be_digitalin.Application/Features/Orders/OrderPlacementService.cs), baris 77–103, memiliki cabang khusus bank dan gerai.
- [MidtransProvider.cs](../src/be_digitalin.Infrastructure/Payments/Midtrans/MidtransProvider.cs), baris 74–135, memiliki cabang payload Mandiri, cstore, dan ShopeePay.
- [IMidtransProvider.cs](../src/be_digitalin.Application/Abstractions/Ports/IMidtransProvider.cs), baris 7, mengekspos kontrak dengan model dan istilah Midtrans.

Penambahan kanal berpotensi membutuhkan perubahan validator, proses order, dan adapter. Interface provider sudah ada, tetapi mengganti gateway tetap memengaruhi Application karena kontraknya mengikuti provider tertentu.

**Arah perbaikan:** bentuk kontrak pembayaran berdasarkan kebutuhan aplikasi, seperti `IPaymentGateway`. Adapter menerjemahkan hasil provider menjadi hasil pembayaran aplikasi. Jika penambahan kanal sering terjadi, gunakan handler kanal yang diregistrasikan melalui DI. Pisahkan kemampuan kanal dari status aktifnya di database.

**Kriteria selesai:** penambahan kanal menggunakan titik ekstensi yang jelas. Pemilihan gateway tidak mengharuskan penulisan ulang kebijakan order. Perilaku kanal lama tetap terjaga.

### 5. Kebijakan expiry pembayaran berada di layanan produk dan menghasilkan default berbeda

**Prioritas:** tinggi. **Prinsip:** SRP; terkait sumber kebenaran kebijakan.

**Bukti:**

- [ProductService.cs](../src/be_digitalin.Application/Features/Catalog/ProductService.cs), baris 104–114, membaca expiry dengan fallback **5 menit** dan menyimpan setting pembayaran.
- [AppSettingsService.cs](../src/be_digitalin.Infrastructure/Settings/AppSettingsService.cs), baris 65–70, menolak nilai `<= 0` dan memakai `MidtransExpiryDefaultMinutes`, yaitu **30 menit**.
- [MidtransProvider.cs](../src/be_digitalin.Infrastructure/Payments/Midtrans/MidtransProvider.cs), baris 65, memakai pembacaan dari `AppSettingsService`.
- [AdminSystemController.cs](../src/be_digitalin.Api/Controllers/AdminSystemController.cs), baris 58–71, memakai pembacaan dan penyimpanan melalui `IProductService`.

Saat setting kosong atau tidak dapat diparse, endpoint admin dapat menampilkan 5 menit, sementara pembuatan pembayaran menggunakan 30 menit. Nilai nol atau negatif juga diperlakukan berbeda oleh kedua jalur pembacaan.

**Arah perbaikan:** pindahkan pengaturan expiry dari layanan produk. Gunakan satu kebijakan pembacaan, validasi, dan default untuk admin, charge, invoice, serta sweep. Tetapkan nilai default yang benar dengan mengacu pada kontrak dan kebutuhan produk sebelum mengubah perilaku.

**Kriteria selesai:** seluruh konsumen memberi hasil konsisten untuk setting kosong, rusak, nol, negatif, dan valid. Aturan khusus expiry kanal seperti cstore tetap berlaku.

### 6. CustomerService mencampur autentikasi, OTP, profil, dan riwayat pesanan

**Prioritas:** sedang. **Prinsip:** SRP.

**Bukti:** [CustomerService.cs](../src/be_digitalin.Application/Features/Customers/CustomerService.cs), baris 16–217, serta [OtpService.cs](../src/be_digitalin.Application/Features/Auth/OtpService.cs), baris 8.

`CustomerService` mengatur registrasi, CAPTCHA, pembuatan OTP, penyimpanan Redis, email, login, password, profil, dan riwayat order. `OtpService` memiliki mekanisme OTP lain berbasis database.

Perbedaan penyimpanan dapat diperlukan karena registrasi belum memiliki customer. Namun, kebijakan OTP tersebar di beberapa layanan sehingga perubahan masa berlaku atau konsumsi OTP membutuhkan pemeriksaan beberapa jalur.

**Arah perbaikan:** pisahkan autentikasi lokal dari profil customer. Bentuk kontrak kebijakan OTP yang mendukung identitas email maupun customer tanpa memaksakan penyimpanan yang sama. Gunakan kontrak query order yang sempit untuk riwayat pembelian.

**Kriteria selesai:** perubahan profil tidak menyentuh autentikasi atau OTP. Perilaku OTP registrasi dan reset password tetap eksplisit serta terverifikasi.

### 7. Hashing password bergantung langsung pada BCrypt

**Prioritas:** sedang. **Prinsip:** DIP.

**Bukti:**

- [CustomerService.cs](../src/be_digitalin.Application/Features/Customers/CustomerService.cs), baris 72, 175, dan 208.
- [AdminAuthService.cs](../src/be_digitalin.Application/Features/Administration/AdminAuthService.cs), baris 54.
- [AdminManagementService.cs](../src/be_digitalin.Application/Features/Administration/AdminManagementService.cs), baris 57 dan 96.
- [PasswordResetService.cs](../src/be_digitalin.Application/Features/Auth/PasswordResetService.cs), baris 60.

Panggilan BCrypt tersebar pada registrasi, login, perubahan password, dan administrasi. Mengubah algoritma atau kebijakan hashing membutuhkan perubahan beberapa use case.

**Arah perbaikan:** gunakan `IPasswordHasher` untuk operasi hash dan verifikasi. Bungkus BCrypt yang sudah berjalan dalam satu implementasi. Pertahankan kompatibilitas hash tersimpan dan kebijakan work factor yang diperlukan.

**Kriteria selesai:** use case tidak memanggil BCrypt langsung. Password dengan hash lama tetap dapat diverifikasi.

### 8. Port database masih terikat pada EF Core

**Prioritas:** rendah; kompromi arsitektur yang sudah didokumentasikan. **Prinsip:** DIP dan ISP sebagian.

**Bukti:** [IApplicationDbContext.cs](../src/be_digitalin.Application/Abstractions/Ports/IApplicationDbContext.cs), baris 8–55.

Interface mengekspos seluruh `DbSet`, `DatabaseFacade`, dan `EntityEntry`. Application tetap mengetahui transaksi, tracking, dan operasi EF. Ketergantungan terhadap `DigitalinDbContext` konkret sudah berkurang, tetapi penggantian teknologi persistence tetap membutuhkan perubahan Application.

File tersebut secara eksplisit mendokumentasikan pilihan EF-typed port sebagai kompromi. Temuan ini bukan alasan untuk mengganti seluruh data access dengan generic repository.

**Arah perbaikan:** evaluasi kebutuhan isolasi terlebih dahulu. Tambahkan port persistence khusus hanya untuk operasi kompleks yang memerlukannya. Pertahankan pola EF Core sesuai aturan repository.

**Kriteria selesai:** keputusan mempertahankan port atau menambah port khusus memiliki alasan konkret. Jika diubah, transaksi dan pengamanan konkurensi tetap terjaga.

## Penilaian Liskov Substitution

Belum ditemukan bukti cukup untuk menyatakan pelanggaran LSP. Banyaknya tanggung jawab class atau penggunaan dependency konkret tidak otomatis menjadi pelanggaran LSP.

Risiko kontrak terlihat pada `IDigiflazzProvider.TopupAsync`, yang mengembalikan `string?`. [FulfillmentService.cs](../src/be_digitalin.Application/Features/Fulfillment/FulfillmentService.cs), baris 941–988, menafsirkan hasil sebagai berikut:

| Hasil | Makna yang diandalkan pemakai |
| --- | --- |
| `null` | Hasil tidak pasti. |
| String kosong | Transaksi diterima dan masih pending. |
| String berisi SN | Transaksi selesai. |
| `DigiflazzTerminalFailureException` | Kegagalan terminal. |
| `DigiflazzUncertainOutcomeException` atau kegagalan lain | Hasil tidak pasti. |

Implementasi pengganti harus menjaga seluruh makna tersebut. Signature method saja belum menjelaskan kontrak perilakunya.

**Arah perbaikan:** gunakan hasil terstruktur dengan status eksplisit. `DigiflazzTransactionResult` sudah tersedia dan dapat menjadi dasar. Lengkapi dengan contract test yang berlaku untuk setiap implementasi provider.

**Kriteria selesai:** implementasi pengganti menjaga hasil completed, pending, terminal failure, dan uncertain, tanpa menyebabkan pengiriman ulang top-up yang tidak aman.

## Fondasi yang sudah baik

- Project Domain tidak mereferensikan paket Infrastructure atau EF Core.
- Port provider eksternal sudah tersedia dan didaftarkan melalui DI.
- Layanan order dan katalog sudah mulai dipecah menjadi layanan yang lebih terfokus.
- Aturan domain seperti transisi status, expiry, input schema, dan pricing sudah memiliki lokasi tersendiri.

## TODO

Gunakan `[x]` hanya setelah pekerjaan dan verifikasi yang relevan selesai. Item perbaikan di bawah masih `[ ]` karena belum dikerjakan. Urutan mengikuti prioritas yang disarankan; pecahan pekerjaan dapat disesuaikan setelah memeriksa kode terbaru.

### Analisis dan dokumentasi

- [x] Membaca aturan backend dan bagian kontrak API yang relevan.
- [x] Memetakan lapisan backend aktif di `be/src`.
- [x] Memeriksa penerapan lima prinsip SOLID pada jalur utama yang dianalisis.
- [x] Mencatat bukti kode, dampak, dan rekomendasi perbaikan.
- [x] Membedakan pelanggaran yang terbukti dari risiko kontrak LSP.
- [x] Menulis analisis dan checklist perbaikan dalam dokumen Markdown.

### Tahap 1 — Satukan kebijakan expiry pembayaran

- [ ] Tetapkan default expiry yang benar berdasarkan kontrak dan kebutuhan produk.
- [ ] Satukan pembacaan, validasi, dan fallback expiry pembayaran.
- [ ] Pindahkan pengaturan expiry dari kontrak dan implementasi produk.
- [ ] Hubungkan admin, charge, invoice, dan sweep ke kebijakan yang sama.
- [ ] Verifikasi setting kosong, rusak, nol, negatif, dan valid, termasuk aturan cstore.

### Tahap 2 — Persempit interface dan benahi penyusunan dependency

- [ ] Petakan operasi yang digunakan setiap konsumen `IOrderService` dan `IProductService`.
- [ ] Pisahkan kontrak order berdasarkan kebutuhan konsumen.
- [ ] Pisahkan kontrak storefront, administrasi katalog, dan sinkronisasi.
- [ ] Pisahkan kemampuan Digiflazz berdasarkan kebutuhan konsumen yang berbeda.
- [ ] Perbarui worker, controller, dan layanan agar memakai kontrak yang diperlukan.
- [ ] Injeksikan layanan internal order dan katalog melalui DI.
- [ ] Hilangkan pembuatan layanan internal secara langsung dari facade terkait.
- [ ] Ubah dependency konkret fulfillment pada pemrosesan webhook menjadi kontrak yang sesuai.
- [ ] Verifikasi resolusi DI dan perilaku pemanggil yang terdampak.

### Tahap 3 — Pisahkan tanggung jawab fulfillment

- [ ] Ekstrak penyusunan dan pengiriman notifikasi operator dari `FulfillmentService`.
- [ ] Pisahkan rekonsiliasi pembayaran dari rekonsiliasi supplier sesuai batas tanggung jawab.
- [ ] Tinjau pemisahan retry, refund, dan callback tanpa menduplikasi aturan status.
- [ ] Pertahankan satu sumber aturan transisi status dan klasifikasi hasil supplier.
- [ ] Verifikasi idempotensi, konkurensi, late payment, voucher, retry, refund, dan sweep.

### Tahap 4 — Sediakan titik ekstensi pembayaran

- [ ] Definisikan kontrak gateway dan hasil pembayaran berdasarkan kebutuhan aplikasi.
- [ ] Petakan detail Midtrans ke kontrak aplikasi di adapter.
- [ ] Pisahkan validasi kemampuan kanal dari konfigurasi aktif atau nonaktif.
- [ ] Tentukan kebutuhan handler kanal berdasarkan pola penambahan fitur yang nyata.
- [ ] Jika diperlukan, registrasikan handler kanal melalui DI.
- [ ] Verifikasi kompatibilitas kanal pembayaran lama dan instruksi pembayarannya.

### Tahap 5 — Pisahkan customer, autentikasi, dan hashing

- [ ] Pisahkan autentikasi lokal dari pengelolaan profil customer.
- [ ] Arahkan riwayat customer ke kontrak query order yang sempit.
- [ ] Satukan batas kebijakan OTP dengan mempertahankan kebutuhan identitas email dan customer.
- [ ] Tambahkan abstraksi hashing password yang membungkus BCrypt saat ini.
- [ ] Ganti panggilan BCrypt langsung pada use case terkait dengan abstraksi tersebut.
- [ ] Verifikasi registrasi, login customer dan admin, reset password, serta perubahan profil.
- [ ] Verifikasi hash password tersimpan tetap kompatibel.

### Tahap 6 — Perjelas kontrak substitusi provider

- [ ] Definisikan hasil top-up terstruktur untuk completed, pending, terminal failure, dan uncertain.
- [ ] Evaluasi penggunaan `DigiflazzTransactionResult` yang sudah tersedia.
- [ ] Perbarui adapter dan pemakai tanpa mengubah makna hasil transaksi.
- [ ] Tambahkan contract test untuk implementasi provider dan fake yang relevan.
- [ ] Verifikasi hasil ambigu tidak memicu top-up ulang yang tidak aman.

### Tahap 7 — Evaluasi port persistence

- [ ] Nilai kebutuhan port khusus berdasarkan operasi kompleks yang memerlukan isolasi.
- [ ] Dokumentasikan keputusan mempertahankan EF-typed port atau menambah port terfokus.
- [ ] Jika ada perubahan, verifikasi transaksi, atomic update, tracking, dan konkurensi tetap benar.

### Verifikasi sebelum menandai perbaikan selesai

- [ ] Jalankan build backend setelah perubahan implementasi.
- [ ] Jalankan test yang relevan terhadap perilaku yang berubah.
- [ ] Untuk perubahan besar, jalankan gate route parity dan smoke sesuai `be/AGENTS.md` serta `docs/regression`.
- [ ] Untuk perubahan besar, jalankan `dotnet ef migrations has-pending-model-changes` sesuai aturan backend.
- [ ] Jika kontrak API berubah, perbarui `be/API_CONTRACT.md`.
- [ ] Tinjau diff untuk memastikan perubahan tetap dalam lingkup dan tidak menimpa pekerjaan lain.
- [ ] Perbarui checklist menjadi `[x]` berdasarkan hasil pekerjaan dan bukti verifikasi.
