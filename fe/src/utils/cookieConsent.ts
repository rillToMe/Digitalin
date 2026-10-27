/**
 * Persetujuan cookie (cookie consent) — SSOT satu-satunya.
 *
 * Cookie consent sendiri adalah cookie ESSENSIAL: ia menyimpan pilihan
 * hukum pengguna (terima/tolak), bukan data pelacakan opsional — jadi selalu
 * boleh ditulis dan tidak pernah digerbangi oleh consent. Satu-satunya
 * cookie non-esensial saat ini adalah "Terakhir Dibeli" (recentGames), yang
 * gerbangnya ada di utils/recentGames.ts via hasCookieConsent().
 *
 * Tidak ada consent/privacy mechanism lain di FE saat file ini dibuat;
 * "ubah pilihan nanti" dilayani link "Pengaturan Cookie" di Footer yang
 * memunculkan ulang banner (event digitalin:cookie-consent-reset).
 */

export const COOKIE_CONSENT_COOKIE = "digitalin_cookie_consent";

export type CookieConsent = "accepted" | "rejected";

/** 180 hari — pilihan bertahan lama tapi tidak permanen. */
const MAX_AGE_SECONDS = 180 * 24 * 60 * 60;

/**
 * Nilai mentah cookie → pilihan valid. Nilai asing (rusak, diedit tangan,
 * versi lama) jatuh ke null = belum memilih = banner tampil lagi — pilihan
 * yang tidak dikenali tidak boleh dianggap persetujuan.
 */
export function parseConsentValue(raw: string | null | undefined): CookieConsent | null {
  const value = raw?.trim().toLowerCase();
  return value === "accepted" || value === "rejected" ? value : null;
}

/** Baca pilihan dari cookie browser; null = belum memilih. */
export function readCookieConsent(): CookieConsent | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${COOKIE_CONSENT_COOKIE}=`));
  if (!match) return null;
  return parseConsentValue(
    decodeURIComponent(match.slice(COOKIE_CONSENT_COOKIE.length + 1)),
  );
}

/**
 * Simpan pilihan (cookie esensial — selalu boleh ditulis). Pembersihan
 * cookie non-esensial saat "rejected" dilakukan pemanggil (banner), bukan
 * di sini, supaya modul ini tidak bergantung balik ke recentGames.
 */
export function writeCookieConsent(choice: CookieConsent): void {
  if (typeof document === "undefined") return;
  const secure = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie =
    `${COOKIE_CONSENT_COOKIE}=${encodeURIComponent(choice)}` +
    `; Max-Age=${MAX_AGE_SECONDS}; Path=/; SameSite=Lax${secure}`;
}

/**
 * Hapus pilihan — banner muncul lagi. Dipakai link "Pengaturan Cookie"
 * di Footer untuk mengubah pilihan.
 */
export function resetCookieConsent(): void {
  if (typeof document === "undefined") return;
  document.cookie = `${COOKIE_CONSENT_COOKIE}=; Max-Age=0; Path=/; SameSite=Lax`;
}

/** True hanya bila pengguna memilih "Terima Semua". Null/rejected = false. */
export function hasCookieConsent(): boolean {
  return readCookieConsent() === "accepted";
}
