import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { BrandItem, GameCategoryItem } from "../api/index";
import { categoryService } from "../services/api";
import {
  ALL_CATEGORY_SLUG,
  BRAND_CATEGORY_MAP,
  CATEGORY_DEFINITIONS,
  CATEGORY_NAMES,
  UNCATEGORIZED_CATEGORY_SLUG,
} from "../data/gameCategories";

interface UseGameCategoriesReturn {
  /** Kategori yang punya ≥1 brand, diawali "Semua", diakhiri "Lainnya" bila perlu */
  categories: GameCategoryItem[];
  activeCategory: string;
  setActiveCategory: (slug: string) => void;
  /** Nama tampilan kategori aktif, untuk judul section */
  activeCategoryName: string;
  /** Brands yang sudah terfilter sesuai kategori aktif */
  filteredBrands: BrandItem[];
}

/**
 * Resolve kategori sebuah brand.
 * Prioritas: categorySlug dari BE → mapping transisi (brand yang belum
 * dikategorikan admin) → "lainnya".
 */
function resolveCategorySlug(brand: BrandItem): string {
  return (
    brand.categorySlug ??
    BRAND_CATEGORY_MAP[brand.slug] ??
    UNCATEGORIZED_CATEGORY_SLUG
  );
}

/**
 * Logic kategori storefront: daftar kategori kanonis dari GET /games/categories
 * (fallback ke definisi statis saat belum termuat), jumlah brand dihitung
 * client-side dari array brands agar konsisten dengan grid, plus filter brands.
 */
export function useGameCategories(brands: BrandItem[]): UseGameCategoriesReturn {
  const [activeCategory, setActiveCategory] = useState(ALL_CATEGORY_SLUG);

  const { data: apiCategories } = useQuery({
    queryKey: ["gameCategories"],
    queryFn: categoryService.getAll,
  });

  const categories = useMemo<GameCategoryItem[]>(() => {
    const counts = new Map<string, number>();
    for (const brand of brands) {
      const slug = resolveCategorySlug(brand);
      counts.set(slug, (counts.get(slug) ?? 0) + 1);
    }

    const list: GameCategoryItem[] = [
      { slug: ALL_CATEGORY_SLUG, name: CATEGORY_NAMES[ALL_CATEGORY_SLUG], brandCount: brands.length },
    ];

    // Definisi kanonis dari endpoint; fallback ke definisi statis selama belum termuat
    const definitions = apiCategories ?? CATEGORY_DEFINITIONS;

    for (const def of definitions) {
      const count = counts.get(def.slug) ?? 0;
      if (count > 0) list.push({ slug: def.slug, name: def.name, brandCount: count });
    }

    // Slug yang ada di brands tapi belum dikenal definisi (mis. kategori baru dari admin
    // yang belum termuat dari endpoint)
    for (const [slug, count] of counts) {
      if (slug !== UNCATEGORIZED_CATEGORY_SLUG && !list.some((c) => c.slug === slug)) {
        list.push({ slug, name: CATEGORY_NAMES[slug] ?? slug, brandCount: count });
      }
    }

    const uncategorized = counts.get(UNCATEGORIZED_CATEGORY_SLUG) ?? 0;
    if (uncategorized > 0) {
      list.push({
        slug: UNCATEGORIZED_CATEGORY_SLUG,
        name: CATEGORY_NAMES[UNCATEGORIZED_CATEGORY_SLUG],
        brandCount: uncategorized,
      });
    }

    return list;
  }, [brands, apiCategories]);

  // Reset ke "Semua" bila kategori aktif menghilang (mis. data brands berubah)
  useEffect(() => {
    if (!categories.some((c) => c.slug === activeCategory)) {
      setActiveCategory(ALL_CATEGORY_SLUG);
    }
  }, [categories, activeCategory]);

  const filteredBrands = useMemo(() => {
    if (activeCategory === ALL_CATEGORY_SLUG) return brands;
    return brands.filter((b) => resolveCategorySlug(b) === activeCategory);
  }, [brands, activeCategory]);

  return {
    categories,
    activeCategory,
    setActiveCategory,
    activeCategoryName:
      categories.find((c) => c.slug === activeCategory)?.name ?? activeCategory,
    filteredBrands,
  };
}
