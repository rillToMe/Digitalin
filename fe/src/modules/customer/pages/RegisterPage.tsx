import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { Eye, EyeOff, Zap, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { authService } from "../../../services/api";
import type { RequestOtpPayload } from "../../../api/index";
import GoogleLoginButton, { googleClientId } from "../../../components/common/GoogleLoginButton";

export default function RegisterPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState({ email: "", password: "", fullName: "", otpCode: "" });
  const [showPass, setShowPass] = useState(false);
  const [step, setStep] = useState<"form" | "otp">("form");
  const [googleLoading, setGoogleLoading] = useState(false);

  const otpMutation = useMutation({
    mutationFn: (payload: RequestOtpPayload) => authService.requestOtp(payload),
    onSuccess: () => {
      toast.success("OTP terkirim ke email kamu");
      setStep("otp");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? "Gagal mengirim OTP";
      toast.error(msg);
    },
  });

  const registerMutation = useMutation({
    // API contract: POST /auth/local/register returns { message } only (no token).
    // After success, redirect to /login so user authenticates properly.
    mutationFn: (payload: { email: string; password: string; fullName: string; otpCode: string }) =>
      authService.register(payload),
    onSuccess: () => {
      toast.success("Registrasi berhasil! Silakan masuk dengan akun baru kamu.");
      navigate("/login");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? "Registrasi gagal";
      toast.error(msg);
    },
  });

  const sendOtp = () => {
    if (!form.email) { toast.error("Masukkan email terlebih dahulu"); return; }
    otpMutation.mutate({ email: form.email });
  };

  const handleRegister = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.fullName || !form.email || !form.password || !form.otpCode) {
      toast.error("Lengkapi semua field");
      return;
    }
    registerMutation.mutate({
      email: form.email,
      password: form.password,
      fullName: form.fullName,
      otpCode: form.otpCode,
    });
  };

  const loading = otpMutation.isPending || registerMutation.isPending;
  const anyLoading = loading || googleLoading;
  // const otpSent = step === "otp";

  return (
    <div className="min-h-screen flex items-center justify-center px-4 py-12" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
      <div className="w-full max-w-md">
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-2">
            <div style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", borderRadius: 10 }} className="w-10 h-10 flex items-center justify-center">
              <Zap size={20} color="#fff" fill="#fff" />
            </div>
            <span style={{ color: "var(--color-primary,#0F1419)", fontWeight: 800, fontSize: "1.3rem" }}>Digitalin</span>
          </div>
          <h2 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.4rem", marginTop: 8 }}>Buat Akun Baru</h2>
          <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.9rem", marginTop: 4 }}>
            {step === "form" ? "Isi data diri kamu" : "Verifikasi email kamu"}
          </p>
        </div>

        <div className="rounded-2xl p-8" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
          {step === "form" ? (
            <div className="space-y-4">
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Nama Lengkap</label>
                <input
                  value={form.fullName}
                  onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                  placeholder="John Doe"
                  className="w-full px-4 py-3 rounded-xl outline-none"
                  style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                  onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                  onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                />
              </div>
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Email</label>
                <div className="flex gap-2">
                  <input
                    type="email"
                    value={form.email}
                    onChange={(e) => setForm({ ...form, email: e.target.value })}
                    placeholder="email@example.com"
                    className="flex-1 px-4 py-3 rounded-xl outline-none"
                    style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                    onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                    onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                  />
                </div>
              </div>
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Password</label>
                <div className="relative">
                  <input
                    type={showPass ? "text" : "password"}
                    value={form.password}
                    onChange={(e) => setForm({ ...form, password: e.target.value })}
                    placeholder="••••••••"
                    className="w-full px-4 py-3 pr-12 rounded-xl outline-none"
                    style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                    onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                    onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                  />
                  <button type="button" onClick={() => setShowPass(!showPass)} className="absolute right-3 top-1/2 -translate-y-1/2" style={{ color: "var(--color-secondary,#4A5568)" }}>
                    {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>
              </div>
              <button
                onClick={sendOtp}
                disabled={anyLoading}
                className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2"
                style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.95rem", opacity: anyLoading ? 0.7 : 1 }}
              >
                {loading && <Loader2 size={16} className="animate-spin" />}
                Kirim Kode OTP
              </button>

              {googleClientId && step === "form" && (
                <GoogleLoginButton disabled={anyLoading} onLoadingChange={setGoogleLoading} />
              )}
            </div>
          ) : (
            <form onSubmit={handleRegister} className="space-y-4">
              <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem", textAlign: "center" }}>
                Kode OTP dikirim ke <strong style={{ color: "var(--color-primary,#0F1419)" }}>{form.email}</strong>
              </p>
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Kode OTP</label>
                <input
                  value={form.otpCode}
                  onChange={(e) => setForm({ ...form, otpCode: e.target.value })}
                  placeholder="123456"
                  maxLength={6}
                  className="w-full px-4 py-3 rounded-xl outline-none text-center"
                  style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "1.2rem", letterSpacing: "0.2em", color: "var(--color-primary,#0F1419)" }}
                  onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                  onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                />
              </div>
              <button
                type="submit"
                disabled={loading}
                className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2"
                style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.95rem", opacity: loading ? 0.7 : 1 }}
              >
                {loading && <Loader2 size={16} className="animate-spin" />}
                Daftar Sekarang
              </button>
              <button type="button" onClick={() => setStep("form")} style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem", width: "100%", textAlign: "center" }}>
                ← Kembali
              </button>
            </form>
          )}

          <p className="text-center mt-5" style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.875rem" }}>
            Sudah punya akun?{" "}
            <Link to="/login" style={{ color: "var(--color-tertiary,#2C5EF5)", fontWeight: 600, textDecoration: "none" }}>
              Masuk
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
