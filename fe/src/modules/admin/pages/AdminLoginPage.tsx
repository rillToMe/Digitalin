import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { Eye, EyeOff, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import api, { adminAuthService } from "../../../services/api";
import type { AdminLoginPayload } from "../../../api/index";
import { useRecaptcha, RecaptchaError } from "../../../hooks/useRecaptcha";
import RecaptchaNotice from "../../../components/common/RecaptchaNotice";

/* Endpoint health BE (Program.cs: /health, 200, dikecualikan dari maintenance
   middleware). Origin diambil dari baseURL axios yang sama dengan app, jadi
   tidak ada baca env kedua. Diping pakai fetch, bukan axios, supaya tidak
   lewat interceptor ( Authorization header dsb.) — ping ini butuh respons
   2xx: ping ke /api/v1/ yang tak punya route membuat browser mencatat 404
   merah di console walau statusnya dianggap online. */
const HEALTH_URL = new URL(api.defaults.baseURL!).origin + "/health";

const APP_VERSION = import.meta.env.VITE_APP_VERSION ?? "dev";

/* "Login terakhir" dibaca sekali saat mount, ditulis lagi saat login sukses —
   cara paling murah mendeteksi akses yang tidak dikenal di perangkat ini. */
const LAST_LOGIN_KEY = "digitalin.admin.lastLogin";

const lastLoginFormat = new Intl.DateTimeFormat("id-ID", {
  weekday: "short",
  day: "numeric",
  month: "short",
  hour: "2-digit",
  minute: "2-digit",
});

const clockFormat = new Intl.DateTimeFormat("id-ID", {
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
});

const TZ_LABEL =
  Intl.DateTimeFormat().resolvedOptions().timeZone.split("/").pop()?.replace(/_/g, " ") ?? "—";

type ServerStatus = { state: "checking" | "online" | "offline"; ms: number | null };

/* Ping HTTP sungguhan ke BE: respons 2xx dari /health berarti online,
   kegagalan jaringan berarti offline. Latensi = round-trip yang benar-benar
   terukur, di-refresh tiap 30 detik. */
function useServerStatus(): ServerStatus {
  const [status, setStatus] = useState<ServerStatus>({ state: "checking", ms: null });

  useEffect(() => {
    let cancelled = false;

    const ping = async () => {
      const t0 = performance.now();
      try {
        const res = await fetch(HEALTH_URL, {
          method: "GET",
          cache: "no-store",
          signal: AbortSignal.timeout(5000),
        });
        if (!cancelled) {
          setStatus(
            res.ok
              ? { state: "online", ms: Math.round(performance.now() - t0) }
              : { state: "offline", ms: null },
          );
        }
      } catch {
        if (!cancelled) setStatus({ state: "offline", ms: null });
      }
    };

    void ping();
    const id = setInterval(() => void ping(), 30_000);
    return () => {
      cancelled = true;
      clearInterval(id);
    };
  }, []);

  return status;
}

function Clock() {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);

  return <time dateTime={now.toISOString()}>{clockFormat.format(now)}</time>;
}

