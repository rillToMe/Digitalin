import { useState } from "react";
import { Search, Loader2, Receipt, Gamepad2, Clock, PackageX } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import { invoiceService } from "../../../services/api";
import type { OrderStatus, InvoicePaymentStatus, PublicInvoiceResponse } from "../../../api/index";
import { ImageWithFallback } from "../../../utils/ImageWithFallback";
import BackButton from "../../../components/common/BackButton";

/* Label status order — sama dengan Riwayat Order supaya bahasa konsisten. */
const STATUS_CONFIG: Record<OrderStatus, { label: string; color: string; bg: string }> = {
  PENDING:      { label: "Menunggu",   color: "var(--color-warning)",   bg: "var(--color-warning-bg)" },
  PAID:         { label: "Dibayar",    color: "var(--color-info)",      bg: "var(--color-info-bg)" },
  PROCESSING:   { label: "Diproses",   color: "var(--color-info)",      bg: "var(--color-info-bg)" },
  COMPLETED:    { label: "Selesai",    color: "var(--color-success)",   bg: "var(--color-success-bg)" },
  NEEDS_ACTION: { label: "Perlu Aksi", color: "var(--color-warning)",    bg: "var(--color-warning-bg)" },
  FAILED:       { label: "Gagal",      color: "var(--color-danger)",    bg: "var(--color-danger-bg)" },
  EXPIRED:      { label: "Expired",    color: "var(--color-brand-secondary)", bg: "var(--color-neutral)" },
  REFUNDED:     { label: "Refunded",   color: "var(--color-info)",      bg: "var(--color-info-bg)" },
};

/* Baris kedua hasil: status pembayaran (ringkasan backend). */
const PAYMENT_STATUS_CONFIG: Record<InvoicePaymentStatus, { label: string; color: string }> = {
  UNPAID:   { label: "Pembayaran belum dilakukan",  color: "var(--color-warning)" },
  PAID:     { label: "Pembayaran berhasil",         color: "var(--color-success)" },
  FAILED:   { label: "Pembayaran gagal / kedaluwarsa", color: "var(--color-danger)" },
  REFUNDED: { label: "Dana dikembalikan (refund)",  color: "var(--color-info)" },
};

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

/**
 * Cek Transaksi — halaman PUBLIK (tanpa login). Pengguna memasukkan nomor
 * invoice (INV-...) lalu melihat status transaksi lewat endpoint publik
 * GET /invoices/{orderNo}. Semua data dari API — tidak ada nilai hardcode.
 * Kalau user login dan invoice miliknya, backend menandai belongsToAccount.
 */
export default function CheckTransactionPage() {
  const [invoiceInput, setInvoiceInput] = useState("");

  const lookup = useMutation({
    mutationFn: (invoiceNumber: string) => invoiceService.get(invoiceNumber),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!invoiceInput.trim()) {
      return;
    }

    lookup.mutate(invoiceInput);
  };

  const invoice = lookup.data ?? null;
  const notFound =
    lookup.isError && (lookup.error as { response?: { status?: number } })?.response?.status === 404;

  return (
    <div className="max-w-xl mx-auto px-4 py-10">
      <BackButton />

      {/* Header */}
      <h1 style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem" }}>
        Cek Transaksi
      </h1>
      <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.875rem", marginTop: 6, marginBottom: 24, lineHeight: 1.5 }}>
        Masukkan nomor invoice untuk melihat status transaksi kamu.
      </p>

      {/* Form */}
      <form onSubmit={handleSubmit} className="mb-8">
        <div
          className="flex items-center gap-2 px-4"
          style={{
            height: 48,
            backgroundColor: "var(--color-surface)",
            border: "1.5px solid var(--color-border-input)",
            borderRadius: 14,
          }}
        >
          <Receipt size={17} style={{ color: "var(--color-brand-secondary)", flexShrink: 0 }} />
          <input
            type="text"
            value={invoiceInput}
            onChange={(e) => setInvoiceInput(e.target.value)}
            placeholder="INV-20260906-67310E54B0"
            aria-label="Nomor invoice"
            spellCheck={false}
            autoCapitalize="characters"
            className="flex-1 min-w-0 outline-none bg-transparent"
            style={{ color: "var(--color-brand-primary)", fontSize: "0.9rem" }}
          />
        </div>
        <button
          type="submit"
          disabled={lookup.isPending || !invoiceInput.trim()}
          className="w-full mt-3 py-3.5 rounded-xl flex items-center justify-center gap-2 disabled:opacity-60"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "var(--color-on-primary)",
            fontWeight: 600,
            fontSize: "0.95rem",
          }}
        >
          {lookup.isPending ? <Loader2 size={16} className="animate-spin" /> : <Search size={16} />}
          {lookup.isPending ? "Mencari..." : "Cek Transaksi"}
        </button>
      </form>

      {/* Hasil */}
      {lookup.isPending ? (
        <div className="rounded-2xl p-5 animate-pulse" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl" style={{ backgroundColor: "var(--color-skeleton)" }} />
            <div className="flex-1 space-y-2">
              <div className="h-4 w-2/3 rounded" style={{ backgroundColor: "var(--color-skeleton)" }} />
              <div className="h-3 w-1/3 rounded" style={{ backgroundColor: "var(--color-skeleton)" }} />
            </div>
          </div>
        </div>
      ) : notFound ? (
        <div className="text-center py-12 rounded-2xl" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
          <PackageX size={36} style={{ color: "var(--color-brand-secondary)", margin: "0 auto 10px" }} />
          <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.95rem" }}>Invoice tidak ditemukan.</p>
          <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", marginTop: 4 }}>
            Periksa kembali nomor invoice kamu.
          </p>
        </div>
      ) : lookup.isError ? (
        <div className="text-center py-12 rounded-2xl" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
          <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.95rem" }}>
            Terjadi kesalahan saat memuat transaksi.
          </p>
          <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", marginTop: 4 }}>
            Silakan coba lagi.
          </p>
        </div>
      ) : invoice ? (
        <InvoiceResult invoice={invoice} />
      ) : null}
    </div>
  );
}

