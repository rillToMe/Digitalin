import React, { useState } from "react";
import { useNavigate } from "react-router";
import { ArrowLeft, Zap } from "lucide-react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { authService } from "../../../services/api";
import ForgotPasswordEmailStep from "../../../components/auth/ForgotPasswordEmailStep";
import ForgotPasswordOtpStep from "../../../components/auth/ForgotPasswordOtpStep";
import ForgotPasswordSuccessStep from "../../../components/auth/ForgotPasswordSuccessStep";

export default function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [email, setEmail] = useState("");
  const [otpCode, setOtpCode] = useState("");

  // Mutations
  const requestOtpMutation = useMutation({
    mutationFn: (emailVal: string) => authService.requestPasswordReset({ email: emailVal }),
    onSuccess: (res) => {
      toast.success(res.message || "Kode OTP reset password berhasil dikirim.");
      setStep(2);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const errMsg = err?.response?.data?.message ?? "Email tidak terdaftar.";
      toast.error(errMsg);
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: (payload: { otp: string; newPass: string }) =>
      authService.resetPassword({
        email,
        otpCode: payload.otp,
        newPassword: payload.newPass,
      }),
    onSuccess: (res) => {
      toast.success(res.message || "Password berhasil diubah.");
      setStep(3);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const errMsg = err?.response?.data?.message ?? "Kode OTP salah atau sudah kedaluwarsa.";
      toast.error(errMsg);
    },
  });

  // Action handlers
  const handleEmailSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim()) return;
    requestOtpMutation.mutate(email);
  };

  const handleOtpSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const formData = new FormData(e.currentTarget as HTMLFormElement);
    const newPass = formData.get("newPassword") as string;
    if (!otpCode.trim() || !newPass) return;
    resetPasswordMutation.mutate({ otp: otpCode, newPass });
  };

  const handleResendOtp = async () => {
    await requestOtpMutation.mutateAsync(email);
  };

  return (
    <div
      className="min-h-screen flex flex-col justify-center py-12 px-4 sm:px-6 lg:px-8"
      style={{ backgroundColor: "var(--color-neutral)" }}
    >
      <div className="sm:mx-auto sm:w-full sm:max-w-md text-center">
        {/* Brand Logo */}
        <div className="flex justify-center items-center gap-2 mb-6">
          <div
            style={{ backgroundColor: "var(--color-brand-tertiary)", borderRadius: 8 }}
            className="w-9 h-9 flex items-center justify-center"
          >
            <Zap size={18} color="#fff" fill="#fff" />
          </div>
          <span style={{ color: "var(--color-brand-primary)", fontWeight: 800, fontSize: "1.35rem" }}>
            Digitalin
          </span>
        </div>
      </div>

      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <div
          className="rounded-2xl py-8 px-6 sm:px-10 shadow-sm relative"
          style={{
            backgroundColor: "var(--color-surface)",
            border: "1px solid var(--color-border-light)",
          }}
        >
          {/* Back button (Only visible in step 1 and 2) */}
          {step < 3 && (
            <button
              onClick={() => (step === 2 ? setStep(1) : navigate("/login"))}
              className="absolute left-6 top-6 flex items-center gap-1 text-xs font-semibold focus:outline-none"
              style={{ color: "var(--color-brand-secondary)" }}
            >
              <ArrowLeft size={13} />
              {step === 2 ? "Kembali" : "Login"}
            </button>
          )}

          {/* Form Header */}
          <div className="text-center mb-8 pt-4">
            <h2 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.2rem" }}>
              Lupa Password
            </h2>
            <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", marginTop: 4 }}>
              {step === 1 && "Langkah 1: Masukkan alamat email terdaftar Anda"}
              {step === 2 && "Langkah 2: Masukkan OTP dan password baru"}
              {step === 3 && "Selesai"}
            </p>
          </div>

          {/* Wizard Steps */}
          {step === 1 && (
            <ForgotPasswordEmailStep
              email={email}
              setEmail={setEmail}
              onSubmit={handleEmailSubmit}
              isPending={requestOtpMutation.isPending}
            />
          )}

          {step === 2 && (
            <ForgotPasswordOtpStep
              email={email}
              otpCode={otpCode}
              setOtpCode={setOtpCode}
              onSubmit={handleOtpSubmit}
              isPending={resetPasswordMutation.isPending}
              onResendOtp={handleResendOtp}
              isResending={requestOtpMutation.isPending}
            />
          )}

          {step === 3 && (
            <ForgotPasswordSuccessStep onBackToLogin={() => navigate("/login")} />
          )}
        </div>
      </div>
    </div>
  );
}
