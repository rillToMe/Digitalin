using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using be_digitalin.Config;
using be_digitalin.Data;
using be_digitalin.Constants;
using Microsoft.Extensions.Options;
using be_digitalin.DTOs;
using be_digitalin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc; // <-- Ditambahkan untuk [FromBody] dan [FromServices]
using Microsoft.EntityFrameworkCore;

namespace be_digitalin.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // AREA PUBLIK (Checkout Pembeli)
        var publicGroup = app.MapGroup("/orders")
                             .WithTags("Transaksi & Checkout (Public)");

        publicGroup.MapPost("/", async (
            [FromBody] CreateOrderRequest req, 
            [FromServices] IOrderService orderService,
            ClaimsPrincipal user,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("PlaceOrder");
            try {
                int? customerId = null;
                
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int parsedId))
                {
                    customerId = parsedId;
                }
                var result = await orderService.PlaceOrderAsync(req, customerId);
                return Results.Ok(result);
            } catch (Exception ex) {
                // [M10] Only return business-logic messages (thrown intentionally by the
                // service layer). Hide unexpected/system exceptions from the client.
                logger.LogWarning(ex, "[PlaceOrder] Failed for request.");
                var message = ex is InvalidOperationException || ex.Message.Length < 100
                    ? ex.Message
                    : "Terjadi kesalahan saat memproses pesanan Anda.";
                return Results.BadRequest(new { message });
            }
        });
        
        // GET /orders/my-history -> Riwayat Pribadi (WAJIB LOGIN)
        // GET /orders/my-history -> Riwayat Pribadi (WAJIB LOGIN)
        publicGroup.MapGet("/my-history", async (
            [FromQuery] int? page, 
            [FromQuery] int? limit,
            ClaimsPrincipal user, 
            ICustomerService service) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null) return Results.Unauthorized();

            // Set nilai default pagination
            int currentPage = page ?? 1;
            int currentLimit = limit ?? 20;

            var history = await service.GetCustomerHistoryAsync(int.Parse(userIdClaim.Value), currentPage, currentLimit);
            return Results.Ok(history);
        }).RequireAuthorization(); // Gembok aktif


        //. AREA ADMIN (Manajemen Pesanan)
        var adminGroup = app.MapGroup("/admin/orders")
                            .WithTags("Manajemen Pesanan (Admin)")
                            .RequireAuthorization("AdminOnly"); // <-- Wajib role ADMIN

        adminGroup.MapGet("/", async ([FromServices] IOrderService orderService) => 
            Results.Ok(await orderService.GetAllOrdersAsync()));
        
        // ── POST /admin/orders/{orderNo}/retry ──────────────────────────────
        // [C3 fix] Before retrying, query Digiflazz upstream status first so we
        // never double-fulfill an order that Digiflazz already processed.
        // [C2 fix] Optimistic concurrency via RowVersion + DbUpdateConcurrencyException.
        adminGroup.MapPost("/{orderNo}/retry", async (
            string orderNo,
            [FromServices] DigitalinDbContext db,
            [FromServices] be_digitalin.Providers.DigiflazzProvider digiflazzProvider,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("OrderRetry");

            // 1. Cari data order beserta produknya
            var order = await db.TopupOrders
                .Include(o => o.GameProduct)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null) 
                return Results.NotFound(new { message = "Pesanan tidak ditemukan" });

            // 2. Proteksi Keamanan: Hanya boleh di-retry jika statusnya NEEDS_ACTION (nyangkut)
            if (order.Status != AppConstants.OrderStatus.NeedsAction)
                return Results.BadRequest(new { message = $"Tidak bisa retry. Status pesanan saat ini adalah: {order.Status}" });

            try
            {
                // ── [C3] Check upstream Digiflazz status BEFORE firing another Topup ──
                // If the original request actually succeeded server-side (but the response
                // was lost), calling TopupAsync again would double-fulfill.
                try
                {
                    var (upstreamStatus, upstreamSn, _) = await digiflazzProvider.CheckStatusAsync(
                        order.GameProduct!.BuyerSkuCode, order.PlayerId, order.OrderNo);

                    if (upstreamStatus == AppConstants.DigiflazzStatus.Sukses)
                    {
                        // Digiflazz already processed this — just persist the SN.
                        order.Status = AppConstants.OrderStatus.Completed;
                        order.Sn = upstreamSn;
                        await db.SaveChangesAsync();
                        return Results.Ok(new { message = "Order sudah selesai di Digiflazz sebelumnya. SN berhasil disimpan.", status = order.Status });
                    }
                    // If upstream says Gagal, we can safely retry. If Pending, we should wait.
                    if (upstreamStatus == AppConstants.DigiflazzStatus.Pending)
                    {
                        return Results.BadRequest(new { message = "Digiflazz masih memproses pesanan ini. Harap tunggu sebelum mencoba retry." });
                    }
                }
                catch
                {
                    // If CheckStatus itself fails, proceed with retry cautiously.
                    logger.LogWarning("[RETRY] CheckStatus failed for Order {OrderNo}. Proceeding with retry.", order.OrderNo);
                }

                // 3. Ubah status ke PROCESSING dulu
                order.Status = AppConstants.OrderStatus.Processing;
                await db.SaveChangesAsync();

                // 4. Tembak ulang API Digiflazz
                string? sn = await digiflazzProvider.TopupAsync(order.GameProduct!.BuyerSkuCode, order.PlayerId, order.OrderNo);
                
                // 5. Jika entah kenapa Digiflazz ngasih SN secara instan saat itu juga
                if (!string.IsNullOrEmpty(sn))
                {
                    order.Sn = sn;
                    order.Status = AppConstants.OrderStatus.Completed;
                }

                await db.SaveChangesAsync();
                return Results.Ok(new { 
                    message = "Retry berhasil dikirim ke Digiflazz! Silakan tunggu SN masuk.", 
                    status = order.Status 
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                // [C2] Another process (webhook / sweep worker) modified the row between
                // our read and write. Return a conflict response.
                logger.LogWarning("[RETRY] Concurrency conflict on Order {OrderNo}. Retry was aborted.", order.OrderNo);
                return Results.Conflict(new { message = "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat." });
            }
            catch (Exception ex)
            {
                // [M10] Don't leak ex.Message — log internally, return generic error
                logger.LogError(ex, "[RETRY] Unexpected error on Order {OrderNo}", order.OrderNo);
                order.Status = AppConstants.OrderStatus.NeedsAction;
                try { await db.SaveChangesAsync(); } catch (DbUpdateConcurrencyException) { /* already handled elsewhere */ }
                
                return Results.BadRequest(new { 
                    message = "Retry ke Digiflazz gagal. Silakan coba lagi atau hubungi tim teknis." 
                });
            }
        });
        
        // POST /admin/orders/{orderNo}/refund -> Ubah status jadi Refunded (Manual)
        adminGroup.MapPost("/{orderNo}/refund", async (
            string orderNo,
            [FromServices] DigitalinDbContext db) =>
        {
            Console.WriteLine($"\n[REFUND REQUEST] Meminta pengembalian dana manual untuk Order: {orderNo}");

            try 
            {
                var order = await db.TopupOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);

                if (order == null) 
                {
                    Console.WriteLine($"[REFUND WARNING] Gagal: Order {orderNo} tidak ditemukan di database.");
                    return Results.NotFound(new { message = "Pesanan tidak ditemukan" });
                }

                // Cuma boleh nge-refund pesanan yang memang bermasalah / nyangkut
                if (order.Status != AppConstants.OrderStatus.NeedsAction)
                {
                    Console.WriteLine($"[REFUND WARNING] Ditolak: Order {orderNo} berstatus '{order.Status}'. Hanya status '{AppConstants.OrderStatus.NeedsAction}' yang bisa di-refund.");
                    return Results.BadRequest(new { message = $"Tidak bisa di-refund. Status pesanan saat ini: {order.Status}" });
                }

                // Ubah statusnya
                order.Status = AppConstants.OrderStatus.Refunded;
                await db.SaveChangesAsync();

                Console.WriteLine($"[REFUND SUCCESS] Order {orderNo} berhasil diubah menjadi REFUNDED.");

                return Results.Ok(new { 
                    message = "Pesanan berhasil ditandai sebagai REFUNDED. Segera hubungi pembeli untuk transfer pengembalian dana secara manual via WA.", 
                    status = order.Status 
                });
            }
            catch (DbUpdateConcurrencyException)
            {
                Console.WriteLine($"[REFUND CONCURRENCY] Order {orderNo} diubah oleh proses lain sebelum refund disimpan.");
                return Results.Conflict(new { message = "Pesanan sedang diproses oleh sistem lain. Coba lagi beberapa saat." });
            }
            catch (Exception ex)
            {
                // Jaga-jaga kalau database tiba-tiba putus koneksi pas lagi nge-save
                Console.WriteLine($"[REFUND CRITICAL ERROR] Sistem gagal mengeksekusi refund untuk Order {orderNo}: {ex.Message}");
                return Results.Problem("Terjadi kesalahan internal saat memproses refund.");
            }
        });
        
        // AREA WEBHOOK (Sistem Integrasi Antar Server)
        var webhookGroup = app.MapGroup("/webhooks")
                              .WithTags("Webhooks (Sistem Integrasi)")
                              .AllowAnonymous();

        // Webhook Midtrans
        webhookGroup.MapPost("/midtrans", async ([FromBody] MidtransNotificationRequest req, [FromServices] IOrderService orderService) =>
        {
            try
            {
                await orderService.HandleNotificationAsync(req);
                return Results.Ok(new { message = "Notification received" });
            }
            catch (Exception)
            {
                // [M10] Don't leak internal error details to the webhook caller (Midtrans).
                // Return a generic 500 so Midtrans retries later.
                return Results.Problem("Internal server error while processing notification.");
            }
        });
        
        // webhookGroup.MapGet("/midtrans", () => "Pintu Webhook Midtrans sudah SIAP! (Tapi Midtrans pakai POST ya)");
        
        // ── Webhook Digiflazz ─────────────────────────────────────────────────
        // [C2 fix] Added optimistic concurrency handling — if a webhook and the
        // sweep worker / admin retry write to the same row, the second writer
        // gets a DbUpdateConcurrencyException which we catch and log.
        webhookGroup.MapPost("/digiflazz", async (
            HttpRequest req, 
            [FromServices] DigitalinDbContext db, 
            [FromServices] be_digitalin.Providers.DigiflazzProvider digiflazzProvider,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("DigiflazzWebhook");
            logger.LogInformation("[WEBHOOK] Ada panggilan masuk dari Digiflazz...");
            
            using var reader = new StreamReader(req.Body);
            var rawBody = await reader.ReadToEndAsync();
            var signatureHeader = req.Headers[AppConstants.HttpHeaders.XHubSignature].ToString();

            // Validasi Keamanan pakai Provider (Hanya 1 baris!)
            if (!digiflazzProvider.ValidateWebhookSignature(rawBody, signatureHeader))
            {
                logger.LogWarning("[WEBHOOK] Ditolak: Signature tidak valid!");
                return Results.Unauthorized();
            }

            var payload = JsonSerializer.Deserialize<DigiflazzWebhookPayload>(
                rawBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
    
            if (payload?.data == null) return Results.Ok();
            
            logger.LogInformation("[WEBHOOK] Pesanan: {RefId} | Status: {Status} | SN: {SN}",
                payload.data.ref_id, payload.data.status, payload.data.sn);

            var order = await db.TopupOrders.FirstOrDefaultAsync(o => o.OrderNo == payload.data.ref_id);
            if (order != null)
            {
                if (payload.data.status == AppConstants.DigiflazzStatus.Sukses)
                {
                    // Idempotency: only update if not already COMPLETED
                    if (order.Status == AppConstants.OrderStatus.Completed)
                    {
                        logger.LogWarning("[WEBHOOK] Duplicate success webhook for {OrderNo}. Already COMPLETED — ignored.", order.OrderNo);
                        return Results.Ok(new { message = "Webhook Digiflazz received (already processed)" });
                    }
                    order.Sn = payload.data.sn;
                    order.Status = AppConstants.OrderStatus.Completed;
                    logger.LogInformation("[WEBHOOK] SUCCESS! SN berhasil ditanam ke database");
                }
                else if (payload.data.status == AppConstants.DigiflazzStatus.Gagal)
                {
                    // INI YANG PALING PENTING! Uang udah di tangan kita, tapi Digiflazz gagal proses
                    order.Status = AppConstants.OrderStatus.NeedsAction;
                    logger.LogWarning("[WEBHOOK CRITICAL] DIGIFLAZZ GAGAL! Order: {OrderNo} butuh penanganan manual.", order.OrderNo);
                }

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    logger.LogWarning(
                        "[WEBHOOK] Concurrency conflict on Order {OrderNo}. Another process already updated it.",
                        order.OrderNo);
                }
            }

            return Results.Ok(new { message = "Webhook Digiflazz received" });
        });
    }
}
