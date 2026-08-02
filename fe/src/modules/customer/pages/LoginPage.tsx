import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { Eye, EyeOff, Zap, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import { authService } from "../../../services/api";
import { useRecaptcha, RecaptchaError } from "../../../hooks/useRecaptcha";
import type { LoginPayload } from "../../../api/index";
import GoogleLoginButton, { googleClientId } from "../../../components/common/GoogleLoginButton";

export default function LoginPage() {
  const navigate = useNavigate();
  const { setToken } = useAuthStore();
  const { executeRecaptcha } = useRecaptcha();
  const [form, setForm] = useState({ email: "", password: "", });
  const [showPass, setShowPass] = useState(false);
  const [googleLoading, setGoogleLoading] = useState(false);

  const loginMutation = useMutation({
    /* captcha dijalankan DI DALAM mutationFn supaya isPending sudah true sejak
       tombol diklik. Kalau token-nya di-await dulu di handleSubmit, tombolnya
       diam sampai captcha selesai — terasa seperti klik yang tidak masuk. */
    mutationFn: async (payload: Omit<LoginPayload, "captchaToken">) => {
      const captchaToken = await executeRecaptcha("login");

      return authService.login({ ...payload, captchaToken });
    },
    onSuccess: (res) => {
      setToken(res.token);
      toast.success("Login berhasil!");
      navigate("/");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(
        err instanceof RecaptchaError
          ? err.message
          : err?.response?.data?.message ?? "Email atau password salah",
      );
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.email || !form.password) {
      toast.error("Isi semua field");
      return;
    }

    loginMutation.mutate({ email: form.email, password: form.password });
  };

  const loading = loginMutation.isPending;
  const anyLoading = loading || googleLoading;

  return (
    <div className="min-h-screen flex items-center justify-center px-4 py-12" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
      <div className="w-full max-w-md">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-2">
            <div style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", borderRadius: 10 }} className="w-10 h-10 flex items-center justify-center">
              <Zap size={20} color="#fff" fill="#fff" />
            </div>
            <span style={{ color: "var(--color-primary,#0F1419)", fontWeight: 800, fontSize: "1.3rem" }}>Digitalin</span>
          </div>
          <h2 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.4rem", marginTop: 8 }}>Selamat Datang Kembali</h2>
          <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.9rem", marginTop: 4 }}>Masuk ke akun Digitalin kamu</p>
        </div>

        <div className="rounded-2xl p-8" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>
                Email
              </label>
              <input
                type="email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
                placeholder="email@example.com"
                className="w-full px-4 py-3 rounded-xl outline-none"
                style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)", backgroundColor: "var(--color-surface,#fff)" }}
                onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary,#2C5EF5)"; }}
                onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
              />
            </div>
            <div>
              <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>
                Password
              </label>
              <div className="relative">
                <input
                  type={showPass ? "text" : "password"}
                  value={form.password}
                  onChange={(e) => setForm({ ...form, password: e.target.value })}
                  placeholder="••••••••"
                  className="w-full px-4 py-3 pr-12 rounded-xl outline-none"
                  style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)", backgroundColor: "var(--color-surface,#fff)" }}
                  onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary,#2C5EF5)"; }}
                  onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                />
                <button type="button" onClick={() => setShowPass(!showPass)} className="absolute right-3 top-1/2 -translate-y-1/2" style={{ color: "var(--color-brand-secondary,#4A5568)" }}>
                  {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>

            <div className="text-right">
              <Link
                to="/forgot-password"
                style={{
                  color: "var(--color-brand-tertiary,#2C5EF5)",
                  fontSize: "0.82rem",
                  fontWeight: 500,
                  textDecoration: "none",
                }}
                className="hover:underline"
              >
                Lupa Password?
              </Link>
            </div>

            <button
              type="submit"
              disabled={anyLoading}
              className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2 transition-opacity mt-2"
              style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.95rem", opacity: anyLoading ? 0.7 : 1 }}
            >
              {loading && <Loader2 size={16} className="animate-spin" />}
              {loading ? "Masuk..." : "Masuk"}
            </button>
          </form>

          {googleClientId && (
            <GoogleLoginButton disabled={anyLoading} onLoadingChange={setGoogleLoading} />
          )}

          <p className="text-center mt-5" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.875rem" }}>
            Belum punya akun?{" "}
            <Link to="/register" style={{ color: "var(--color-brand-tertiary,#2C5EF5)", fontWeight: 600, textDecoration: "none" }}>
              Daftar sekarang
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