export default function AdminLoginPage() {
  const navigate = useNavigate();
  const { setAdminToken } = useAuthStore();
  const { executeRecaptcha } = useRecaptcha();
  const [form, setForm] = useState({ username: "", password: "" });
  const [showPass, setShowPass] = useState(false);
  const server = useServerStatus();
  const [lastLogin] = useState(() => {
    const raw = localStorage.getItem(LAST_LOGIN_KEY);
    const date = raw ? new Date(raw) : null;
    return date && !Number.isNaN(date.getTime()) ? date : null;
  });

  const loginMutation = useMutation({
    /* captcha dijalankan DI DALAM mutationFn supaya isPending sudah true sejak
       tombol diklik. Kalau token-nya di-await dulu di handleSubmit, tombolnya
       diam sampai captcha selesai — terasa seperti klik yang tidak masuk. */
    mutationFn: async (payload: Omit<AdminLoginPayload, "captchaToken">) => {
      const captchaToken = await executeRecaptcha("admin_login");

      return adminAuthService.login({ ...payload, captchaToken });
    },
    onSuccess: (res) => {
      localStorage.setItem(LAST_LOGIN_KEY, new Date().toISOString());
      setAdminToken(res.token);
      toast.success("Login admin berhasil!");
      navigate("/admin/dashboard");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(
        err instanceof RecaptchaError
          ? err.message
          : err?.response?.data?.message ?? "Username atau password salah",
      );
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.username || !form.password) {
      toast.error("Isi semua field");
      return;
    }

    loginMutation.mutate({ username: form.username, password: form.password });
  };

  const loading = loginMutation.isPending;

  const statusText =
    server.state === "online"
      ? `API online${server.ms !== null ? ` · ${server.ms} ms` : ""}`
      : server.state === "checking"
        ? "Menghubungkan…"
        : "API tidak terjangkau";

  return (
    <div className="admin-login">
      <header className="admin-login-bar">
        <div className="admin-login-brand">
          <img src="/favicon.svg" alt="Digitalin" />
          <span className="admin-login-brand-name">Digitalin</span>
          <span className="admin-login-brand-role">Admin</span>
        </div>
        <p className="admin-login-status" role="status">
          <span className={`admin-login-dot admin-login-dot--${server.state}`} aria-hidden="true" />
          {statusText}
        </p>
      </header>

      <main className="admin-login-main">
        <div className="admin-login-inner">
          <h1 className="admin-login-title">Masuk ke dashboard</h1>
          <p className="admin-login-sub">
            Kelola pesanan top-up, voucher, dan konten Digitalin dari satu tempat.
          </p>

          <div className="admin-login-panel">
            <form onSubmit={handleSubmit}>
              <div className="admin-login-row">
                <label className="admin-login-field" htmlFor="username">Username</label>
                <input
                  id="username"
                  className="admin-login-input"
                  value={form.username}
                  onChange={(e) => setForm({ ...form, username: e.target.value })}
                  placeholder="admin"
                  autoComplete="username"
                  autoCapitalize="none"
                  spellCheck={false}
                  autoFocus
                  disabled={loading}
                />
              </div>

              <div className="admin-login-row">
                <label className="admin-login-field" htmlFor="password">Password</label>
                <div className="admin-login-passwrap">
                  <input
                    id="password"
                    className="admin-login-input admin-login-input--pass"
                    type={showPass ? "text" : "password"}
                    value={form.password}
                    onChange={(e) => setForm({ ...form, password: e.target.value })}
                    placeholder="••••••••"
                    autoComplete="current-password"
                    disabled={loading}
                  />
                  <button
                    type="button"
                    className="admin-login-toggle"
                    onClick={() => setShowPass(!showPass)}
                    aria-label={showPass ? "Sembunyikan password" : "Tampilkan password"}
                    tabIndex={-1}
                  >
                    {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                  </button>
                </div>
              </div>

              <button type="submit" className="admin-login-submit" disabled={loading}>
                {loading && <Loader2 size={16} className="animate-spin" />}
                {loading ? "Memeriksa…" : "Masuk"}
              </button>

              <p className="admin-login-help">
                Akun terkunci atau lupa password? Hubungi super admin untuk reset.
              </p>
            </form>
          </div>

          <p className="admin-login-lastlogin">
            {lastLogin
              ? `Login terakhir · ${lastLoginFormat.format(lastLogin)}`
              : "Belum ada riwayat login di perangkat ini"}
          </p>

          <RecaptchaNotice />
        </div>
      </main>

      <footer className="admin-login-foot">
        <span>v{APP_VERSION}</span>
        <span className="admin-login-foot-meta">
          {TZ_LABEL} · <Clock />
        </span>
      </footer>
    </div>
  );
}