/** Kartu hasil lookup — field dari API saja. */
function InvoiceResult({ invoice }: { invoice: PublicInvoiceResponse }) {
  const statusCfg = STATUS_CONFIG[invoice.status] ??
    { label: invoice.status, color: "var(--color-brand-secondary)", bg: "var(--color-neutral)" };
  const paymentCfg = PAYMENT_STATUS_CONFIG[invoice.paymentStatus] ??
    { label: invoice.paymentStatus, color: "var(--color-brand-secondary)" };

  return (
    <div
      className="rounded-2xl p-5"
      style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
    >
      {/* Produk + nominal */}
      <div className="flex items-center gap-4">
        <div
          className="w-12 h-12 rounded-xl overflow-hidden flex-shrink-0 flex items-center justify-center"
          style={{ backgroundColor: "var(--color-neutral)" }}
        >
          {invoice.imageUrl ? (
            <ImageWithFallback
              src={invoice.imageUrl}
              alt={invoice.productName}
              className="w-full h-full object-cover"
            />
          ) : (
            <Gamepad2 size={20} style={{ color: "var(--color-brand-tertiary)" }} />
          )}
        </div>

        <div className="flex-1 min-w-0">
          <h2 className="truncate" style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.95rem" }}>
            {invoice.productName}
          </h2>
          <p className="tabular-price" style={{ color: "var(--color-brand-tertiary)", fontWeight: 700, fontSize: "1.1rem", marginTop: 2 }}>
            {formatRp(invoice.amount)}
          </p>
        </div>

        <span className="px-2.5 py-1 rounded-full flex-shrink-0" style={{ backgroundColor: statusCfg.bg, color: statusCfg.color, fontSize: "0.75rem", fontWeight: 600 }}>
          {statusCfg.label}
        </span>
      </div>

      {/* Status pembayaran + batas bayar */}
      <div className="mt-4 space-y-1.5">
        <p style={{ color: paymentCfg.color, fontSize: "0.82rem", fontWeight: 600 }}>
          {paymentCfg.label}
        </p>
        {invoice.expiresAt && (
          <p className="flex items-center gap-1.5" style={{ color: "var(--color-brand-secondary)", fontSize: "0.78rem" }}>
            <Clock size={12} />
            Selesaikan pembayaran sebelum{" "}
            {new Date(invoice.expiresAt).toLocaleString("id-ID", {
              day: "numeric",
              month: "short",
              year: "numeric",
              hour: "2-digit",
              minute: "2-digit",
            })}
          </p>
        )}
      </div>

      {/* Metadata invoice */}
      <div
        className="mt-4 pt-4 space-y-2"
        style={{ borderTop: "1px solid var(--color-border-lighter)" }}
      >
        <div className="flex justify-between items-center gap-3" style={{ fontSize: "0.82rem" }}>
          <span style={{ color: "var(--color-brand-secondary)" }}>Invoice</span>
          <span style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontFamily: "monospace" }}>
            {invoice.invoiceNumber}
          </span>
        </div>
        <div className="flex justify-between items-center gap-3" style={{ fontSize: "0.82rem" }}>
          <span style={{ color: "var(--color-brand-secondary)" }}>Waktu Transaksi</span>
          <span style={{ color: "var(--color-brand-primary)" }}>
            {new Date(invoice.createdAt).toLocaleString("id-ID", {
              day: "numeric",
              month: "long",
              year: "numeric",
              hour: "2-digit",
              minute: "2-digit",
            })}
          </span>
        </div>
      </div>

      {/* Catatan pemilik (optional-auth) */}
      {invoice.belongsToAccount && (
        <p
          className="mt-4 px-4 py-3 rounded-xl"
          style={{ backgroundColor: "var(--color-info-bg)", color: "var(--color-info)", fontSize: "0.8rem" }}
        >
          Transaksi ini tersimpan di riwayat akun kamu.
        </p>
      )}
    </div>
  );
}
