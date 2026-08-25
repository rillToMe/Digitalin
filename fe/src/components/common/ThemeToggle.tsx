import { Moon, Sun } from "lucide-react";
import { useThemeStore } from "../../store/themeStore";

/**
 * Tombol ganti tema. Ikonnya menunjukkan tema TUJUAN (matahari saat sekarang
 * gelap), sama seperti label aksesibilitasnya — kalau ikon menunjukkan tema
 * yang sedang aktif, arah tombolnya jadi ambigu.
 *
 * Warna, hover, dan press semuanya dari layer global: komponen ini tidak tahu
 * tema apa pun selain "gelap atau tidak".
 */
export default function ThemeToggle({ className = "" }: { className?: string }) {
  const theme = useThemeStore((state) => state.theme);
  const toggleTheme = useThemeStore((state) => state.toggleTheme);

  const isDark = theme === "dark";
  const label = isDark ? "Ganti ke tema terang" : "Ganti ke tema gelap";

  return (
    <button
      type="button"
      onClick={toggleTheme}
      aria-label={label}
      title={label}
      className={`flex h-9 w-9 items-center justify-center rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary) ${className}`}
      style={{ color: "var(--color-brand-primary)" }}
    >
      {isDark ? <Sun size={18} aria-hidden="true" /> : <Moon size={18} aria-hidden="true" />}
    </button>
  );
}
