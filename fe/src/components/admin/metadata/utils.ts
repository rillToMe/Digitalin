import type { BrandItem } from "../../../api/index";

export function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

export function formatNumberInput(value: string) {
  if (!value) return "";

  return Number(value).toLocaleString("id-ID");
}

// Slug brand dari katalog, atau derive dari nama
export function getSlugForBrand(brandName: string, brands: BrandItem[]): string {
  const match = brands.find((b) => b.brand.toLowerCase() === brandName.toLowerCase());
  if (match) return match.slug;
  // Fallback: derive slug from brand name (lowercase, spaces to hyphens)
  return brandName.toLowerCase().replace(/\s+/g, "-");
}

export type ModalTab = "info" | "products";
