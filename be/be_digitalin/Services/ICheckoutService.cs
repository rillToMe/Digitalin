using be_digitalin.DTOs;

namespace be_digitalin.Services;

public interface ICheckoutService
{
    Task<CheckoutPreviewResponse> PreviewAsync(CheckoutPreviewRequest request);
}
