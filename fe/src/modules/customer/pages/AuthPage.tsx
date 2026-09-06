import { useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { Eye, EyeOff, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { authService } from "../../../services/api";
import { useRecaptcha, RecaptchaError } from "../../../hooks/useRecaptcha";
import { useAuthStore } from "../../../store/authStore";
import type { LoginPayload, RegisterPayload } from "../../../api/index";
import OtpInput from "../../../components/auth/OtpInput";
import GoogleLoginButton, { googleClientId } from "../../../components/common/GoogleLoginButton";
import FacebookLoginButton from "../../../components/common/FacebookLoginButton";
import RecaptchaNotice from "../../../components/common/RecaptchaNotice";
import BackButton from "../../../components/common/BackButton";

/** BE membatasi request OTP 3x / 10 menit per IP, jadi kirim ulang dikunci dulu. */
const RESEND_COOLDOWN_SECONDS = 60;

const inputStyle = {
  border: "1.5px solid var(--color-border-input)",
  fontSize: "0.9rem",
  color: "var(--color-brand-primary)",
  backgroundColor: "var(--color-surface)",
};

const labelStyle = {
  color: "var(--color-brand-primary)",
  fontSize: "0.85rem",
  fontWeight: 500,
  display: "block" as const,
  marginBottom: 6,
};

/** Pemisah "atau" sebelum tombol login sosial (Google / Facebook). */
function SocialDivider() {
  return (
    <div className="flex items-center gap-3 my-5">
      <div className="flex-1 h-px" style={{ backgroundColor: "var(--color-border-light)" }} />
      <span className="text-sm" style={{ color: "var(--color-brand-secondary)" }}>atau</span>
      <div className="flex-1 h-px" style={{ backgroundColor: "var(--color-border-light)" }} />
    </div>
  );
}

function LoginForm() {
  const navigate = useNavigate();
  const { setToken } = useAuthStore();
  const { executeRecaptcha } = useRecaptcha();
  const [form, setForm] = useState({ email: "", password: "" });
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
    <>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="email" style={labelStyle}>Email</label>
          <input
            id="email"
            type="email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            placeholder="email@example.com"
            autoComplete="email"
            disabled={anyLoading}
            className="w-full px-4 py-3 rounded-xl outline-none"
            style={inputStyle}
            onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
            onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
          />
        </div>

        <div>
          <label htmlFor="password" style={labelStyle}>Password</label>
          <div className="relative">
            <input
              id="password"
              type={showPass ? "text" : "password"}
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              placeholder="••••••••"
              autoComplete="current-password"
              disabled={anyLoading}
              className="w-full px-4 py-3 pr-12 rounded-xl outline-none"
              style={inputStyle}
              onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
              onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
            />
            <button
              type="button"
              onClick={() => setShowPass(!showPass)}
              className="absolute right-3 top-1/2 -translate-y-1/2"
              style={{ color: "var(--color-brand-secondary)" }}
              aria-label={showPass ? "Sembunyikan password" : "Tampilkan password"}
              tabIndex={-1}
            >
              {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>
        </div>

        <div className="text-right">
          <Link
            to="/forgot-password"
            style={{
              color: "var(--color-brand-tertiary)",
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
          className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2 disabled:opacity-60"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "var(--color-on-primary)",
            fontWeight: 600,
            fontSize: "0.95rem",
          }}
        >
          {loading && <Loader2 size={16} className="animate-spin" />}
          {loading ? "Masuk..." : "Masuk"}
        </button>

        <SocialDivider />

        <div className="space-y-3">
          {googleClientId && (
            <GoogleLoginButton disabled={anyLoading} onLoadingChange={setGoogleLoading} />
          )}
          <FacebookLoginButton disabled={anyLoading} />
        </div>
      </form>

      <p className="text-center mt-5" style={{ color: "var(--color-brand-secondary)", fontSize: "0.875rem" }}>
        Belum punya akun?{" "}
        <Link to="/register" style={{ color: "var(--color-brand-tertiary)", fontWeight: 600, textDecoration: "none" }}>
          Daftar sekarang
        </Link>
      </p>
    </>
  );
}

function RegisterForm() {
  const navigate = useNavigate();
  const { executeRecaptcha } = useRecaptcha();
  const { setToken } = useAuthStore();
  const [form, setForm] = useState({ email: "", password: "", fullName: "", otpCode: "" });
  const [showPass, setShowPass] = useState(false);
  const [otpSent, setOtpSent] = useState(false);
  const [cooldown, setCooldown] = useState(0);
  const [googleLoading, setGoogleLoading] = useState(false);

  useEffect(() => {
    if (cooldown === 0) return;
    const timer = setInterval(() => setCooldown((s) => s - 1), 1000);
    return () => clearInterval(timer);
  }, [cooldown]);

  const otpMutation = useMutation({
    /* captcha dijalankan DI DALAM mutationFn supaya isPending sudah true sejak
       tombol diklik. Kalau token-nya di-await dulu di sendOtp, tombolnya diam
       sampai captcha selesai — terasa seperti klik yang tidak masuk. */
    mutationFn: async (email: string) => {
      const captchaToken = await executeRecaptcha("register_request_otp");

      return authService.requestOtp({ email, captchaToken });
    },
    onSuccess: () => {
      toast.success("Kode OTP terkirim ke email kamu");
      setOtpSent(true);
      setCooldown(RESEND_COOLDOWN_SECONDS);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(
        err instanceof RecaptchaError
          ? err.message
          : err?.response?.data?.message ?? "Gagal mengirim OTP",
      );
    },
  });

  const registerMutation = useMutation({
    mutationFn: (payload: RegisterPayload) => authService.register(payload),
    onSuccess: (res) => {
      setToken(res.token);
      toast.success("Registrasi berhasil!");
      navigate("/");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Registrasi gagal");
    },
  });

  const sendOtp = () => {
    if (!form.fullName || !form.email || !form.password) {
      toast.error("Lengkapi nama, email, dan password dulu");
      return;
    }
    if (form.password.length < 8) {
      toast.error("Password minimal 8 karakter");
      return;
    }
    if (cooldown > 0) return;

    otpMutation.mutate(form.email);
  };

  const handleRegister = (e: React.FormEvent) => {
    e.preventDefault();
    if (!otpSent) {
      sendOtp();
      return;
    }
    if (form.otpCode.length !== 6) {
      toast.error("Masukkan 6 digit kode OTP");
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

  return (
    <>
      <form onSubmit={handleRegister} className="space-y-4">
        <div>
          <label htmlFor="fullName" style={labelStyle}>Nama Lengkap</label>
          <input
            id="fullName"
            value={form.fullName}
            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
            placeholder="John Doe"
            autoComplete="name"
            disabled={anyLoading}
            className="w-full px-4 py-3 rounded-xl outline-none"
            style={inputStyle}
            onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
            onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
          />
        </div>

        <div>
          <label htmlFor="email" style={labelStyle}>Email</label>
          <input
            id="email"
            type="email"
            value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })}
            placeholder="email@example.com"
            autoComplete="email"
            disabled={anyLoading}
            className="w-full px-4 py-3 rounded-xl outline-none"
            style={inputStyle}
            onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
            onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
          />
        </div>

        <div>
          <label htmlFor="password" style={labelStyle}>Password</label>
          <div className="relative">
            <input
              id="password"
              type={showPass ? "text" : "password"}
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              placeholder="Minimal 8 karakter"
              autoComplete="new-password"
              disabled={anyLoading}
              className="w-full px-4 py-3 pr-12 rounded-xl outline-none"
              style={inputStyle}
              onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
              onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
            />
            <button
              type="button"
              onClick={() => setShowPass(!showPass)}
              className="absolute right-3 top-1/2 -translate-y-1/2"
              style={{ color: "var(--color-brand-secondary)" }}
              aria-label={showPass ? "Sembunyikan password" : "Tampilkan password"}
              tabIndex={-1}
            >
              {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
            </button>
          </div>
        </div>
        {/* Blok OTP muncul in-place setelah kode terkirim — tidak pindah
            halaman, jadi nama/email/password tetap terlihat dan bisa
            diperbaiki. Isinya baru di-mount saat terbuka supaya autofocus
            OtpInput tidak mencuri fokus ketika form pertama dibuka.
            marginTop 0 saat tertutup: panel tinggi 0 tapi space-y-4 tetap
            menyumbang jarak, jadi akan ada celah ganda. */}
        <div
          className="collapse-panel"
          data-open={otpSent}
          style={otpSent ? undefined : { marginTop: 0 }}
        >
          <div>
            <div className="pt-1 space-y-3">
              <div
                className="rounded-xl px-4 py-3"
                style={{ backgroundColor: "var(--color-info-bg)" }}
              >
                <p style={{ color: "var(--color-info)", fontSize: "0.8rem", textAlign: "center" }}>
                  Kode OTP dikirim ke{" "}
                  <strong>{form.email}</strong>
                </p>
              </div>

              <div>
                <span style={{ ...labelStyle, textAlign: "center" }}>Masukkan 6 Digit Kode OTP</span>
                {otpSent && (
                  <OtpInput
                    value={form.otpCode}
                    onChange={(otpCode) => setForm((f) => ({ ...f, otpCode }))}
                  />
                )}
              </div>

              <div className="flex justify-center" style={{ fontSize: "0.78rem" }}>
                {cooldown > 0 ? (
                  <p style={{ color: "var(--color-brand-secondary)" }}>
                    Kirim ulang kode dalam{" "}
                    <span style={{ color: "var(--color-brand-tertiary)", fontWeight: 600 }}>{cooldown} detik</span>
                  </p>
                ) : (
                  <button
                    type="button"
                    onClick={sendOtp}
                    disabled={anyLoading}
                    style={{ color: "var(--color-brand-tertiary)", fontWeight: 600 }}
                  >
                    {otpMutation.isPending ? "Mengirim..." : "Kirim Ulang Kode"}
                  </button>
                )}
              </div>
            </div>
          </div>
        </div>

        <button
          type="submit"
          disabled={anyLoading || (otpSent && form.otpCode.length !== 6)}
          className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2 disabled:opacity-60"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "var(--color-on-primary)",
            fontWeight: 600,
            fontSize: "0.95rem",
          }}
        >
          {loading && <Loader2 size={16} className="animate-spin" />}
          {otpSent ? "Daftar Sekarang" : "Kirim Kode OTP"}
        </button>

        {!otpSent && (
          <>
            <SocialDivider />

            <div className="space-y-3">
              {googleClientId && (
                <GoogleLoginButton disabled={anyLoading} onLoadingChange={setGoogleLoading} />
              )}
              <FacebookLoginButton disabled={anyLoading} />
            </div>
          </>
        )}
      </form>

      <p className="text-center mt-5" style={{ color: "var(--color-brand-secondary)", fontSize: "0.875rem" }}>
        Sudah punya akun?{" "}
        <Link to="/login" style={{ color: "var(--color-brand-tertiary)", fontWeight: 600, textDecoration: "none" }}>
          Masuk
        </Link>
      </p>
    </>
  );
}

