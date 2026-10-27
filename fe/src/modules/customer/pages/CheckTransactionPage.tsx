import { useState } from "react";
import { Search, Loader2, Receipt, PackageX } from "lucide-react";
import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { invoiceService } from "../../../services/api";
import BackButton from "../../../components/common/BackButton";

/**
 * Cek Transaksi — halaman PUBLIK (tanpa login). Pengguna memasukkan nomor
 * invoice (INV-...) → endpoint publik POST /invoices/resolve mengembalikan
 * PublicHash SAJA (tanpa data transaksi) → redirect ke halaman status
 * /order/status/{publicHash} yang juga publik. OrderNo sendiri tidak pernah
 * valid sebagai URL status (backend 404) — PublicHash satu-satunya
 * identifier publik.
 */
export default function CheckTransactionPage() {
  const [invoiceInput, setInvoiceInput] = useState("");
  const navigate = useNavigate();

  const lookup = useMutation({
    mutationFn: (orderNo: string) => invoiceService.resolve(orderNo),
    onSuccess: (res) => {
      navigate(`/order/status/${res.publicHash}`);
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!invoiceInput.trim()) {
      return;
    }

    lookup.mutate(invoiceInput);
  };

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
      ) : null}
    </div>
  );
}
