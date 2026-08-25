import type { LucideIcon } from "lucide-react";
import {
  LayoutGrid,
  Smartphone,
  Monitor,
  Ticket,
  Wallet,
  Signal,
  Gamepad2,
  Flame,
  MoreHorizontal,
} from "lucide-react";

/**
 * KONFIGURASI UI — Kategori game storefront.
 *
 * Sumber kanonis data ada di backend:
 *   1. GET /games/categories        → daftar kategori + nama tampilan + brandCount
 *                                     (termasuk bucket virtual "all" & "lainnya")
 *   2. GET /games/brands?category=  → brand per kategori, dipaginasi (offset/limit)
 *
 * File ini hanya menyimpan concern FE: ikon per kategori, nama fallback saat
 * endpoint belum termuat, dan ukuran batch fetch per section.
 */

/** Slug kategori virtual "tampilkan semua" */
export const ALL_CATEGORY_SLUG = "all";

/** Slug fallback untuk brand yang belum terpetakan ke kategori mana pun */
export const UNCATEGORIZED_CATEGORY_SLUG = "lainnya";

/**
 * Slug section virtual "Populer" — isinya dari GET /games/brands/popular
 * (kurasi manual admin), bukan dari registry kategori. Slug ini reserved di BE
 * supaya admin tidak bisa membuat kategori dengan slug sama.
 */
export const POPULAR_CATEGORY_SLUG = "populer";

/**
 * Definisi kategori yang dikenali storefront — dipakai sebagai fallback saat
 * GET /games/categories belum termuat, dan sebagai opsi di editor metadata admin.
 */
export const CATEGORY_DEFINITIONS: { slug: string; name: string }[] = [
  { slug: "mobile", name: "Mobile Game" },
  { slug: "pc", name: "PC Game" },
  { slug: "voucher", name: "Voucher" },
  { slug: "e-money", name: "E-Money" },
  { slug: "pulsa-data", name: "Pulsa & Data" },
];

/** Nama tampilan per slug kategori (termasuk virtual & fallback) */
export const CATEGORY_NAMES: Record<string, string> = {
  [ALL_CATEGORY_SLUG]: "Semua",
  [POPULAR_CATEGORY_SLUG]: "Populer",
  [UNCATEGORIZED_CATEGORY_SLUG]: "Lainnya",
  ...Object.fromEntries(CATEGORY_DEFINITIONS.map((c) => [c.slug, c.name])),
};

/** Ikon per slug kategori — concern UI, tetap di frontend meski BE sudah ready */
export const CATEGORY_ICONS: Record<string, LucideIcon> = {
  [ALL_CATEGORY_SLUG]: LayoutGrid,
  [POPULAR_CATEGORY_SLUG]: Flame,
  mobile: Smartphone,
  pc: Monitor,
  voucher: Ticket,
  "e-money": Wallet,
  "pulsa-data": Signal,
  [UNCATEGORIZED_CATEGORY_SLUG]: MoreHorizontal,
};

/** Kategori custom dari admin tidak punya ikon bawaan — jangan render tanpa ikon. */
export function categoryIcon(slug: string): LucideIcon {
  return CATEGORY_ICONS[slug] ?? Gamepad2;
}

/** Batas validasi PUT /admin/system/game-categories (dicerminkan dari BE). */
export const MAX_CATEGORIES = 30;
export const MAX_CATEGORY_NAME_LENGTH = 40;

/**
 * Pratinjau slug yang akan dibuat BE dari nama kategori baru.
 * Cermin dari ProductService.NormalizeCategory: trim → lowercase →
 * whitespace jadi "-". Hanya untuk ditampilkan; BE tetap yang menentukan.
 */
export function categorySlugPreview(name: string): string {
  return name.trim().toLowerCase().split(/\s+/).filter(Boolean).join("-");
}

/**
 * Ukuran batch fetch per section homepage. Section mengambil batch pertama saat
 * mount, lalu batch berikutnya (lebih besar) tiap "Tampilkan lebih banyak" —
 * bukan seluruh katalog beserta ikonnya dalam satu response.
 *
 * Semua nilai harus ≤ 60 (clamp MaxBrandPageSize di ProductEndpoints).
 */
export const INITIAL_FETCH_LIMIT = 12;

/** "Lainnya" isinya brand yang belum dikurasi admin — batch awalnya lebih kecil. */
export const INITIAL_FETCH_LIMIT_UNCATEGORIZED = 7;

export const MORE_FETCH_LIMIT = 24;

export function initialFetchLimit(categorySlug: string): number {
  return categorySlug === UNCATEGORIZED_CATEGORY_SLUG
    ? INITIAL_FETCH_LIMIT_UNCATEGORIZED
    : INITIAL_FETCH_LIMIT;
}

/**
 * Offset batch berikutnya untuk useInfiniteQuery, atau undefined bila section
 * sudah lengkap. Offset = jumlah brand yang sudah termuat (bukan nomor halaman,
 * karena batch pertama sengaja lebih kecil dari batch berikutnya).
 *
 * `lastBatchSize` 0 juga menghentikan fetch: kalau brandCount lebih besar dari
 * isi katalog sebenarnya (brand dinonaktifkan di antara dua request), tombol
 * "lebih banyak" tidak boleh memanggil server tanpa henti.
 */
export function nextBrandOffset(
  loaded: number,
  total: number,
  lastBatchSize: number,
): number | undefined {
  return lastBatchSize > 0 && loaded < total ? loaded : undefined;
}
