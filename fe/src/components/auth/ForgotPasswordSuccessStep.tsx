import React from "react";
import { CheckCircle2, ArrowLeft } from "lucide-react";

import type { ForgotPasswordSuccessStepProps } from "../../api/index";

export default function ForgotPasswordSuccessStep({ onBackToLogin }: ForgotPasswordSuccessStepProps) {
  return (
    <div className="text-center space-y-6">
      <div className="flex justify-center">
        <div
          className="w-16 h-16 rounded-full flex items-center justify-center"
          style={{ backgroundColor: "var(--color-success-bg)" }}
        >
          <CheckCircle2 size={36} style={{ color: "var(--color-success)" }} />
        </div>
      </div>

      <div className="space-y-2">
        <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.15rem" }}>
          Berhasil!
        </h3>
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.88rem" }}>
          Password berhasil diubah.
        </p>
      </div>

      <button
        onClick={onBackToLogin}
        className="w-full py-3 rounded-xl flex items-center justify-center gap-2 font-semibold text-white transition-opacity hover:opacity-90"
        style={{ backgroundColor: "var(--color-brand-tertiary)" }}
      >
        <ArrowLeft size={16} />
        Kembali ke Login
      </button>
    </div>
  );
}
