import React, { useEffect, useRef } from "react";
import { Mail, Loader2, ArrowRight } from "lucide-react";

import type { ForgotPasswordEmailStepProps } from "../../api/index";

export default function ForgotPasswordEmailStep({
  email,
  setEmail,
  onSubmit,
  isPending,
}: ForgotPasswordEmailStepProps) {
  const emailInputRef = useRef<HTMLInputElement>(null);

  // Autofocus email input on mount
  useEffect(() => {
    emailInputRef.current?.focus();
  }, []);

  return (
    <form onSubmit={onSubmit} className="space-y-5">
      <div>
        <label
          htmlFor="email"
          style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}
        >
          Email Terdaftar
        </label>
        <div className="relative">
          <Mail
            size={18}
            className="absolute left-4 top-1/2 -translate-y-1/2"
            style={{ color: "var(--color-brand-secondary)" }}
          />
          <input
            id="email"
            ref={emailInputRef}
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="Masukkan email Anda"
            disabled={isPending}
            required
            className="w-full pl-11 pr-4 py-3 rounded-xl outline-none transition-all"
            style={{
              border: "1.5px solid var(--color-border-input)",
              color: "var(--color-brand-primary)",
              backgroundColor: "var(--color-surface)",
              fontSize: "0.9rem",
            }}
            onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
            onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
            aria-label="Email Terdaftar"
          />
        </div>
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem", marginTop: 6 }}>
          Kami akan mengirimkan 6 digit kode OTP ke email ini untuk mereset password.
        </p>
      </div>

      <button
        type="submit"
        disabled={isPending || !email.trim()}
        className="w-full py-3 rounded-xl flex items-center justify-center gap-2 font-semibold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
        style={{
          backgroundColor: "var(--color-brand-tertiary)",
          cursor: isPending || !email.trim() ? "not-allowed" : "pointer",
        }}
      >
        {isPending ? <Loader2 size={16} className="animate-spin" /> : <ArrowRight size={16} />}
        Kirim OTP
      </button>
    </form>
  );
}
