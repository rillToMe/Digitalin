using be_digitalin.Constants;
using be_digitalin.DTOs.Requests;
using Microsoft.AspNetCore.Mvc;
using be_digitalin.Services.Commerce;

namespace be_digitalin.Endpoints;

public static class VoucherEndpoints
{
    public static void MapVoucherEndpoints(this IEndpointRouteBuilder app)
    {
        // AREA ADMIN
        var adminGroup = app.MapGroup("/admin/vouchers")
                            .WithTags("Manajemen Voucher (Admin)")
                            .RequireAuthorization("AdminOnly");

        // GET /admin/vouchers — List with pagination, search, filter, sort
        adminGroup.MapGet("/", async (
            [FromServices] IVoucherService voucherService,
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20) =>
        {
            page = Math.Max(1, page);
            limit = Math.Clamp(limit, 1, 100);

            var result = await voucherService.GetVouchersAsync(search, status, sortBy, sortOrder, page, limit);
            return Results.Ok(result);
        });

        // GET /admin/vouchers/{id} — Detail
        adminGroup.MapGet("/{id}", async (
            int id,
            [FromServices] IVoucherService voucherService) =>
        {
            var voucher = await voucherService.GetVoucherByIdAsync(id);
            if (voucher == null)
                return Results.NotFound(new { message = "Voucher tidak ditemukan." });

            return Results.Ok(voucher);
        });

        // POST /admin/vouchers — Create
        adminGroup.MapPost("/", async (
            [FromBody] CreateVoucherRequest request,
            [FromServices] IVoucherService voucherService) =>
        {
            try
            {
                var voucher = await voucherService.CreateVoucherAsync(request);
                return Results.Created($"{AppConstants.Api.Prefix}/admin/vouchers/{voucher.Id}", voucher);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // PUT /admin/vouchers/{id} — Full update
        adminGroup.MapPut("/{id}", async (
            int id,
            [FromBody] UpdateVoucherRequest request,
            [FromServices] IVoucherService voucherService) =>
        {
            try
            {
                var voucher = await voucherService.UpdateVoucherAsync(id, request);
                if (voucher == null)
                    return Results.NotFound(new { message = "Voucher tidak ditemukan." });

                return Results.Ok(voucher);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        // PATCH /admin/vouchers/{id}/status — Enable/Disable
        adminGroup.MapPatch("/{id}/status", async (
            int id,
            [FromBody] UpdateVoucherStatusRequest request,
            [FromServices] IVoucherService voucherService) =>
        {
            var result = await voucherService.UpdateVoucherStatusAsync(id, request);
            if (result == null)
                return Results.NotFound(new { message = "Voucher tidak ditemukan." });

            return Results.Ok(new { message = "Status voucher berhasil diperbarui.", isActive = result.Value });
        });

        // DELETE /admin/vouchers/{id} — Delete (blocked if used)
        adminGroup.MapDelete("/{id}", async (
            int id,
            [FromServices] IVoucherService voucherService) =>
        {
            var (success, errorMessage) = await voucherService.DeleteVoucherAsync(id);

            if (!success)
            {
                // 404 if not found, 409 if has usage
                if (errorMessage == "Voucher tidak ditemukan.")
                    return Results.NotFound(new { message = errorMessage });

                return Results.Conflict(new { message = errorMessage });
            }

            return Results.Ok(new { message = "Voucher berhasil dihapus." });
        });

        // GET /admin/vouchers/{id}/usage — Usage history
        adminGroup.MapGet("/{id}/usage", async (
            int id,
            [FromServices] IVoucherService voucherService,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20) =>
        {
            page = Math.Max(1, page);
            limit = Math.Clamp(limit, 1, 100);

            try
            {
                var result = await voucherService.GetVoucherUsageAsync(id, page, limit);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });
    }
}
