using be_digitalin.DTOs;

namespace be_digitalin.Services.Orders;

public interface ICheckoutService
{
    Task<CheckoutPreviewResponse> PreviewAsync(CheckoutPreviewRequest request);
}
