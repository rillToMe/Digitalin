using be_digitalin.Application.Ports;
using be_digitalin.Config;
using be_digitalin.Constants;
using be_digitalin.Data;
using be_digitalin.Domain.Entities;
using be_digitalin.DTOs;
using be_digitalin.Services.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace be_digitalin.Tests.Services;

/// <summary>
/// Unit test jalur pembayaran Core API di OrderService (Phase 2A): validasi
/// paymentMethod/paymentBank ditolak paling awal (sebelum DB — InMemory
/// tidak mendukung BeginTransactionAsync, jadi jalur sukses charge diuji
/// lewat provider tests + mapper/codec di sini), mapping instruksi charge,
/// dan round-trip codec persist.
/// </summary>
public class OrderServicePaymentMethodValidationTests
{
    private static OrderService CreateService(DigitalinDbContext db) =>
        new(db, null!, null!, null!, null!, null!, null!,
            Options.Create(new OrderPublicHashSettings
            {
                SecretKey = "unit-test-hmac-secret-0123456789abcdef"
            }));

    private static DigitalinDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DigitalinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DigitalinDbContext(options);
    }

    private static CreateOrderRequest Request(
        string? paymentMethod = null, string? paymentBank = null, string? paymentStore = null) =>
        new(1, new Dictionary<string, string?> { ["userId"] = "123" },
            PaymentMethod: paymentMethod, PaymentBank: paymentBank, PaymentStore: paymentStore);

    [Fact]
    public async Task PlaceOrderAsync_MissingPaymentMethod_ThrowsBeforeOrderCreation()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request()));

        Assert.Contains("Metode pembayaran wajib dipilih", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_UnknownPaymentMethod_ThrowsBeforeOrderCreation()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "ovo")));

        Assert.Contains("Metode pembayaran tidak didukung", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_ShopeePay_PassesProviderWhitelist()
    {
        await using var db = CreateDb();
        await SeedCheckoutAsync(db);
        db.PaymentMethods.Add(new PaymentMethod
        {
            Code = "shopeepay", DisplayName = "ShopeePay", Enabled = true, SortOrder = 3
        });
        await db.SaveChangesAsync();

        // Lewat whitelist provider + config → gagal BERIKUTNYA di
        // BuildTargetValue (schema "[]"), bukan lagi di validasi metode.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "shopeepay")));

        Assert.DoesNotContain("tidak didukung", ex.Message);
        Assert.DoesNotContain("tidak tersedia", ex.Message);
    }

    [Fact]
    public async Task PlaceOrderAsync_BankTransferWithoutBank_Throws()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "bank_transfer")));

        Assert.Contains("wajib memilih bank VA", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_UnknownBank_Throws()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(
                Request(paymentMethod: "bank_transfer", paymentBank: "bukopin")));

        Assert.Contains("Bank VA tidak didukung: bukopin", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_BankWithNonTransferMethod_Throws()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(
                Request(paymentMethod: "qris", paymentBank: "bca")));

        Assert.Contains("paymentBank hanya berlaku", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    // ── cstore (Indomaret/Alfamart) ──

    [Fact]
    public async Task PlaceOrderAsync_CstoreWithoutStore_Throws()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "cstore")));

        Assert.Contains("wajib memilih toko", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_CstoreUnknownStore_Throws()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(
                Request(paymentMethod: "cstore", paymentStore: "lawson")));

        Assert.Contains("Toko tidak didukung: lawson", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_StoreWithNonCstoreMethod_Throws()
    {
        await using var db = CreateDb();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(
                Request(paymentMethod: "qris", paymentStore: "indomaret")));

        Assert.Contains("paymentStore hanya berlaku", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_UnknownPaymentMethod_NoDbTouched()
    {
        // Bukti "sebelum DB": DB tanpa product/metadata sama sekali tetap
        // menolak di validasi metode — bukan di lookup product.
        await using var db = CreateDb();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "gopay-legacy")));

        Assert.Empty(db.GameProducts);
        Assert.Empty(db.GameMetadata);
    }

    // ── Konfigurasi checkout (SSOT tabel PaymentMethods) ──
    // Provider-whitelist lolos (qris/gopay/bank_transfer valid), tapi item
    // dinonaktifkan admin → order ditolak setelah lookup product/metadata.

    private static async Task SeedCheckoutAsync(DigitalinDbContext db)
    {
        db.GameProducts.Add(new GameProduct
        {
            Name = "5280 CP", GameCode = "TESTGAME", BuyerSkuCode = "sku-1",
            Brand = "TESTGAME", Category = "Games", Price = 15199m, IsActive = true
        });
        db.GameMetadata.Add(new GameMetadata
        {
            Brand = "TESTGAME", BrandSlug = "testgame",
            InputSchemaJson = "[]", TargetTemplate = "{userId}"
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task PlaceOrderAsync_DisabledMethod_Throws()
    {
        await using var db = CreateDb();
        await SeedCheckoutAsync(db);
        db.PaymentMethods.Add(new PaymentMethod
        {
            Code = "gopay", DisplayName = "GoPay", Enabled = false, SortOrder = 2
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "gopay")));

        Assert.Contains("Metode pembayaran sedang tidak tersedia", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }

    [Fact]
    public async Task PlaceOrderAsync_EnabledMethod_ProceedsPastConfigCheck()
    {
        await using var db = CreateDb();
        await SeedCheckoutAsync(db);
        db.PaymentMethods.Add(new PaymentMethod
        {
            Code = "gopay", DisplayName = "GoPay", Enabled = true, SortOrder = 2
        });
        await db.SaveChangesAsync();

        // Lewat config check → gagal BERIKUTNYA di BuildTargetValue (schema
        // "[]" tidak punya field) — bukan lagi di cek konfigurasi.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(Request(paymentMethod: "gopay")));

        Assert.DoesNotContain("tidak tersedia", ex.Message);
    }

    [Fact]
    public async Task PlaceOrderAsync_DisabledBank_Throws()
    {
        await using var db = CreateDb();
        await SeedCheckoutAsync(db);
        db.PaymentMethods.Add(new PaymentMethod
        {
            Code = "bank_transfer", DisplayName = "Transfer Bank", Enabled = true, SortOrder = 3
        });
        db.PaymentBanks.Add(new PaymentBank
        {
            Code = "bca", DisplayName = "BCA", Enabled = false, SortOrder = 1,
            PaymentMethodCode = "bank_transfer"
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(db).PlaceOrderAsync(
                Request(paymentMethod: "bank_transfer", paymentBank: "bca")));

        Assert.Contains("Bank VA sedang tidak tersedia", ex.Message);
        Assert.Equal(0, await db.TopupOrders.CountAsync());
    }
}

/// <summary>Mapping MidtransChargeResult → OrderPaymentInstructions.</summary>
public class OrderServiceMapPaymentInstructionsTests
{
    [Fact]
    public void Map_QrisCharge_MapsQrStringAndActions()
    {
        var charge = new MidtransChargeResult(
            TransactionId: "tx-1", PaymentType: "qris", TransactionStatus: "pending",
            FraudStatus: "accept", StatusCode: "201", StatusMessage: null,
            QrString: "00020101QRIS",
            Actions: [new MidtransAction("generate-qr-code", "https://x/qr")],
            VaNumbers: [], PermataVaNumber: null, BillKey: null, BillerCode: null);

        var result = OrderService.MapPaymentInstructions(charge);

        Assert.NotNull(result);
        Assert.Equal("00020101QRIS", result!.QrString);
        var action = Assert.Single(result.Actions);
        Assert.Equal("generate-qr-code", action.Name);
        Assert.Equal("https://x/qr", action.Url);
        Assert.Null(result.VaBank);
        Assert.Null(result.VaNumber);
    }

    [Fact]
    public void Map_BankTransferCharge_MapsFirstVaNumber()
    {
        var charge = new MidtransChargeResult(
            TransactionId: "tx-2", PaymentType: "bank_transfer", TransactionStatus: "pending",
            FraudStatus: "accept", StatusCode: "201", StatusMessage: null,
            QrString: null, Actions: [],
            VaNumbers: [new MidtransVaNumber("bca", "91019021581")],
            PermataVaNumber: null, BillKey: null, BillerCode: null);

        var result = OrderService.MapPaymentInstructions(charge);

        Assert.NotNull(result);
        Assert.Equal("bca", result!.VaBank);
        Assert.Equal("91019021581", result.VaNumber);
        Assert.Null(result.QrString);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Map_PermataCharge_NormalizesToVaBankVaNumber()
    {
        // Permata mengirim VA di luar va_numbers — dinormalisasi supaya FE
        // punya satu bentuk render VA.
        var charge = new MidtransChargeResult(
            TransactionId: "tx-3", PaymentType: "bank_transfer", TransactionStatus: "pending",
            FraudStatus: "accept", StatusCode: "201", StatusMessage: null,
            QrString: null, Actions: [],
            VaNumbers: [], PermataVaNumber: "8708000000000001",
            BillKey: null, BillerCode: null);

        var result = OrderService.MapPaymentInstructions(charge);

        Assert.NotNull(result);
        Assert.Equal("permata", result!.VaBank);
        Assert.Equal("8708000000000001", result.VaNumber);
    }

    [Fact]
    public void Map_EchannelCharge_MapsBillerCodeAndBillKey()
    {
        // Mandiri Bill (echannel): instruksi = biller_code + bill_key.
        var charge = new MidtransChargeResult(
            TransactionId: "tx-4", PaymentType: "echannel", TransactionStatus: "pending",
            FraudStatus: "accept", StatusCode: "201", StatusMessage: null,
            QrString: null, Actions: [],
            VaNumbers: [], PermataVaNumber: null,
            BillKey: "864116861019", BillerCode: "70012");

        var result = OrderService.MapPaymentInstructions(charge);

        Assert.NotNull(result);
        Assert.Equal("70012", result!.BillerCode);
        Assert.Equal("864116861019", result.BillKey);
        Assert.Null(result.VaBank);
        Assert.Null(result.VaNumber);
    }

    [Fact]
    public void Map_CstoreCharge_MapsStoreAndPaymentCode()
    {
        var charge = new MidtransChargeResult(
            TransactionId: "tx-5", PaymentType: "cstore", TransactionStatus: "pending",
            FraudStatus: "accept", StatusCode: "201", StatusMessage: null,
            QrString: null, Actions: [], VaNumbers: [], PermataVaNumber: null,
            BillKey: null, BillerCode: null,
            PaymentCode: "146028402627", Store: "indomaret", MerchantId: "M241438321");

        var result = OrderService.MapPaymentInstructions(charge);

        Assert.NotNull(result);
        Assert.Equal("indomaret", result!.Store);
        Assert.Equal("146028402627", result.PaymentCode);
        Assert.Equal("M241438321", result.MerchantId);
        Assert.Null(result.VaBank);
        Assert.Null(result.BillKey);
    }

    [Fact]
    public void RoundTrip_CstoreInstructions_CamelCaseJsonShape()
    {
        var instructions = new OrderPaymentInstructions(
            QrString: null, Actions: [],
            VaBank: null, VaNumber: null,
            BillerCode: null, BillKey: null,
            Store: "indomaret", PaymentCode: "146028402627", MerchantId: "M241438321");

        var json = OrderPaymentInstructionsCodec.Serialize(instructions);

        Assert.Contains("\"store\":\"indomaret\"", json);
        Assert.Contains("\"paymentCode\":\"146028402627\"", json);
        Assert.Contains("\"merchantId\":\"M241438321\"", json);

        var parsed = OrderPaymentInstructionsCodec.Deserialize(json);
        Assert.Equal("indomaret", parsed!.Store);
        Assert.Equal("146028402627", parsed.PaymentCode);
        Assert.Equal("M241438321", parsed.MerchantId);
    }

    [Fact]
    public void Map_NullCharge_ReturnsNull()
    {
        Assert.Null(OrderService.MapPaymentInstructions(null));
    }
}

/// <summary>Round-trip persist PaymentInstructionsJson (camelCase web-defaults).</summary>
public class OrderPaymentInstructionsCodecTests
{
    [Fact]
    public void RoundTrip_QrisInstructions_PreservesShape()
    {
        var instructions = new OrderPaymentInstructions(
            QrString: "00020101QRIS",
            Actions: [new OrderPaymentAction("generate-qr-code", "https://x/qr")],
            VaBank: null, VaNumber: null);

        var json = OrderPaymentInstructionsCodec.Serialize(instructions);
        var parsed = OrderPaymentInstructionsCodec.Deserialize(json);

        Assert.NotNull(parsed);
        Assert.Equal("00020101QRIS", parsed!.QrString);
        var action = Assert.Single(parsed.Actions);
        Assert.Equal("generate-qr-code", action.Name);
        Assert.Equal("https://x/qr", action.Url);
        Assert.Null(parsed.VaBank);
        Assert.Null(parsed.VaNumber);
    }

    [Fact]
    public void RoundTrip_VaInstructions_CamelCaseJsonShape()
    {
        var instructions = new OrderPaymentInstructions(
            QrString: null, Actions: [],
            VaBank: "bca", VaNumber: "91019021581");

        var json = OrderPaymentInstructionsCodec.Serialize(instructions);

        // Bentuk persist camelCase — kontrak kolom stabil.
        Assert.Contains("\"qrString\":null", json);
        Assert.Contains("\"vaBank\":\"bca\"", json);
        Assert.Contains("\"vaNumber\":\"91019021581\"", json);

        var parsed = OrderPaymentInstructionsCodec.Deserialize(json);
        Assert.Equal("bca", parsed!.VaBank);
        Assert.Equal("91019021581", parsed.VaNumber);
    }

    [Fact]
    public void RoundTrip_MandiriBillInstructions_CamelCaseJsonShape()
    {
        var instructions = new OrderPaymentInstructions(
            QrString: null, Actions: [],
            VaBank: null, VaNumber: null,
            BillerCode: "70012", BillKey: "864116861019");

        var json = OrderPaymentInstructionsCodec.Serialize(instructions);

        // Bentuk persist camelCase — kontrak kolom stabil.
        Assert.Contains("\"billerCode\":\"70012\"", json);
        Assert.Contains("\"billKey\":\"864116861019\"", json);

        var parsed = OrderPaymentInstructionsCodec.Deserialize(json);
        Assert.Equal("70012", parsed!.BillerCode);
        Assert.Equal("864116861019", parsed.BillKey);
    }

    [Fact]
    public void Serialize_Null_ReturnsNull()
    {
        Assert.Null(OrderPaymentInstructionsCodec.Serialize(null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_NullOrWhitespace_ReturnsNull(string? json)
    {
        Assert.Null(OrderPaymentInstructionsCodec.Deserialize(json));
    }
}
