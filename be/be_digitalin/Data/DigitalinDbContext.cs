using be_digitalin.Application.Ports;
using be_digitalin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Data;

public class DigitalinDbContext : DbContext, IApplicationDbContext
{
    public DigitalinDbContext(DbContextOptions<DigitalinDbContext> options) : base(options) { }
    
    public DbSet<GameProduct> GameProducts { get; set; }
    public DbSet<TopupOrder> TopupOrders { get; set; }
    public DbSet<Banner> Banners { get; set; }
    public DbSet<GameMetadata> GameMetadata { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<ExternalLogin> ExternalLogins { get; set; }
    public DbSet<Faq> Faqs { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<CustomerOtp> CustomerOtps { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<PaymentBank> PaymentBanks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure RowVersion as a concurrency token for optimistic locking.
        modelBuilder.Entity<TopupOrder>()
            .Property(o => o.RowVersion)
            .IsRowVersion();

        // Lookup OrderNo dipakai webhook Midtrans/Digiflazz, retry, refund,
        // dan sweep — jalur pembayaran yang paling sering mencari berdasarkan
        // nomor order. Unique: OrderNo adalah kunci korespondensi pembayaran
        // (order_id Midtrans, ref_id Digiflazz) — duplikat berarti pembayaran
        // bisa salah dialirkan ke order lain.
        modelBuilder.Entity<TopupOrder>()
            .HasIndex(o => o.OrderNo)
            .IsUnique();

        // Panjang eksplisit agar kolom bisa di-index (indexed string tidak
        // boleh longtext di MariaDB).
        modelBuilder.Entity<TopupOrder>()
            .Property(o => o.OrderNo)
            .HasMaxLength(255);

        // PublicHash = HMAC-SHA256(OrderNo) — hex 64 char, representasi URL
        // status publik. Unique: satu OrderNo satu hash (dan tabrakan hash
        // antar order tidak boleh terjadi). Nullable: baris lama sebelum
        // backfill startup; MariaDB memperlakukan NULL unik-semua.
        modelBuilder.Entity<TopupOrder>()
            .Property(o => o.PublicHash)
            .HasMaxLength(64);

        modelBuilder.Entity<TopupOrder>()
            .HasIndex(o => o.PublicHash)
            .IsUnique();

        modelBuilder.Entity<TopupOrder>()
            .Property(o => o.Status)
            .HasMaxLength(255);

        // Sweep worker (Status = Processing AND OrderDate <= cutoff) dan
        // dashboard chart (range OrderDate + Status Completed).
        modelBuilder.Entity<TopupOrder>()
            .HasIndex(o => new { o.Status, o.OrderDate });

        // CUSTOMER
        modelBuilder.Entity<Customer>(entity =>
        {
            // Email adalah identity lookup untuk login, registrasi, Google
            // auth, dan cek email — unique sesuai kontrak bisnis.
            entity.HasIndex(c => c.Email)
                .IsUnique();

            entity.Property(c => c.Email)
                .IsRequired()
                .HasMaxLength(255);
        });

        // EXTERNAL LOGIN (identitas provider OAuth — Facebook)
        modelBuilder.Entity<ExternalLogin>(entity =>
        {
            // Satu identitas provider = satu akun Digitalin. Lookup login
            // Facebook selalu lewat pasangan ini (provider user id), tidak
            // pernah lewat email — email yang sama di provider berbeda tetap
            // jadi akun terpisah (kebijakan no-linking).
            entity.HasIndex(e => new { e.Provider, e.ProviderUserId })
                .IsUnique();

            entity.Property(e => e.Provider)
                .IsRequired()
                .HasMaxLength(50);

            // Facebook user id numeric (panjang aman 255 — indexable di MySQL).
            entity.Property(e => e.ProviderUserId)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // FAQ — seed 17 record dari data FE lama (src/data/faqs.ts) dengan
        // nilai persis; EF HasData menuliskannya sebagai INSERT di migration.
        // Tidak ada config lain: string panjang (longtext) tanpa index —
        // tabel kecil, filter kategori/search tidak butuh index.
        modelBuilder.Entity<Faq>().HasData(SeedFaqs);

        // GAME PRODUCT
        modelBuilder.Entity<GameProduct>(entity =>
        {
            // Harga Produk (Contoh: 99,999,999.00)
            entity.Property(p => p.Price)
                .HasPrecision(18, 2);

            // Custom Margin keuntungan (Contoh: 5,000.00)
            entity.Property(p => p.CustomMargin)
                .HasPrecision(18, 2);

            // Satu SKU supplier = satu baris. Dedup di memori saat sync hanya
            // optimasi; index ini proteksi terakhir kalau dua sync jalan
            // bersamaan. Panjang dibatasi karena MySQL tidak bisa mengindeks
            // longtext tanpa key length (SKU Digiflazz terpanjang 11 karakter).
            entity.Property(p => p.BuyerSkuCode)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(p => p.BuyerSkuCode)
                .IsUnique();

            // Storefront: brands list, detail game, popular brands — semuanya
            // memfilter Brand (+ Category) dengan produk aktif. Panjang
            // eksplisit karena ketiga kolom ikut composite index.
            entity.HasIndex(p => new { p.Brand, p.Category, p.IsActive });

            entity.Property(p => p.Brand)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(p => p.Category)
                .IsRequired()
                .HasMaxLength(255);
        });

        // GAME METADATA
        modelBuilder.Entity<GameMetadata>(entity =>
        {
            entity.Property(m => m.Brand)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(m => m.Brand)
                .IsUnique();

            // Slug brand dipersist: URL & endpoint detail memakai kolom ini,
            // tidak ada sanitasi ulang di query. Unique agar dua brand tidak
            // pernah berbagi satu URL detail.
            entity.Property(m => m.BrandSlug)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(m => m.BrandSlug)
                .IsUnique();

            entity.Property(m => m.InputSchemaJson)
                .IsRequired()
                .HasColumnType("longtext");

            entity.Property(m => m.TargetTemplate)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(m => m.UsernameCheckProductId);

            // CSV slug kategori (multi-kategori per game) — lebih lega dari
            // satu slug, tapi tetap satu kolom.
            entity.Property(m => m.Categories)
                .HasMaxLength(255);
        });
        
        // TOPUP ORDER
        modelBuilder.Entity<TopupOrder>(entity =>
        {
            // Nominal Diskon Order
            entity.Property(o => o.DiscountAmount)
                .HasPrecision(18, 2);

            // Snapshot Harga
            entity.Property(o => o.OriginalPrice)
                .HasPrecision(18, 2);
            entity.Property(o => o.TotalPaid)
                .HasPrecision(18, 2);

            // Snapshot Modal & Profit
            entity.Property(o => o.CostPrice)
                .HasPrecision(18, 2);
            entity.Property(o => o.ProfitAmount)
                .HasPrecision(18, 2);

            // Core API: panjang eksplisit (bukan longtext) — payment_type
            // pendek dan transaction_id berformat UUID; keduanya berpotensi
            // dipakai filter lookup di masa depan.
            entity.Property(o => o.PaymentType)
                .HasMaxLength(50);
            entity.Property(o => o.MidtransTransactionId)
                .HasMaxLength(255);
        });

        // PAYMENT METHOD / PAYMENT BANK — konfigurasi checkout (ikon, urutan,
        // aktif). Seed idempotent via HasData (migration INSERT).
        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            // Code = alternate key (unique via principal key relasi bank),
            // sekaligus kunci lookup konfigurasi checkout.
            entity.Property(m => m.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(m => m.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(m => m.Description)
                .HasMaxLength(255);

            entity.HasData(SeedPaymentMethods);
        });

        modelBuilder.Entity<PaymentBank>(entity =>
        {
            entity.HasIndex(b => b.Code)
                .IsUnique();

            entity.Property(b => b.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(b => b.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            // Lookup bank per metode saat checkout + validasi order.
            entity.HasIndex(b => new { b.PaymentMethodCode, b.Code });

            entity.HasOne(b => b.PaymentMethod)
                .WithMany(m => m.Banks)
                .HasForeignKey(b => b.PaymentMethodCode)
                .HasPrincipalKey(m => m.Code)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(SeedPaymentBanks);
        });

        // CUSTOMER OTP
        modelBuilder.Entity<CustomerOtp>(entity =>
        {
            // One active OTP per customer per purpose (upsert overwrite logic)
            entity.HasIndex(o => new { o.CustomerId, o.Purpose }).IsUnique();

            entity.HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // VOUCHER 
        modelBuilder.Entity<Voucher>(entity =>
        {
            // Code harus unik
            entity.HasIndex(v => v.Code).IsUnique();

            // Nilai diskon (Contoh: 12.50 untuk 12.5% atau 5000.00 untuk FixedAmount)
            entity.Property(v => v.DiscountValue)
                .HasPrecision(18, 2);

            // Minimal Transaksi untuk klaim voucher
            entity.Property(v => v.MinimumTransaction)
                .HasPrecision(18, 2);

            // Maksimal potongan diskon voucher (khusus Percentage)
            entity.Property(v => v.MaximumDiscount)
                .HasPrecision(18, 2);
        });

        // ADMIN
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.Property(a => a.Username)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(a => a.PasswordHash)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(a => a.Role)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(a => a.Username)
                .IsUnique();
        });
    }

    /// <summary>
    /// Seed konfigurasi metode pembayaran — paritas kemampuan provider
    /// (AppConstants.MidtransPaymentMethods). Ikon sengaja NULL: admin
    /// mengisinya lewat upload UI (Cloudflare R2) di halaman Pembayaran.
    /// E-wallet non-Midtrans-ready di-seed NONAKTIF — aktifkan setelah kanal
    /// aktif di akun Midtrans (lihat komentar whitelist provider).
    /// Id stabil: baris existing tidak boleh bergeser Id (diff HasData
    /// menghasilkan delete+insert, dan delete PaymentMethods cascade ke
    /// PaymentBanks lewat FK Code — kolom Id 1-3 dipertahankan, metode baru
    /// menyusul di ekor; urutan tampil diatur SortOrder, bukan Id).
    /// </summary>
    private static readonly PaymentMethod[] SeedPaymentMethods =
    [
        new() { Id = 1, Code = "qris", DisplayName = "QRIS", Description = "Scan dari semua e-wallet & m-banking", IconUrl = null, Enabled = true, SortOrder = 1 },
        new() { Id = 2, Code = "gopay", DisplayName = "GoPay", Description = "Langsung buka aplikasi Gojek", IconUrl = null, Enabled = true, SortOrder = 2 },
        new() { Id = 3, Code = "bank_transfer", DisplayName = "Transfer Bank", Description = "Virtual Account otomatis", IconUrl = null, Enabled = true, SortOrder = 4 },
        new() { Id = 4, Code = "shopeepay", DisplayName = "ShopeePay", Description = "Langsung buka aplikasi Shopee", IconUrl = null, Enabled = true, SortOrder = 3 },
        new() { Id = 5, Code = "dana", DisplayName = "DANA", Description = "Langsung buka aplikasi DANA", IconUrl = null, Enabled = false, SortOrder = 5 },
        new() { Id = 6, Code = "linkaja", DisplayName = "LinkAja", Description = "Langsung buka aplikasi LinkAja", IconUrl = null, Enabled = false, SortOrder = 6 },
        new() { Id = 7, Code = "ovo", DisplayName = "OVO", Description = "Langsung buka aplikasi OVO", IconUrl = null, Enabled = false, SortOrder = 7 },
        new() { Id = 8, Code = "cstore", DisplayName = "Toko Retail", Description = "Bayar di kasir Indomaret / Alfamart", IconUrl = null, Enabled = true, SortOrder = 8 },
    ];

    /// <summary>
    /// Seed bank VA di bawah bank_transfer. bca/bni/bri/cimb/permata aktif
    /// (paritas whitelist provider). Mandiri aktif via kanal echannel
    /// (Mandiri Bill Payment) — provider memetakan pilihan "mandiri" ke
    /// payment_type echannel; lihat komentar VaBanks.
    /// </summary>
    private static readonly PaymentBank[] SeedPaymentBanks =
    [
        new() { Id = 1, Code = "bca", DisplayName = "BCA", IconUrl = null, Enabled = true, SortOrder = 1, PaymentMethodCode = "bank_transfer" },
        new() { Id = 2, Code = "bni", DisplayName = "BNI", IconUrl = null, Enabled = true, SortOrder = 2, PaymentMethodCode = "bank_transfer" },
        new() { Id = 3, Code = "bri", DisplayName = "BRI", IconUrl = null, Enabled = true, SortOrder = 3, PaymentMethodCode = "bank_transfer" },
        new() { Id = 4, Code = "cimb", DisplayName = "CIMB Niaga", IconUrl = null, Enabled = true, SortOrder = 4, PaymentMethodCode = "bank_transfer" },
        new() { Id = 5, Code = "permata", DisplayName = "Permata", IconUrl = null, Enabled = true, SortOrder = 5, PaymentMethodCode = "bank_transfer" },
        new() { Id = 6, Code = "mandiri", DisplayName = "Mandiri", IconUrl = null, Enabled = true, SortOrder = 6, PaymentMethodCode = "bank_transfer" },
        // BSI: VA asli via bank_transfer (verifikasi sandbox); hanya bisa
        // dibayar lewat aplikasi BYOND by BSI — tidak interbank. Nonaktif
        // default: aktifkan via admin bila demand terbukti.
        new() { Id = 7, Code = "bsi", DisplayName = "BSI", IconUrl = null, Enabled = false, SortOrder = 7, PaymentMethodCode = "bank_transfer" },
        // Sub-kanal cstore (toko) — reuse PaymentBanks: struktur pilihan
        // sub-kanal identik (code/nama/ikon/aktif/urutan).
        new() { Id = 8, Code = "indomaret", DisplayName = "Indomaret", IconUrl = null, Enabled = true, SortOrder = 1, PaymentMethodCode = "cstore" },
        new() { Id = 9, Code = "alfamart", DisplayName = "Alfamart", IconUrl = null, Enabled = true, SortOrder = 2, PaymentMethodCode = "cstore" },
    ];

    /// <summary>
    /// Seed FAQ — 17 record yang sebelumnya hardcode di FE (src/data/faqs.ts),
    /// nilai question/answer/category/featured dipertahankan persis. Urutan Id
    /// mengikuti urutan file lama. 6 record featured (topup-1, topup-2, topup-4,
    /// pay-1, tx-1, tx-2) = tepat batas maksimal unggulan.
    /// </summary>
    private static readonly Faq[] SeedFaqs =
    [
        new Faq { Id = 1, Question = "Bagaimana cara melakukan top up game?", Answer = "Pilih game yang ingin di-top up, masukkan Player ID, pilih nominal, lalu lakukan pembayaran. Proses otomatis setelah pembayaran berhasil.", Category = "Top Up", Featured = true },
        new Faq { Id = 2, Question = "Berapa lama proses top up?", Answer = "Proses top up umumnya instan (1-5 menit) setelah pembayaran terkonfirmasi. Beberapa produk mungkin membutuhkan waktu hingga 15 menit.", Category = "Top Up", Featured = true },
        new Faq { Id = 3, Question = "Apakah perlu membuat akun untuk top up?", Answer = "Tidak wajib. Anda bisa melakukan top up sebagai guest. Namun, dengan akun Anda bisa melihat riwayat transaksi.", Category = "Top Up", Featured = false },
        new Faq { Id = 4, Question = "Player ID salah, bisa dibatalkan?", Answer = "Jika sudah dibayar, top up tidak dapat dibatalkan. Pastikan Player ID benar sebelum melakukan pembayaran.", Category = "Top Up", Featured = true },
        new Faq { Id = 5, Question = "Metode pembayaran apa saja yang didukung?", Answer = "Kami mendukung berbagai metode pembayaran melalui Midtrans: GoPay, OVO, ShopeePay, transfer bank, kartu kredit, dan convenience store.", Category = "Pembayaran", Featured = true },
        new Faq { Id = 6, Question = "Apakah pembayaran aman?", Answer = "Ya. Seluruh transaksi diproses melalui Midtrans yang sudah terenkripsi dan tersertifikasi PCI DSS.", Category = "Pembayaran", Featured = false },
        new Faq { Id = 7, Question = "Bagaimana jika pembayaran gagal?", Answer = "Jika pembayaran gagal, Anda bisa mencoba kembali. Tidak ada biaya yang dikenakan untuk transaksi yang gagal.", Category = "Pembayaran", Featured = false },
        new Faq { Id = 8, Question = "Bagaimana cara mendaftar?", Answer = "Klik tombol 'Daftar' di navbar, masukkan email dan password, lalu verifikasi dengan kode OTP yang dikirim ke email Anda.", Category = "Akun", Featured = false },
        new Faq { Id = 9, Question = "Apakah bisa login dengan Google?", Answer = "Ya. Kami mendukung login menggunakan akun Google untuk kemudahan akses.", Category = "Akun", Featured = false },
        new Faq { Id = 10, Question = "Bagaimana cara mengubah password?", Answer = "Masuk ke halaman Profile, lalu pilih opsi ubah password. Masukkan password baru dan simpan.", Category = "Akun", Featured = false },
        new Faq { Id = 11, Question = "Bagaimana cara melihat riwayat transaksi?", Answer = "Masuk ke halaman Riwayat Order (jika memiliki akun) untuk melihat status seluruh transaksi Anda.", Category = "Transaksi", Featured = true },
        new Faq { Id = 12, Question = "Diamond belum masuk setelah pembayaran berhasil?", Answer = "Tunggu 5-15 menit. Jika masih belum masuk, hubungi kami melalui halaman Kontak dengan menyertakan nomor invoice.", Category = "Transaksi", Featured = true },
        new Faq { Id = 13, Question = "Apa arti status PENDING pada pesanan?", Answer = "Status PENDING berarti pembayaran belum terkonfirmasi. Selesaikan pembayaran Anda agar pesanan diproses.", Category = "Transaksi", Featured = false },
        new Faq { Id = 14, Question = "Bagaimana cara menggunakan kode voucher?", Answer = "Masukkan kode voucher pada kolom yang tersedia saat melakukan checkout. Diskon akan otomatis diterapkan.", Category = "Voucher", Featured = false },
        new Faq { Id = 15, Question = "Voucher tidak bisa digunakan?", Answer = "Pastikan kode voucher masih aktif, belum kedaluwarsa, dan memenuhi minimum transaksi. Jika masih bermasalah, hubungi support.", Category = "Voucher", Featured = false },
        new Faq { Id = 16, Question = "Apakah Digitalin aman?", Answer = "Ya. Digitalin menggunakan enkripsi SSL, pembayaran diproses via Midtrans (PCI DSS certified), dan data Anda dilindungi.", Category = "Lainnya", Featured = false },
        new Faq { Id = 17, Question = "Apakah bisa refund?", Answer = "Refund hanya dapat dilakukan jika pesanan gagal diproses oleh sistem. Hubungi tim support kami untuk pengajuan refund.", Category = "Lainnya", Featured = false }
    ];
}
