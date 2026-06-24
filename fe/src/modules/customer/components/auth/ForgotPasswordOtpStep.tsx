import React, { useState, useEffect } from "react";
import { Eye, EyeOff, Loader2, KeyRound, Lock } from "lucide-react";
import OtpInput from "./OtpInput";

import type { ForgotPasswordOtpStepProps } from "../../../../api/index";

export default function ForgotPasswordOtpStep({
  email,
  otpCode,
  setOtpCode,
  onSubmit,
  isPending,
  onResendOtp,
  isResending,
}: ForgotPasswordOtpStepProps) {
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);

  // Countdown timer for Resend OTP (60s)
  const [countdown, setCountdown] = useState(60);

  useEffect(() => {
    if (countdown === 0) return;
    const timer = setInterval(() => {
      setCountdown((prev) => prev - 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [countdown]);

  const handleResend = async () => {
    if (countdown > 0 || isResending) return;
    await onResendOtp();
    setCountdown(60);
  };

  // Real-time Validation
  const isPasswordValid = newPassword.length >= 8;
  const isPasswordMatching = newPassword === confirmPassword;
  const isFormValid = otpCode.length === 6 && isPasswordValid && isPasswordMatching;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid) return;
    // Pass password through a hidden input or state. Let's make sure the parent form onSubmit triggers with this data.
    // Actually we can pass { otpCode, newPassword } back. Since onSubmit is a custom handler in parent, let's pass a wrapper.
    onSubmit(e);
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      {/* Hidden inputs to pass password to parent form if needed */}
      <input type="hidden" name="newPassword" value={newPassword} />

      {/* OTP Code Section */}
      <div>
        <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 8, textAlign: "center" }}>
          Masukkan 6 Digit Kode OTP
        </label>
        <OtpInput value={otpCode} onChange={setOtpCode} />
        <p style={{ color: "var(--color-secondary)", fontSize: "0.78rem", marginTop: 6, textAlign: "center" }}>
          Kode dikirim ke <strong style={{ color: "var(--color-primary)" }}>{email}</strong>
        </p>
      </div>

      {/* New Password Section */}
      <div>
        <label
          htmlFor="newPassword"
          style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}
        >
          Password Baru
        </label>
        <div className="relative">
          <KeyRound
            size={18}
            className="absolute left-4 top-1/2 -translate-y-1/2"
            style={{ color: "var(--color-secondary)" }}
          />
          <input
            id="newPassword"
            type={showPassword ? "text" : "password"}
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            placeholder="Minimal 8 karakter"
            disabled={isPending}
            required
            className="w-full pl-11 pr-11 py-3 rounded-xl outline-none transition-all"
            style={{
              border: "1.5px solid var(--color-border-input)",
              color: "var(--color-primary)",
              backgroundColor: "var(--color-surface)",
              fontSize: "0.9rem",
            }}
            onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary)"; }}
            onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
          />
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword)}
            className="absolute right-4 top-1/2 -translate-y-1/2 focus:outline-none"
            style={{ color: "var(--color-secondary)" }}
            tabIndex={-1}
          >
            {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        </div>
        {newPassword && !isPasswordValid && (
          <p style={{ color: "var(--color-danger)", fontSize: "0.75rem", marginTop: 4 }}>
            Password minimal 8 karakter.
          </p>
        )}
      </div>

      {/* Confirm Password Section */}
      <div>
        <label
          htmlFor="confirmPassword"
          style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}
        >
          Konfirmasi Password Baru
        </label>
        <div className="relative">
          <Lock
            size={18}
            className="absolute left-4 top-1/2 -translate-y-1/2"
            style={{ color: "var(--color-secondary)" }}
          />
          <input
            id="confirmPassword"
            type={showConfirmPassword ? "text" : "password"}
            value={confirmPassword}
            onChange={(e) => setConfirmPassword(e.target.value)}
            placeholder="Ketik ulang password baru"
            disabled={isPending}
            required
            className="w-full pl-11 pr-11 py-3 rounded-xl outline-none transition-all"
            style={{
              border: "1.5px solid var(--color-border-input)",
              color: "var(--color-primary)",
              backgroundColor: "var(--color-surface)",
              fontSize: "0.9rem",
            }}
            onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary)"; }}
            onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
          />
          <button
            type="button"
            onClick={() => setShowConfirmPassword(!showConfirmPassword)}
            className="absolute right-4 top-1/2 -translate-y-1/2 focus:outline-none"
            style={{ color: "var(--color-secondary)" }}
            tabIndex={-1}
          >
            {showConfirmPassword ? <EyeOff size={18} /> : <Eye size={18} />}
          </button>
        </div>
        {confirmPassword && !isPasswordMatching && (
          <p style={{ color: "var(--color-danger)", fontSize: "0.75rem", marginTop: 4 }}>
            Konfirmasi password tidak cocok.
          </p>
        )}
      </div>

      {/* Resend OTP button & timer */}
      <div className="flex justify-center text-xs">
        {countdown > 0 ? (
          <p style={{ color: "var(--color-secondary)" }}>
            Kirim ulang OTP dalam <span className="font-semibold" style={{ color: "var(--color-tertiary)" }}>{countdown} detik</span>
          </p>
        ) : (
          <button
            type="button"
            onClick={handleResend}
            disabled={isResending}
            className="font-semibold hover:underline"
            style={{ color: "var(--color-tertiary)" }}
          >
            {isResending ? "Mengirim..." : "Kirim Ulang OTP"}
          </button>
        )}
      </div>

      {/* Submit Button */}
      <button
        type="submit"
        disabled={isPending || !isFormValid}
        className="w-full py-3 rounded-xl flex items-center justify-center gap-2 font-semibold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
        style={{
          backgroundColor: "var(--color-tertiary)",
          cursor: isPending || !isFormValid ? "not-allowed" : "pointer",
        }}
      >
        {isPending ? <Loader2 size={16} className="animate-spin" /> : null}
        Reset Password
      </button>
    </form>
  );
}
