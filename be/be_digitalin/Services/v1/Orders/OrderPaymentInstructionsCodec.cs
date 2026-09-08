using System.Text.Json;
using be_digitalin.DTOs;

namespace be_digitalin.Services.Orders;

/// <summary>
/// Encode/decode OrderPaymentInstructions ke kolom
/// TopupOrder.PaymentInstructionsJson — satu SSOT format persist
/// (camelCase web-defaults, toleran baca).
/// </summary>
internal static class OrderPaymentInstructionsCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static string? Serialize(OrderPaymentInstructions? instructions) =>
        instructions is null
            ? null
            : JsonSerializer.Serialize(instructions, Options);

    internal static OrderPaymentInstructions? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<OrderPaymentInstructions>(json, Options);
}
