import { useEffect, useState } from "react";
import { useLocation } from "react-router";
import { Cookie } from "lucide-react";
import { toast } from "sonner";
import {
  readCookieConsent,
  resetCookieConsent,
  writeCookieConsent,
  type CookieConsent,
} from "../../utils/cookieConsent";
import { clearRecentGamesCookie } from "../../utils/recentGames";

/**
 * Event dari link "Pengaturan Cookie" (Footer) — hapus pilihan lama lalu
 * munculkan banner lagi supaya user bisa mengubah keputusannya.
 */
export const COOKIE_CONSENT_RESET_EVENT = "digitalin:cookie-consent-reset";

/**
 * Banner persetujuan cookie — tampil hanya bila user belum memilih
 * (cookie consent null/invalid). Dua aksi primer: Terima Semua /
 * Tolak Semua; tidak ada aksi ketiga.
 *
 * Cookie consent itu sendiri esensial (catatan pilihan hukum) sehingga
 * selalu ditulis. Satu-satunya cookie non-esensial saat ini ("Terakhir
 * Dibeli") hanya dibuat saat "Terima Semua"; "Tolak Semua" juga
 * menghapusnya. Cookie/localStorage esensial lain (login, tema) tidak
 * tersentuh.
 *
 * Mount di App (bukan per-layout) supaya juga tampil di halaman tanpa
 * CustomerLayout (login/register); area /admin dilewati — staf internal
 * bukan subjek persetujuan pengguna akhir.
 */
export default function CookieConsentBanner() {
  const location = useLocation();
  const [consent, setConsent] = useState<CookieConsent | null>(() =>
    readCookieConsent(),
  );

  /* "Pengaturan Cookie" di Footer → banner muncul lagi untuk mengubah
     pilihan. Listener, bukan state global — satu boolean tidak perlu store. */
  useEffect(() => {
    const reopen = () => setConsent(null);
    window.addEventListener(COOKIE_CONSENT_RESET_EVENT, reopen);
    return () => window.removeEventListener(COOKIE_CONSENT_RESET_EVENT, reopen);
  }, []);

  if (location.pathname.startsWith("/admin")) return null;
  if (consent !== null) return null;

  const decide = (choice: CookieConsent) => {
    writeCookieConsent(choice);
    if (choice === "rejected") {
      // Tarik persetujuan = buang data non-esensial yang pernah disimpan,
      // bukan cuma berhenti memperbarui.
      clearRecentGamesCookie();
      toast.info("Cookie opsional ditolak. Kamu bisa mengubahnya kapan saja lewat Pengaturan Cookie di footer.");
    } else {
      toast.success("Terima kasih! Cookie opsional diaktifkan.");
    }
    setConsent(choice);
  };

  return (
    <div
      role="dialog"
      aria-modal="false"
      aria-labelledby="cookie-consent-title"
      aria-describedby="cookie-consent-desc"
      className="fixed inset-x-0 bottom-0 z-40 px-3 pb-3 sm:px-4 sm:pb-4"
    >
      <div
        className="mx-auto flex max-w-3xl flex-col gap-4 rounded-2xl p-4 shadow-lg sm:flex-row sm:items-center sm:gap-5 sm:p-5"
        style={{
          backgroundColor: "var(--color-surface)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-card, 0 8px 30px rgba(0,0,0,0.12))",
        }}
      >
        <div className="flex min-w-0 items-start gap-3">
          <span
            className="flex h-8 w-8 shrink-0 items-center justify-center border"
            aria-hidden="true"
            style={{
              backgroundColor: "var(--color-hover-surface)",
              color: "var(--color-brand-secondary)",
            }}
          >
            <Cookie size={16} />
          </span>
          <div className="min-w-0">
            <h2
              id="cookie-consent-title"
              style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "0.9rem" }}
            >
              Pengaturan Cookie
            </h2>
            <p
              id="cookie-consent-desc"
              style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem", lineHeight: 1.5, marginTop: 2 }}
            >
              Kami memakai cookie esensial agar situs berfungsi (login, keamanan).
              Cookie opsional menyimpan daftar game <em>Terakhir Dibeli</em> di perangkatmu.
            </p>
          </div>
        </div>

        <div className="flex shrink-0 flex-col gap-2 sm:flex-row sm:gap-3">
          <button
            type="button"
            onClick={() => decide("rejected")}
            className="rounded-xl px-5 py-2.5"
            style={{
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface)",
              color: "var(--color-brand-primary)",
              fontSize: "0.82rem",
              fontWeight: 600,
            }}
          >
            Tolak Semua
          </button>
          <button
            type="button"
            onClick={() => decide("accepted")}
            className="rounded-xl px-5 py-2.5"
            style={{
              backgroundColor: "var(--color-brand-tertiary)",
              color: "var(--color-on-primary)",
              border: "1px solid var(--color-brand-tertiary)",
              fontSize: "0.82rem",
              fontWeight: 600,
            }}
          >
            Terima Semua
          </button>
        </div>
      </div>
    </div>
  );
}

/** Dipakai Footer: hapus pilihan lama → banner muncul lagi. */
export function reopenCookieConsent(): void {
  resetCookieConsent();
  window.dispatchEvent(new CustomEvent(COOKIE_CONSENT_RESET_EVENT));
}