type Mode = "login" | "register";

export default function AuthPage() {
  const location = useLocation();
  const mode: Mode = location.pathname === "/register" ? "register" : "login";

  const heading =
    mode === "login"
      ? { title: "Selamat Datang Kembali", subtitle: "Masuk ke akun Digitalin kamu" }
      : { title: "Buat Akun Baru", subtitle: "Isi data diri kamu, lalu verifikasi email" };

  return (
    <div className="auth-hide-scrollbar h-dvh overflow-y-auto flex px-4 py-12" style={{ backgroundColor: "var(--color-neutral)" }}>
      <div className="w-full max-w-md m-auto">
        {/* Kembali — di atas kartu; fallback ke home saat tanpa riwayat SPA. */}
        <div className="mb-4">
          <BackButton />
        </div>

        {/* Logo */}
        <div className="text-center mb-4">
          <div className="inline-flex items-center gap-2">
            <img src="/favicon.svg" alt="Digitalin" style={{ height: 36, width: "auto" }} />
            <span style={{ color: "var(--color-brand-primary)", fontWeight: 800, fontSize: "1.3rem" }}>Digitalin</span>
          </div>
        </div>

        <div>
          <div className="text-center mb-8">
            <h2 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.4rem" }}>{heading.title}</h2>
            <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.9rem", marginTop: 4 }}>
              {heading.subtitle}
            </p>
          </div>

          <div className="rounded-2xl p-8" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
            {mode === "login" ? <LoginForm /> : <RegisterForm />}
          </div>

          <RecaptchaNotice />
        </div>
      </div>
    </div>
  );
}
