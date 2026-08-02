import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { GameCategoryItem } from "../api/index";
import { categoryService } from "../services/api";
import { ALL_CATEGORY_SLUG } from "../data/gameCategories";

interface UseGameCategoriesReturn {
  /** Kategori berisi ≥1 brand aktif, diawali "Semua", diakhiri "Lainnya" bila perlu */
  categories: GameCategoryItem[];
  activeCategory: string;
  setActiveCategory: (slug: string) => void;
  /** Nama tampilan kategori aktif, untuk judul section */
  activeCategoryName: string;
  isLoading: boolean;
}

/**
 * Daftar tab kategori + kategori aktif. Nama, urutan, dan brandCount seluruhnya
 * dari GET /games/categories (termasuk bucket virtual "all" & "lainnya") — brand
 * per section diambil terpisah & dipaginasi, jadi hook ini tidak pernah memegang
 * seluruh katalog untuk menghitung sendiri.
 */
export function useGameCategories(): UseGameCategoriesReturn {
  const [activeCategory, setActiveCategory] = useState(ALL_CATEGORY_SLUG);

  const { data: categories = [], isLoading } = useQuery({
    queryKey: ["gameCategories"],
    queryFn: categoryService.getAll,
    staleTime: 1000 * 60 * 5,
  });

  // Kategori aktif bisa hilang setelah admin menghapus/mengosongkannya.
  // Katalog kosong (categories []) bukan alasan reset — tab-nya juga tidak ada.
  useEffect(() => {
    if (
      categories.length > 0 &&
      !categories.some((category) => category.slug === activeCategory)
    ) {
      setActiveCategory(ALL_CATEGORY_SLUG);
    }
  }, [categories, activeCategory]);

  return {
    categories,
    activeCategory,
    setActiveCategory,
    activeCategoryName:
      categories.find((category) => category.slug === activeCategory)?.name ??
      activeCategory,
    isLoading,
  };
}
