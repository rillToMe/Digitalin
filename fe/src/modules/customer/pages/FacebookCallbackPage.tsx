import { useEffect } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { useAuthStore } from "../../../store/authStore";
import PageLoader from "../../../components/common/PageLoader";

/**
 * Tujuan redirect backend setelah OAuth Facebook selesai. Backend mengarahkan
 * ke sini membawa hasil di URL fragment (fragment tidak pernah dikirim ke
 * server mana pun — token tidak bocor ke log HTTP / referrer):
 *   #token=<jwt>  → login sukses
 *   #error=<pesan user-friendly> → gagal (cancel, state invalid, API error)
 *
 * Komponen ini hanya parsing + menyerahkan token ke auth store — tidak ada
 * duplikasi logika auth di frontend.
 */
export default function FacebookCallbackPage() {
  const navigate = useNavigate();
  const setToken = useAuthStore((s) => s.setToken);

  useEffect(() => {
    const params = new URLSearchParams(window.location.hash.replace(/^#/, ""));
    const token = params.get("token");
    const error = params.get("error");

    // Bersihkan fragment dari address bar + history supaya JWT tidak
    // tertinggal di URL yang bisa di-share / dibaca back-button.
    window.history.replaceState(null, "", window.location.pathname);

    if (token) {
      setToken(token);
      toast.success("Login Facebook berhasil!");
      navigate("/", { replace: true });
    } else {
      toast.error(error ?? "Login Facebook gagal. Silakan coba lagi.");
      navigate("/login", { replace: true });
    }
  }, [navigate, setToken]);

  return <PageLoader />;
}
