import type { LucideIcon } from "lucide-react";
import {
  LayoutGrid,
  Smartphone,
  Monitor,
  Ticket,
  Wallet,
  Signal,
  MoreHorizontal,
} from "lucide-react";

/**
 * KONFIGURASI FALLBACK — Kategori game storefront.
 *
 * Sumber kanonis sekarang dari backend:
 *   1. GET /games/categories  → daftar kategori + nama tampilan
 *   2. BrandItem.categorySlug → kategori per brand (dari kolom GameMetadata.Category)
 *
 * File ini tetap dipakai untuk:
 *   - ikon & nama kategori virtual ("Semua" / "Lainnya") — concern UI, tetap di FE
 *   - fallback saat endpoint belum termuat (CATEGORY_DEFINITIONS)
 *   - mapping transisi brand yang categorySlug-nya masih null di DB (BRAND_CATEGORY_MAP)
 *
 * BRAND_CATEGORY_MAP bisa dihapus setelah seluruh brand terkategorisasi di admin.
 */

/** Slug kategori virtual "tampilkan semua" */
export const ALL_CATEGORY_SLUG = "all";

/** Slug fallback untuk brand yang belum terpetakan ke kategori mana pun */
export const UNCATEGORIZED_CATEGORY_SLUG = "lainnya";

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
  [UNCATEGORIZED_CATEGORY_SLUG]: "Lainnya",
  ...Object.fromEntries(CATEGORY_DEFINITIONS.map((c) => [c.slug, c.name])),
};

/** Ikon per slug kategori — concern UI, tetap di frontend meski BE sudah ready */
export const CATEGORY_ICONS: Record<string, LucideIcon> = {
  [ALL_CATEGORY_SLUG]: LayoutGrid,
  mobile: Smartphone,
  pc: Monitor,
  voucher: Ticket,
  "e-money": Wallet,
  "pulsa-data": Signal,
  [UNCATEGORIZED_CATEGORY_SLUG]: MoreHorizontal,
};

/**
 * Mapping transisi slug brand → slug kategori, dipakai hanya saat
 * brand.categorySlug masih null (admin belum mengisi kategori di CMS).
 */
export const BRAND_CATEGORY_MAP: Record<string, string> = {
  "mobile-legends": "mobile",
  "free-fire": "mobile",
  "pubg-mobile": "mobile",
  "honkai-star-rail": "mobile",
  "genshin-impact": "mobile",
  valorant: "pc",
  "steam-wallet": "pc",
};
