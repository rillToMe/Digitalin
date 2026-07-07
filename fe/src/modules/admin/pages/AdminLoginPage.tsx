import { useState } from "react";
import { useNavigate } from "react-router";
import { Eye, EyeOff, Zap, Loader2, ShieldCheck } from "lucide-react";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import { adminAuthService } from "../../../services/api";
import type { AdminLoginPayload } from "../../../api/index";
import { useRecaptcha } from "../../../hooks/useRecaptcha";

export default function AdminLoginPage() {
  const navigate = useNavigate();
  const { setAdminToken } = useAuthStore();
  const { executeRecaptcha } = useRecaptcha();
  const [form, setForm] = useState({ username: "", password: "" });
  const [showPass, setShowPass] = useState(false);

  const loginMutation = useMutation({
    mutationFn: (payload: AdminLoginPayload) => adminAuthService.login(payload),
    onSuccess: (res) => {
      setAdminToken(res.token);
      toast.success("Login admin berhasil!");
      navigate("/admin/dashboard");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? "Username atau password salah";
      toast.error(msg);
    },
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.username || !form.password) {
      toast.error("Isi semua field");
      return;
    }

    try {
      const captchaToken = await executeRecaptcha("admin_login");

      loginMutation.mutate({
        username: form.username,
        password: form.password,
        captchaToken,
      });
    } catch {
      toast.error(
        "Verifikasi keamanan gagal. Silakan coba lagi.",
      );
    }
  };

  const loading = loginMutation.isPending;

  return (
    <div className="min-h-screen flex items-center justify-center px-4" style={{ backgroundColor: "var(--color-brand-primary,#0F1419)" }}>
      <div className="w-full max-w-sm">
        <div className="text-center mb-8">
          <div className="inline-flex items-center gap-2 mb-4">
            <div style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", borderRadius: 10 }} className="w-10 h-10 flex items-center justify-center">
              <Zap size={20} color="#fff" fill="#fff" />
            </div>
            <span style={{ color: "#fff", fontWeight: 800, fontSize: "1.3rem" }}>Digitalin</span>
          </div>
          <div className="flex items-center justify-center gap-2 mb-2">
            <ShieldCheck size={16} style={{ color: "rgba(255,255,255,0.5)" }} />
            <span style={{ color: "rgba(255,255,255,0.5)", fontSize: "0.8rem", textTransform: "uppercase", letterSpacing: "0.1em" }}>Admin Panel</span>
          </div>
          <h2 style={{ color: "#fff", fontWeight: 700, fontSize: "1.3rem" }}>Masuk ke Dashboard</h2>
        </div>

        <div className="rounded-2xl p-7" style={{ backgroundColor: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.1)" }}>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label style={{ color: "rgba(255,255,255,0.7)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Username</label>
              <input
                value={form.username}
                onChange={(e) => setForm({ ...form, username: e.target.value })}
                placeholder="admin"
                className="w-full px-4 py-3 rounded-xl outline-none"
                style={{ backgroundColor: "rgba(255,255,255,0.08)", border: "1.5px solid rgba(255,255,255,0.12)", color: "#fff", fontSize: "0.9rem" }}
                onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary,#2C5EF5)"; }}
                onBlur={(e) => { e.target.style.borderColor = "rgba(255,255,255,0.12)"; }}
              />
            </div>
            <div>
              <label style={{ color: "rgba(255,255,255,0.7)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Password</label>
              <div className="relative">
                <input
                  type={showPass ? "text" : "password"}
                  value={form.password}
                  onChange={(e) => setForm({ ...form, password: e.target.value })}
                  placeholder="••••••••"
                  className="w-full px-4 py-3 pr-12 rounded-xl outline-none"
                  style={{ backgroundColor: "rgba(255,255,255,0.08)", border: "1.5px solid rgba(255,255,255,0.12)", color: "#fff", fontSize: "0.9rem" }}
                  onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary,#2C5EF5)"; }}
                  onBlur={(e) => { e.target.style.borderColor = "rgba(255,255,255,0.12)"; }}
                />
                <button type="button" onClick={() => setShowPass(!showPass)} className="absolute right-3 top-1/2 -translate-y-1/2" style={{ color: "rgba(255,255,255,0.5)" }}>
                  {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>
            <button
              type="submit"
              disabled={loading}
              className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2 mt-2"
              style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.95rem", opacity: loading ? 0.7 : 1 }}
            >
              {loading && <Loader2 size={16} className="animate-spin" />}
              {loading ? "Masuk..." : "Masuk"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
