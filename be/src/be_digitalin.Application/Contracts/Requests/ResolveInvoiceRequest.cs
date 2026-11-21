namespace be_digitalin.DTOs;

public record ResolveInvoiceRequest(
    /// <summary>Nomor invoice lengkap (INV-yyyyMMdd-XXXXXXXXXX) — di-trim.</summary>
    string OrderNo
);
