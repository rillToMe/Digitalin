import type { BrandItem } from "../../api/index";
import { useGameCategories } from "../../hooks/useGameCategories";
import { ALL_CATEGORY_SLUG } from "../../data/gameCategories";
import CategoryTabs from "./CategoryTabs";
import CategoryGameGrid from "./CategoryGameGrid";

interface CategorySectionProps {
  brands: BrandItem[];
  isLoading: boolean;
}

/**
 * Section katalog per kategori di HomePage.
 * Komposisi: tabs kategori → judul + jumlah → grid game.
 * id="game-list" wajib dipertahankan (anchor dari CustomerLayout "/#game-list").
 */
export default function CategorySection({ brands, isLoading }: CategorySectionProps) {
  const {
    categories,
    activeCategory,
    setActiveCategory,
    activeCategoryName,
    filteredBrands,
  } = useGameCategories(brands);

  return (
    <section id="game-list" className="scroll-mt-24">
      <CategoryTabs
        categories={categories}
        activeCategory={activeCategory}
        onChange={setActiveCategory}
      />

      <div className="mb-4 mt-3 flex items-center justify-between">
        <h2
          style={{
            color: "var(--color-brand-primary)",
            fontWeight: 700,
            fontSize: "1.1rem",
          }}
        >
          {activeCategory === ALL_CATEGORY_SLUG ? "Semua Game" : activeCategoryName}
        </h2>
        {!isLoading && (
          <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
            {filteredBrands.length} game
          </span>
        )}
      </div>

      {/* key me-remount grid setiap ganti kategori: replay animasi + reset expand */}
      <CategoryGameGrid
        key={activeCategory}
        brands={filteredBrands}
        isLoading={isLoading}
      />
    </section>
  );
}
