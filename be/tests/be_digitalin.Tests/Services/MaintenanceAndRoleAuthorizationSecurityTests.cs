using System.Reflection;
using System.Security.Claims;
using be_digitalin.Application.Validation;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Controllers;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.DTOs.Requests;
using be_digitalin.Middleware;
using be_digitalin.Services.Customer;
using be_digitalin.Services.Games;
using be_digitalin.Services.Orders;
using be_digitalin.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using CustomerEntity = be_digitalin.Domain.Entities.Customer;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Regression tests untuk Phase 2A:
/// - CRIT-01: MaintenanceMiddleware mengizinkan POST /webhooks/midtrans dan POST /webhooks/digiflazz
///   selama maintenance mode aktif, namun tetap memblokir metode non-POST, path webhook lain, dan storefront.
/// - HIGH-04: MaintenanceMiddleware mengenali ADMIN maupun SUPER_ADMIN saat AllowAdmin = true;
///   endpoint customer (AuthController.GetProfile/UpdateProfile, OrderController.MyHistory/PlaceOrder,
///   CatalogController.GetRecentBrands, PublicInvoiceController.GetInvoice) wajib memisahkan
///   Role=CUSTOMER dari Role=ADMIN / Role=SUPER_ADMIN (anti cross-role IDOR).
/// </summary>
public class MaintenanceAndRoleAuthorizationSecurityTests
{
    private sealed class StubMaintenanceSettings : IAppSettingsService
    {
        public bool MaintenanceEnabled { get; set; } = true;
        public bool AllowAdmin { get; set; } = true;

        public Task<string?> GetAsync(string key, string? defaultValue = null) => Task.FromResult(defaultValue);
        public Task SetAsync(string key, string value) => Task.CompletedTask;
        public Task<int> GetMidtransExpiryMinutesAsync() => Task.FromResult(30);
        public Task<bool> IsMaintenanceEnabledAsync() => Task.FromResult(MaintenanceEnabled);
        public Task<MaintenanceConfig> GetMaintenanceConfigAsync() =>
            Task.FromResult(new MaintenanceConfig(
                MaintenanceEnabled,
                "Maintenance",
                "Sistem sedang dalam pemeliharaan",
                null,
                AllowAdmin));
        public Task UpdateMaintenanceConfigAsync(MaintenanceConfig config) => Task.CompletedTask;
    }

    private static ClaimsPrincipal CreatePrincipal(int id, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestJwt"));
    }

    private static async Task<(int StatusCode, bool NextCalled)> InvokeMaintenanceAsync(
        string method,
        string path,
        StubMaintenanceSettings settings,
        ClaimsPrincipal? user = null)
    {
        bool nextCalled = false;
        var middleware = new MaintenanceMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (user != null)
        {
            context.User = user;
        }

        await middleware.InvokeAsync(context, settings);
        return (context.Response.StatusCode, nextCalled);
    }

    // ── CRIT-01: Webhook Allowlist During Maintenance ─────────────────

