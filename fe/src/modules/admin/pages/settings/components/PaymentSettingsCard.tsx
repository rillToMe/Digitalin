import { CreditCard, Save, Loader2, RefreshCw, AlertTriangle } from "lucide-react";
import { usePaymentSettings } from "../hooks/usePaymentSettings";
import { SettingSectionHeader } from "./SettingSectionHeader";

export function PaymentSettingsCard() {
  const {
    expiryValue,
    setExpiryValue,
    expiryLoading,
    expiryError,
    refetchExpiry,
    isSavingExpiry,
    handleSaveExpiry,
  } = usePaymentSettings();

  return (
    <div
      className="rounded-2xl p-6"
      style={{
        backgroundColor: "var(--color-surface)",
        border: "1px solid var(--color-border-light)",
      }}
    >
      <SettingSectionHeader icon={CreditCard} title="Payment Settings" />

      {expiryError ? (
        <div
          className="p-5 rounded-xl text-center space-y-3"
          style={{ backgroundColor: "var(--color-danger-bg)" }}
        >
          <AlertTriangle
            size={32}
            style={{ color: "var(--color-danger)", margin: "0 auto" }}
          />
          <p
            style={{
              color: "var(--color-danger)",
              fontSize: "0.85rem",
              fontWeight: 500,
            }}
          >
            Gagal memuat konfigurasi dari backend.
          </p>
          <button
            onClick={() => refetchExpiry()}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold"
            style={{
              backgroundColor: "var(--color-surface)",
              border: "1px solid var(--color-border)",
              color: "var(--color-brand-primary)",
            }}
          >
            <RefreshCw size={12} /> Coba Lagi
          </button>
        </div>
      ) : expiryLoading ? (
        <div className="py-8 flex flex-col items-center justify-center gap-2">
          <Loader2
            size={24}
            className="animate-spin"
            style={{ color: "var(--color-brand-tertiary)" }}
          />
          <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem" }}>
            Memuat konfigurasi...
          </span>
        </div>
      ) : (
        <form onSubmit={handleSaveExpiry} className="space-y-4">
          <div>
            <label
              style={{
                color: "var(--color-brand-primary)",
                fontSize: "0.85rem",
                fontWeight: 500,
                display: "block",
                marginBottom: 6,
              }}
            >
              Midtrans Payment Expiry
            </label>
            <div className="flex items-center gap-3 max-w-xs">
              <input
                type="number"
                value={expiryValue}
                onChange={(e) =>
                  setExpiryValue(Math.max(1, parseInt(e.target.value) || 0))
                }
                disabled={isSavingExpiry}
                min={1}
                className="w-24 px-4 py-2.5 rounded-xl outline-none transition-all text-center font-semibold"
                style={{
                  border: "1.5px solid var(--color-border-input)",
                  fontSize: "0.9rem",
                  color: "var(--color-brand-primary)",
                  backgroundColor: "var(--color-surface)",
                }}
                onFocus={(e) =>
                  (e.target.style.borderColor = "var(--color-brand-tertiary)")
                }
                onBlur={(e) =>
                  (e.target.style.borderColor = "var(--color-border-input)")
                }
              />
              <span
                style={{ color: "var(--color-brand-secondary)", fontSize: "0.88rem" }}
              >
                Menit
              </span>
            </div>
            <p
              style={{
                color: "var(--color-brand-secondary)",
                fontSize: "0.78rem",
                marginTop: 6,
              }}
            >
              Menentukan batas waktu pembayaran Midtrans sebelum invoice
              otomatis expired.
            </p>
          </div>

          <div className="pt-2">
            <button
              type="submit"
              disabled={isSavingExpiry}
              className="px-5 py-2.5 rounded-xl flex items-center justify-center gap-2 text-white font-semibold transition-opacity hover:opacity-90 disabled:opacity-50"
              style={{ backgroundColor: "var(--color-brand-tertiary)" }}
            >
              {isSavingExpiry ? (
                <Loader2 size={15} className="animate-spin" />
              ) : (
                <Save size={15} />
              )}
              {isSavingExpiry ? "Menyimpan..." : "Save Changes"}
            </button>
          </div>
        </form>
      )}
    </div>
  );
}
