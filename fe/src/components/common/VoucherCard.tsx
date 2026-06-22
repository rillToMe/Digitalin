import { useState } from "react";
import { Ticket, X, Loader2 } from "lucide-react";

interface VoucherCardProps {
  /** Currently applied voucher code (null = none) */
  appliedCode: string | null;
  /** Called when user confirms a voucher code */
  onApply: (code: string) => void;
  /** Called when user removes the applied voucher */
  onRemove: () => void;
  /** Compact mode: removes outer card styling (for use inside another card) */
  compact?: boolean;
  /** Loading state for voucher validation */
  isLoading?: boolean;
  /** Error message from backend */
  error?: string | null;
}

/**
 * Voucher discount card for the checkout flow.
 * Three states: collapsed (pick voucher), expanded input, and applied.
 * Voucher validation happens server-side during order creation.
 */
export default function VoucherCard({ appliedCode, onApply, onRemove, compact = false, isLoading = false, error = null }: VoucherCardProps) {
  const [inputValue, setInputValue] = useState("");
  const [isExpanded, setIsExpanded] = useState(false);

  const handleApply = () => {
    const code = inputValue.trim();
    if (!code) return;
    onApply(code.toUpperCase());
    setIsExpanded(false);
  };

  const handleRemove = () => {
    setInputValue("");
    onRemove();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter") {
      e.preventDefault();
      handleApply();
    }
    if (e.key === "Escape") {
      setIsExpanded(false);
    }
  };

  const outerClass = compact ? "rounded-xl px-3 py-3" : "rounded-2xl p-5";
  const iconSize = compact ? 15 : 17;

  // ── Applied state ──────────────────────────────────────────────────────────
  if (appliedCode) {
    return (
      <div
        className={outerClass}
        style={compact
          ? { backgroundColor: "var(--color-success-bg)" }
          : { backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Ticket size={iconSize} style={{ color: "var(--color-success)", flexShrink: 0 }} />
            <div>
              <p style={{ color: "var(--color-primary)", fontWeight: 600, fontSize: compact ? "0.82rem" : "0.9rem" }}>
                {compact ? "Voucher" : "Voucher Diskon"}
              </p>
              <p style={{
                fontFamily: "monospace",
                fontSize: "0.82rem",
                fontWeight: 700,
                color: "var(--color-success)",
                letterSpacing: "0.03em",
              }}>
                {appliedCode}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={() => { setInputValue(appliedCode); setIsExpanded(true); onRemove(); }}
              className="px-2.5 py-1.5 rounded-lg transition-colors"
              style={{ color: "var(--color-tertiary)", fontSize: "0.75rem", fontWeight: 600, border: "none", backgroundColor: "transparent" }}
              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-info)"; }}
              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
            >
              Ganti
            </button>
            <button
              onClick={handleRemove}
              className="px-2 py-1.5 rounded-lg transition-colors"
              style={{ color: "var(--color-danger)", fontSize: "0.75rem", border: "none", backgroundColor: "transparent" }}
              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-danger-bg)"; }}
              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
            >
              <X size={13} />
            </button>
          </div>
        </div>
      </div>
    );
  }

  // ── Input state (collapsed) ────────────────────────────────────────────────
  if (!isExpanded) {
    return (
      <div
        className={outerClass}
        style={compact
          ? { backgroundColor: "var(--color-neutral)" }
          : { backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
      >
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Ticket size={iconSize} style={{ color: "var(--color-secondary)", flexShrink: 0 }} />
            <div>
              <p style={{ color: "var(--color-primary)", fontWeight: 600, fontSize: compact ? "0.82rem" : "0.9rem" }}>
                Voucher Diskon
              </p>
              <p style={{ color: "var(--color-secondary)", fontSize: "0.75rem" }}>
                Belum menggunakan voucher
              </p>
            </div>
          </div>
          <button
            onClick={() => setIsExpanded(true)}
            className="px-3 py-1.5 rounded-lg transition-colors"
            style={{
              color: "var(--color-tertiary)",
              fontWeight: 600,
              fontSize: compact ? "0.78rem" : "0.83rem",
              border: "none",
              backgroundColor: "transparent",
            }}
            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-info)"; }}
            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
          >
            Pilih →
          </button>
        </div>
      </div>
    );
  }

  // ── Input state (expanded) ─────────────────────────────────────────────────
  return (
    <div
      className={outerClass}
      style={{ backgroundColor: "var(--color-surface)", border: "1.5px solid var(--color-tertiary)" }}
    >
      <div className="flex items-center gap-2 mb-3">
        <Ticket size={iconSize} style={{ color: "var(--color-tertiary)", flexShrink: 0 }} />
        <p style={{ color: "var(--color-primary)", fontWeight: 600, fontSize: "0.85rem" }}>
          Masukkan Kode Voucher
        </p>
      </div>

      <div className="flex gap-2">
        <input
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ""))}
          onKeyDown={handleKeyDown}
          placeholder="Contoh: HEMAT20"
          maxLength={50}
          autoFocus
          className="flex-1 px-3 py-2 rounded-lg outline-none"
          style={{
            border: "1.5px solid var(--color-border-input)",
            fontSize: "0.85rem",
            color: "var(--color-primary)",
            fontFamily: "monospace",
            letterSpacing: "0.04em",
          }}
        />
        <button
          onClick={handleApply}
          disabled={!inputValue.trim() || isLoading}
          className="px-4 py-2 rounded-lg transition-colors flex items-center gap-1.5"
          style={{
            backgroundColor: inputValue.trim() && !isLoading ? "var(--color-tertiary)" : "var(--color-neutral)",
            color: inputValue.trim() && !isLoading ? "var(--color-on-primary)" : "var(--color-secondary)",
            fontWeight: 600,
            fontSize: "0.82rem",
            border: "none",
            whiteSpace: "nowrap",
          }}
        >
          {isLoading && <Loader2 size={13} className="animate-spin" />}
          {isLoading ? "Memvalidasi..." : "Gunakan"}
        </button>
      </div>

      {/* Error message */}
      {error && (
        <p style={{ color: "var(--color-danger)", fontSize: "0.78rem", marginTop: 8 }}>
          {error}
        </p>
      )}

      <button
        onClick={() => { setIsExpanded(false); setInputValue(""); }}
        className="mt-2 transition-colors"
        style={{ color: "var(--color-secondary)", fontSize: "0.78rem", border: "none", backgroundColor: "transparent" }}
      >
        Batal
      </button>
    </div>
  );
}
