import { ChevronLeft } from "lucide-react";
import { useNavigate } from "react-router";

/**
 * Tombol kembali generik untuk halaman customer. navigate(-1) bila ada
 * riwayat SPA (history.state.idx > 0); deep link / tab baru → fallback ke
 * home supaya tidak "keluar" dari aplikasi. Ikon: chevron-left (lucide).
 */
export default function BackButton({ label = "Kembali" }: { label?: string }) {
  const navigate = useNavigate();

  const hasHistory =
    typeof window.history.state?.idx === "number" && window.history.state.idx > 0;

  return (
    <button
      type="button"
      onClick={() => (hasHistory ? navigate(-1) : navigate("/"))}
      className="pressable flex items-center gap-1 mb-5 no-underline"
      style={{
        color: "var(--color-brand-secondary)",
        fontSize: "0.85rem",
        fontWeight: 500,
        padding: 0,
        background: "none",
        border: "none",
        cursor: "pointer",
      }}
    >
      <ChevronLeft size={16} />
      <span>{label}</span>
    </button>
  );
}
