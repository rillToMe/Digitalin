/**
 * Cookie "Terakhir Dibeli" (Recently) — guest only.
 *
 * Isi HANYA slug katalog publik (identifier yang sama dengan URL
 * /game/{slug}) — tidak ada nomor invoice, player ID, data pembayaran,
 * kredensial, atau data transaksi lain. Cookie ini bukan bukti pembelian
 * maupun otorisasi: backend memvalidasi ulang setiap slug terhadap katalog
 * aktif, dan user login selalu memakai riwayat DB.
 *
 * Cookie ini NON-ESSENSIAL — hanya boleh dibuat/diperbarui setelah user
 * memilih "Terima Semua" (gate hasCookieConsent). Membacanya tetap bebas
 * (cookie yang tak pernah ditulis terbaca kosong). Cookie consent-nya
 * sendiri esensial dan tidak digerbangi (lihat cookieConsent.ts).
 */

import { hasCookieConsent } from "./cookieConsent";

export const RECENT_GAMES_COOKIE = "digitalin_recent_games";

/** Batas jumlah game — cermin RecentBrandLimit (BE) & MaxRecentSlugs (controller). */
export const RECENT_GAMES_MAX = 12;

/** 60 hari — recency yang wajar tanpa menyimpan jejak permanen. */
const MAX_AGE_SECONDS = 60 * 24 * 60 * 60;

/** Slug valid = kebab-case [a-z0-9-], cermin aturan Domain.Slug di BE. */
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/**
 * Nilai mentah cookie → slug bersih: entri invalid/kosong dibuang, huruf
 * besar dinormalisasi, duplikat dihapus (pertahankan kemunculan pertama),
 * jumlah dibatasi RECENT_GAMES_MAX. Input rusak/diedit tangan jatuh aman
 * ke daftar lebih pendek / kosong — tidak pernah melempar.
 */
export function parseRecentSlugs(raw: string | null | undefined): string[] {
  if (!raw) return [];

  const seen = new Set<string>();
  const slugs: string[] = [];
  for (const entry of raw.split(",")) {
    const slug = entry.trim().toLowerCase();
    if (!slug || !SLUG_PATTERN.test(slug) || seen.has(slug)) continue;
    seen.add(slug);
    slugs.push(slug);
    if (slugs.length === RECENT_GAMES_MAX) break;
  }
  return slugs;
}

/** Daftar slug → nilai cookie (join koma; kosong = string kosong). */
export function serializeRecentSlugs(slugs: string[]): string {
  return slugs.slice(0, RECENT_GAMES_MAX).join(",");
}

/** Slug baru dipindah ke depan (paling baru dulu), tetap tanpa duplikat & bounded. */
export function upsertRecentSlug(slugs: string[], slug: string): string[] {
  const clean = slug.trim().toLowerCase();
  if (!clean || !SLUG_PATTERN.test(clean)) return slugs;
  return [clean, ...slugs.filter((s) => s !== clean)].slice(0, RECENT_GAMES_MAX);
}

/** Baca cookie browser → slug bersih. */
export function readRecentSlugs(): string[] {
  if (typeof document === "undefined") return [];
  const match = document.cookie
    .split(";")
    .map((part) => part.trim())
    .find((part) => part.startsWith(`${RECENT_GAMES_COOKIE}=`));
  if (!match) return [];
  return parseRecentSlugs(decodeURIComponent(match.slice(RECENT_GAMES_COOKIE.length + 1)));
}

/** Tulis cookie (best-effort; kegagalan pasang cookie tidak boleh mengganggu flow pembelian). */
export function writeRecentSlugs(slugs: string[]): void {
  if (typeof document === "undefined") return;
  const secure = window.location.protocol === "https:" ? "; Secure" : "";
  document.cookie =
    `${RECENT_GAMES_COOKIE}=${encodeURIComponent(serializeRecentSlugs(slugs))}` +
    `; Max-Age=${MAX_AGE_SECONDS}; Path=/; SameSite=Lax${secure}`;
}

/**
 * Hapus cookie Recently (Max-Age=0). Dipanggil saat user memilih "Tolak
 * Semua" — menarik persetujuan berarti data non-esensial ikut dibuang,
 * bukan cuma berhenti diperbarui.
 */
export function clearRecentGamesCookie(): void {
  if (typeof document === "undefined") return;
  document.cookie = `${RECENT_GAMES_COOKIE}=; Max-Age=0; Path=/; SameSite=Lax`;
}

/**
 * Catat satu pembelian sukses: baca → upsert → tulis. Digerbangi consent:
 * tanpa "Terima Semua" cookie TIDAK dibuat/diperbarui (return null).
 * Mengembalikan slug hasil normalisasi, atau null bila slug tidak layak
 * atau consent belum diberikan.
 */
export function addRecentSlug(slug: string): string | null {
  if (!hasCookieConsent()) return null;
  const clean = slug.trim().toLowerCase();
  if (!SLUG_PATTERN.test(clean)) return null;
  writeRecentSlugs(upsertRecentSlug(readRecentSlugs(), clean));
  return clean;
}