    [Theory]
    [InlineData("POST", "/webhooks/midtrans")]
    [InlineData("POST", "/webhooks/digiflazz")]
    [InlineData("POST", "/webhooks/midtrans/")]
    [InlineData("POST", "/webhooks/digiflazz/")]
    public async Task MaintenanceMiddleware_WhenEnabled_AllowsLegitimateWebhookPosts(string method, string path)
    {
        var settings = new StubMaintenanceSettings { MaintenanceEnabled = true, AllowAdmin = false };

        var (statusCode, nextCalled) = await InvokeMaintenanceAsync(method, path, settings);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, statusCode);
    }

    [Theory]
    [InlineData("GET", "/webhooks/midtrans")]
    [InlineData("GET", "/webhooks/digiflazz")]
    [InlineData("PUT", "/webhooks/midtrans")]
    [InlineData("POST", "/webhooks/other")]
    [InlineData("POST", "/webhooks")]
    [InlineData("GET", "/api/v1/games/brands")]
    [InlineData("POST", "/api/v1/orders")]
    public async Task MaintenanceMiddleware_WhenEnabled_BlocksNonPostWebhooks_UnknownWebhooks_AndStorefront(
        string method, string path)
    {
        var settings = new StubMaintenanceSettings { MaintenanceEnabled = true, AllowAdmin = true };

        var (statusCode, nextCalled) = await InvokeMaintenanceAsync(method, path, settings);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
    }

    // ── HIGH-04: MaintenanceMiddleware Admin & SuperAdmin Recognition ──

    [Theory]
    [InlineData(AppConstants.Roles.Admin, true, true)]
    [InlineData(AppConstants.Roles.SuperAdmin, true, true)]
    [InlineData(AppConstants.Roles.Customer, true, false)]
    [InlineData(AppConstants.Roles.Admin, false, false)]
    [InlineData(AppConstants.Roles.SuperAdmin, false, false)]
    public async Task MaintenanceMiddleware_AdminAndSuperAdminBypass_HonorsRoleAndAllowAdminFlag(
        string role, bool allowAdmin, bool expectedAllowed)
    {
        var settings = new StubMaintenanceSettings
        {
            MaintenanceEnabled = true,
            AllowAdmin = allowAdmin
        };
        var user = CreatePrincipal(1, role);

        var (statusCode, nextCalled) = await InvokeMaintenanceAsync(
            "GET", "/api/v1/games/brands", settings, user);

        Assert.Equal(expectedAllowed, nextCalled);
        Assert.Equal(
            expectedAllowed ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable,
            statusCode);
    }

    // ── HIGH-04: Customer Endpoints Enforce Role=CUSTOMER ─────────────

    [Fact]
    public void CustomerProtectedEndpoints_DeclareExplicitCustomerRoleAuthorizeAttribute()
    {
        var getProfileAuth = typeof(AuthController)
            .GetMethod(nameof(AuthController.GetProfile))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var updateProfileAuth = typeof(AuthController)
            .GetMethod(nameof(AuthController.UpdateProfile))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var myHistoryAuth = typeof(OrderController)
            .GetMethod(nameof(OrderController.MyHistory))!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(getProfileAuth);
        Assert.Equal(AppConstants.Roles.Customer, getProfileAuth!.Roles);

        Assert.NotNull(updateProfileAuth);
        Assert.Equal(AppConstants.Roles.Customer, updateProfileAuth!.Roles);

        Assert.NotNull(myHistoryAuth);
        Assert.Equal(AppConstants.Roles.Customer, myHistoryAuth!.Roles);
    }

    private sealed class StubCustomerService : ICustomerService
    {
        public int? LastRequestedProfileId { get; private set; }
        public int? LastUpdatedProfileId { get; private set; }
        public int? LastHistoryCustomerId { get; private set; }

        public Task<CustomerEntity?> GetProfileAsync(int id)
        {
            LastRequestedProfileId = id;
            return Task.FromResult<CustomerEntity?>(new CustomerEntity
            {
                Id = id,
                Email = "customer1@example.com",
                FullName = "Customer One",
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<bool> UpdateProfileAsync(int id, string fullName, string? newPassword)
        {
            LastUpdatedProfileId = id;
            return Task.FromResult(true);
        }

        public Task<PagedResult<OrderResponse>> GetCustomerHistoryAsync(int customerId, int page = 1, int limit = 20)
        {
            LastHistoryCustomerId = customerId;
            return Task.FromResult(new PagedResult<OrderResponse>([], 0, 0, page));
        }

        public Task<string?> RegisterAsync(string email, string password, string fullName, string otpCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<(bool Success, string Message)> RequestRegistrationOtpAsync(string email, string captchaToken, string? remoteIp = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<string?> LoginAsync(string email, string password, string captchaToken, string? remoteIp = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> CheckEmailExistsAsync(string email) =>
            throw new NotSupportedException();
    }

    [Theory]
    [InlineData(AppConstants.Roles.Admin)]
    [InlineData(AppConstants.Roles.SuperAdmin)]
    public async Task AuthController_GetAndUpdateProfile_ForbidAdminRoles_PreventingCrossRoleIdor(string nonCustomerRole)
    {
        var customerService = new StubCustomerService();
        var controller = new AuthController(
            customerService,
            null!,
            null!,
            null!,
            Options.Create(new FacebookAuthSettings()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(1, nonCustomerRole) }
            }
        };

        var getResult = await controller.GetProfile();
        var updateResult = await controller.UpdateProfile(new UpdateProfileRequest("Hacked Name", null));

        Assert.IsType<ForbidResult>(getResult);
        Assert.IsType<ForbidResult>(updateResult);
        Assert.Null(customerService.LastRequestedProfileId);
        Assert.Null(customerService.LastUpdatedProfileId);
    }

    [Fact]
    public async Task AuthController_GetAndUpdateProfile_AllowsCustomerRole()
    {
        var customerService = new StubCustomerService();
        var controller = new AuthController(
            customerService,
            null!,
            null!,
            null!,
            Options.Create(new FacebookAuthSettings()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(7, AppConstants.Roles.Customer) }
            }
        };

        var getResult = await controller.GetProfile();
        var updateResult = await controller.UpdateProfile(new UpdateProfileRequest("Customer Seven", null));

        Assert.IsType<OkObjectResult>(getResult);
        Assert.IsType<OkObjectResult>(updateResult);
        Assert.Equal(7, customerService.LastRequestedProfileId);
        Assert.Equal(7, customerService.LastUpdatedProfileId);
    }

    private sealed class StubOrderService : IOrderService
    {
        public int? LastPlacedCustomerId { get; private set; }

        public Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, int? customerId = null)
        {
            LastPlacedCustomerId = customerId;
            return Task.FromResult(new OrderResponse(
                1, "INV-1", "123", AppConstants.OrderStatus.Pending, "Test", 10000m,
                DateTime.UtcNow, null, null, 0m, 10000m, "qris", null, "hash"));
        }

        public Task<PagedResult<OrderResponse>> GetAllOrdersAsync(int page = 1, int limit = 20, string? status = null) =>
            throw new NotSupportedException();
        public Task<PagedResult<OrderResponse>> GetOrdersAsync(int? customerId = null, int page = 1, int limit = 20, string? status = null) =>
            throw new NotSupportedException();
        public Task<bool> HandleNotificationAsync(MidtransNotificationRequest notification) =>
            throw new NotSupportedException();
        public Task<OrderActionResult> RetryOrderAsync(string orderNo) =>
            throw new NotSupportedException();
        public Task<OrderActionResult> RefundOrderAsync(string orderNo) =>
            throw new NotSupportedException();
        public Task<string> HandleDigiflazzWebhookAsync(DigiflazzWebhookData data) =>
            throw new NotSupportedException();
        public Task SweepStuckOrdersAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Theory]
    [InlineData(AppConstants.Roles.Admin)]
    [InlineData(AppConstants.Roles.SuperAdmin)]
    public async Task OrderController_MyHistory_ForbidsAdminRoles_And_PlaceOrder_IgnoresAdminId(string nonCustomerRole)
    {
        var orderService = new StubOrderService();
        var customerService = new StubCustomerService();
        var controller = new OrderController(
            orderService,
            null!,
            customerService,
            NullLoggerFactory.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(1, nonCustomerRole) }
            }
        };

        var historyResult = await controller.MyHistory(1, 20);
        Assert.IsType<ForbidResult>(historyResult.Result);
        Assert.Null(customerService.LastHistoryCustomerId);

        var placeResult = await controller.PlaceOrder(
            new CreateOrderRequest(1, new Dictionary<string, string?> { ["userId"] = "123" }, PaymentMethod: "qris"));
        Assert.IsType<OkObjectResult>(placeResult);
        Assert.Null(orderService.LastPlacedCustomerId); // Admin tidak boleh diatribusikan ke Customers.Id = 1
    }

    [Fact]
    public async Task OrderController_PlaceOrder_ExtractsCustomerId_WhenRoleIsCustomer()
    {
        var orderService = new StubOrderService();
        var customerService = new StubCustomerService();
        var controller = new OrderController(
            orderService,
            null!,
            customerService,
            NullLoggerFactory.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(42, AppConstants.Roles.Customer) }
            }
        };

        var placeResult = await controller.PlaceOrder(
            new CreateOrderRequest(1, new Dictionary<string, string?> { ["userId"] = "123" }, PaymentMethod: "qris"));
        Assert.IsType<OkObjectResult>(placeResult);
        Assert.Equal(42, orderService.LastPlacedCustomerId);
    }

    private sealed class StubProductService : IProductService
    {
        public int? LastRecentBrandsCustomerId { get; private set; }

        public Task<IEnumerable<CategoryInfo>> GetRecentGameBrandsAsync(
            int? customerId,
            IReadOnlyList<string>? guestSlugs = null)
        {
            LastRecentBrandsCustomerId = customerId;
            return Task.FromResult<IEnumerable<CategoryInfo>>([]);
        }

        public Task<PagedResult<GameProduct>> GetPagedProductsAsync(string? search, string? brand, int page, int limit) => throw new NotSupportedException();
        public Task<IEnumerable<CategoryInfo>> GetUniqueGameBrandsAsync(string? category = null, int offset = 0, int? limit = null) => throw new NotSupportedException();
        public Task<IEnumerable<CategoryInfo>> GetPopularGameBrandsAsync() => throw new NotSupportedException();
        public Task<IEnumerable<GameMetadataResponse>> GetPopularGameOptionsAsync() => throw new NotSupportedException();
        public Task<IEnumerable<CategoryInfo>> UpdatePopularGameBrandsAsync(IReadOnlyList<string> slugs) => throw new NotSupportedException();
        public Task<IEnumerable<GameCategoryInfo>> GetGameCategoriesAsync() => throw new NotSupportedException();
        public Task<IEnumerable<GameCategoryInfo>> GetGameCategoryRegistryAsync() => throw new NotSupportedException();
        public Task<IEnumerable<GameCategoryInfo>> UpdateGameCategoryRegistryAsync(UpdateGameCategoriesRequest request) => throw new NotSupportedException();
        public Task<IEnumerable<ProductResponse>> GetAllProductsAsync() => throw new NotSupportedException();
        public Task<GameProduct> CreateProductAsync(GameProduct product) => throw new NotSupportedException();
        public Task<string?> UpdateProductIconAsync(int id, ImageFileData file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<(int Added, int Updated, int Deactivated)> SyncCatalogFromDigiflazzAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<GameMetadataResponse>> GetGameMetadataAsync() => throw new NotSupportedException();
        public Task<GameMetadataResponse?> GetGameMetadataByIdAsync(int id) => throw new NotSupportedException();
        public Task<GameDetailResponse?> GetGameDetailAsync(string brandSlug) => throw new NotSupportedException();
        public Task<UsernameCheckResponse> CheckUsernameAsync(string brandSlug, Dictionary<string, string?> targetValues, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GameMetadataResponse?> UpdateGameMetadataAsync(int id, UpdateGameMetadataRequest request) => throw new NotSupportedException();
        public Task<int> CleanDummyProductsAsync() => throw new NotSupportedException();
        public Task<bool> UpdateProductMarginAsync(int productId, decimal? margin) => throw new NotSupportedException();
        public Task<int> GetMidtransExpiryAsync() => throw new NotSupportedException();
        public Task SetMidtransExpiryAsync(int expiryMinutes) => throw new NotSupportedException();
        public Task<string?> UpdateGameCoverAsync(int metadataId, ImageFileData file, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubInvoiceLookupService : IInvoiceLookupService
    {
        public int? LastLookupCustomerId { get; private set; }

        public Task<PublicInvoiceResponse?> GetPublicInvoiceAsync(
            string publicHash,
            int? currentCustomerId = null,
            CancellationToken cancellationToken = default)
        {
            LastLookupCustomerId = currentCustomerId;
            return Task.FromResult<PublicInvoiceResponse?>(null);
        }

        public Task<string?> ResolvePublicHashAsync(string orderNo, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Theory]
    [InlineData(AppConstants.Roles.Admin)]
    [InlineData(AppConstants.Roles.SuperAdmin)]
    public async Task CatalogAndPublicInvoiceControllers_IgnoreAdminNameIdentifier_OnOptionalAuthEndpoints(
        string nonCustomerRole)
    {
        var productService = new StubProductService();
        var catalogController = new CatalogController(productService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(1, nonCustomerRole) }
            }
        };

        await catalogController.GetRecentBrands("mobile-legends");
        Assert.Null(productService.LastRecentBrandsCustomerId);

        var invoiceService = new StubInvoiceLookupService();
        var invoiceController = new PublicInvoiceController(invoiceService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreatePrincipal(1, nonCustomerRole) }
            }
        };

        await invoiceController.GetInvoice(new string('a', 64), CancellationToken.None);
        Assert.Null(invoiceService.LastLookupCustomerId);
    }
}
