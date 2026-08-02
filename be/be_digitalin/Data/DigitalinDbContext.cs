using be_digitalin.Models;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Data;

public class DigitalinDbContext : DbContext
{
    public DigitalinDbContext(DbContextOptions<DigitalinDbContext> options) : base(options) { }
    
    public DbSet<GameProduct> GameProducts { get; set; }
    public DbSet<TopupOrder> TopupOrders { get; set; }
    public DbSet<Banner> Banners { get; set; }
    public DbSet<GameMetadata> GameMetadata { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<AppSetting> AppSettings { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<CustomerOtp> CustomerOtps { get; set; }
    public DbSet<Admin> Admins { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure RowVersion as a concurrency token for optimistic locking.
        modelBuilder.Entity<TopupOrder>()
            .Property(o => o.RowVersion)
            .IsRowVersion();

        // GAME PRODUCT
        modelBuilder.Entity<GameProduct>(entity =>
        {
            // Harga Produk (Contoh: 99,999,999.00)
            entity.Property(p => p.Price)
                .HasPrecision(18, 2);

            // Custom Margin keuntungan (Contoh: 5,000.00)
            entity.Property(p => p.CustomMargin)
                .HasPrecision(18, 2);
        });

        // GAME METADATA
        modelBuilder.Entity<GameMetadata>(entity =>
        {
            entity.Property(m => m.Brand)
                .IsRequired()
                .HasMaxLength(255);

            entity.HasIndex(m => m.Brand)
                .IsUnique();

            entity.Property(m => m.InputSchemaJson)
                .IsRequired()
                .HasColumnType("longtext");

            entity.Property(m => m.TargetTemplate)
                .IsRequired()
                .HasMaxLength(500);

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
}