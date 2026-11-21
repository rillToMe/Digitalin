using be_digitalin.Application.Ports;
using be_digitalin.DTOs;
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Use case pembacaan riwayat transaksi customer maupun admin (berbasis snapshot).
/// </summary>
public sealed class OrderQueryService
{
    private readonly IApplicationDbContext _db;

    public OrderQueryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<OrderResponse>> GetOrdersAsync(
        int? customerId = null,
        int page = 1,
        int limit = 20,
        string? status = null)
    {
        var query = _db.TopupOrders.AsNoTracking().AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == customerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            string upperStatus = status.ToUpper();
            query = query.Where(o => o.Status == upperStatus);
        }

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)limit);

        var rows = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(o => new
            {
                o.Id, o.OrderNo, o.PublicHash, o.PlayerId, o.Status, o.ProductName,
                o.OriginalPrice, o.OrderDate, o.Sn,
                o.ImageUrl, o.DiscountAmount, o.TotalPaid,
                o.PaymentType, o.PaymentInstructionsJson
            })
            .ToListAsync();

        var data = rows
            .Select(o => new OrderResponse(
                o.Id, o.OrderNo ?? "-", o.PlayerId, o.Status, o.ProductName,
                o.OriginalPrice, o.OrderDate, o.Sn,
                o.ImageUrl, o.DiscountAmount, o.TotalPaid,
                o.PaymentType, OrderPaymentInstructionsCodec.Deserialize(o.PaymentInstructionsJson),
                o.PublicHash
            ))
            .ToList();

        return new PagedResult<OrderResponse>(data, totalItems, totalPages, page);
    }
}
