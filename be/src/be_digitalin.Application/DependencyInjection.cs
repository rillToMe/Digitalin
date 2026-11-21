using be_digitalin.Services.Admin;
using be_digitalin.Services.Auth;
using be_digitalin.Services.Commerce;
using be_digitalin.Services.Content;
using be_digitalin.Services.Customer;
using be_digitalin.Services.Games;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Payments;
using be_digitalin.Services.Security;
using Microsoft.Extensions.DependencyInjection;

namespace be_digitalin.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Auth & Customers
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IFacebookAuthService, FacebookAuthService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IRecaptchaService, RecaptchaService>();

        // Catalog, Search, Vouchers & Payment Methods
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IVoucherService, VoucherService>();
        services.AddScoped<IPaymentMethodService, PaymentMethodService>();

        // Checkout, Orders, Invoices & Leaderboards
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<IInvoiceLookupService, InvoiceLookupService>();

        // Administration & Content Management
        services.AddScoped<IBannerService, BannerService>();
        services.AddScoped<IFaqService, FaqService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAdminManagementService, AdminManagementService>();

        return services;
    }
}
