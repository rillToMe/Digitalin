import { useState } from "react";
import { ChevronDown, Gamepad2 } from "lucide-react";
import type { BrandItem } from "../../api/index";
import CategoryGameCard from "./CategoryGameCard";

const INITIAL_VISIBLE = 12;
const SKELETON_COUNT = 12;
/** Batas index stagger agar baris bawah tidak menunggu terlalu lama */
const MAX_STAGGER_INDEX = 14;

interface CategoryGameGridProps {
  /** Brands yang sudah terfilter kategori */
  brands: BrandItem[];
  isLoading: boolean;
}

function SkeletonCard() {
  return (
    <div
      className="overflow-hidden rounded-2xl"
      style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
    >
      <div className="aspect-square w-full animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
      <div className="p-3">
        <div className="mb-2 h-4 w-3/4 animate-pulse rounded" style={{ backgroundColor: "var(--color-skeleton)" }} />
        <div className="h-3 w-1/2 animate-pulse rounded" style={{ backgroundColor: "var(--color-skeleton)" }} />
      </div>
    </div>
  );
}

/**
 * Grid game per kategori.
 * Interaksi (murni CSS): staggered fade-in-up saat mount, expand/collapse
 * "Tampilkan lebih banyak". Parent memberi key={activeCategory} agar
 * state expand & animasi ter-reset setiap ganti kategori.
 */
export default function CategoryGameGrid({ brands, isLoading }: CategoryGameGridProps) {
  const [expanded, setExpanded] = useState(false);

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6">
        {Array.from({ length: SKELETON_COUNT }).map((_, i) => (
          <SkeletonCard key={i} />
        ))}
      </div>
    );
  }

  if (brands.length === 0) {
    return (
      <div className="py-16 text-center">
        <Gamepad2 size={40} style={{ color: "var(--color-brand-secondary)", margin: "0 auto 12px" }} />
        <p style={{ color: "var(--color-brand-secondary)" }}>Tidak ada game ditemukan</p>
      </div>
    );
  }

  const visibleBrands = expanded ? brands : brands.slice(0, INITIAL_VISIBLE);
  const canExpand = brands.length > INITIAL_VISIBLE;

  return (
    <>
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6">
        {visibleBrands.map((brand, index) => (
          <div
            key={brand.slug}
            className="animate-category-in"
            style={{ animationDelay: `${Math.min(index, MAX_STAGGER_INDEX) * 40}ms` }}
          >
            <CategoryGameCard brand={brand} />
          </div>
        ))}
      </div>

      {canExpand && (
        <div className="mt-6 text-center">
          <button
            onClick={() => setExpanded((v) => !v)}
            className="inline-flex items-center gap-1.5 rounded-xl px-5 py-2.5 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
            style={{
              color: "var(--color-brand-tertiary)",
              backgroundColor: "transparent",
              border: "1px solid var(--color-brand-tertiary)",
              fontWeight: 600,
              fontSize: "0.85rem",
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = "var(--color-hover-info)";
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = "transparent";
            }}
          >
            {expanded ? "Tampilkan lebih sedikit" : "Tampilkan lebih banyak"}
            <ChevronDown
              size={15}
              className={`transition-transform duration-200 ${expanded ? "rotate-180" : ""}`}
            />
          </button>
        </div>
      )}
    </>
  );
}
