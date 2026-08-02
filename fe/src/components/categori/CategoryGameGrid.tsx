import { ChevronDown, Gamepad2 } from "lucide-react";
import type { BrandItem } from "../../api/index";
import CategoryGameCard from "./CategoryGameCard";

/** Kartu di luar jendela ini tidak menunda animasinya lagi — batch kedua
 *  (offset 12+) harus muncul segera, bukan 12 × 40ms setelah diklik. */
const STAGGER_WINDOW = 12;

interface CategoryGameGridProps {
  brands: BrandItem[];
  isLoading: boolean;
  /** Jumlah skeleton = ukuran batch pertama section, supaya tidak ada layout shift */
  skeletonCount?: number;
  hasMore?: boolean;
  isLoadingMore?: boolean;
  onLoadMore?: () => void;
}

/**
 * Struktur harus sama persis dengan CategoryGameCard (padding p-3, gambar
 * rounded-xl, blok judul 2 baris) supaya tidak ada layout shift saat data
 * datang. 2.34rem = judul 2 baris di kartu asli (0.9rem × line-height 1.3 × 2).
 */
function SkeletonCard() {
  return (
    <div
      className="rounded-2xl p-3"
      style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
    >
      <div
        className="aspect-square w-full animate-pulse rounded-xl"
        style={{ backgroundColor: "var(--color-skeleton)" }}
      />
      <div className="flex flex-col gap-1.5 px-1 pt-3" style={{ minHeight: "2.34rem" }}>
        <div className="h-3 w-4/5 animate-pulse rounded" style={{ backgroundColor: "var(--color-skeleton)" }} />
        <div className="h-3 w-1/2 animate-pulse rounded" style={{ backgroundColor: "var(--color-skeleton)" }} />
      </div>
    </div>
  );
}

const GRID_CLASS =
  "grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6";

/**
 * Grid game satu section. Murni presentational: batch berikutnya diambil parent
 * (useInfiniteQuery) lewat onLoadMore — brand yang belum diklik tidak pernah
 * diunduh, jadi tidak ada lagi collapse "Tampilkan lebih sedikit".
 * Interaksi murni CSS: staggered fade-in-up saat kartu mount.
 */
export default function CategoryGameGrid({
  brands,
  isLoading,
  skeletonCount = 12,
  hasMore = false,
  isLoadingMore = false,
  onLoadMore,
}: CategoryGameGridProps) {
  if (isLoading) {
    return (
      <div className={GRID_CLASS}>
        {Array.from({ length: skeletonCount }).map((_, i) => (
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

  return (
    <>
      <div className={GRID_CLASS}>
        {brands.map((brand, index) => (
          <div
            key={brand.slug}
            className="animate-category-in"
            style={{ animationDelay: `${(index % STAGGER_WINDOW) * 40}ms` }}
          >
            <CategoryGameCard brand={brand} />
          </div>
        ))}

        {/* Batch berikutnya menempati slot grid, bukan menggeser tombol. */}
        {isLoadingMore &&
          Array.from({ length: 6 }).map((_, i) => <SkeletonCard key={`more-${i}`} />)}
      </div>

      {hasMore && (
        <div className="mt-6 text-center">
          <button
            onClick={onLoadMore}
            disabled={isLoadingMore}
            className="inline-flex items-center gap-1.5 rounded-xl px-5 py-2.5 transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary) disabled:opacity-60"
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
            {isLoadingMore ? "Memuat…" : "Tampilkan lebih banyak"}
            <ChevronDown size={15} aria-hidden="true" />
          </button>
        </div>
      )}
    </>
  );
}
