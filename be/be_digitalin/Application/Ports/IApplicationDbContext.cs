using be_digitalin.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace be_digitalin.Application.Ports;

/// <summary>
/// Pragmatic persistence port — interface EF-typed (Jason Taylor style).
///
/// Application (services) bergantung pada interface ini, BUKAN pada
/// <c>DigitalinDbContext</c> concrete (menghilangkan P1). Anggota di sini adalah
/// kebutuhan aktual yang terverifikasi:
///   - DbSet per entity (LINQ di seluruh service)
///   - SaveChangesAsync (semua service penulis)
///   - Database.BeginTransactionAsync (OrderService: voucher claim + proses order)
///   - Entry(...).ReloadAsync / Entry(...).State (OrderService concurrency,
///     GoogleAuthService detach)
///
/// Trade-off disepakati: Application mereferensikan package Microsoft.EntityFrameworkCore,
/// tetapi Domain tetap murni. Jangan jadikan repository — lihat blueprint §6–7.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<GameProduct> GameProducts { get; }

    DbSet<TopupOrder> TopupOrders { get; }

    DbSet<Banner> Banners { get; }

    DbSet<GameMetadata> GameMetadata { get; }

    DbSet<Customer> Customers { get; }

    DbSet<ExternalLogin> ExternalLogins { get; }

    DbSet<Faq> Faqs { get; }

    DbSet<AppSetting> AppSettings { get; }

    DbSet<Voucher> Vouchers { get; }

    DbSet<CustomerOtp> CustomerOtps { get; }

    DbSet<Admin> Admins { get; }

    DbSet<PaymentMethod> PaymentMethods { get; }

    DbSet<PaymentBank> PaymentBanks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DatabaseFacade Database { get; }

    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
}