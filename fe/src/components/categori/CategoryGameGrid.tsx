import { useRef } from "react";
import { ChevronDown } from "lucide-react";
import type { BrandItem } from "../../api/index";
import CategoryGameCard from "./CategoryGameCard";

interface CategoryGameGridProps {
  brands: BrandItem[];
  isLoading: boolean;
  /** Jumlah skeleton = ukuran batch pertama section, supaya tidak ada layout shift */
  skeletonCount?: number;
  hasMore?: boolean;
  isLoadingMore?: boolean;
  onLoadMore?: () => void;
  /** "rail" = satu baris horizontal yang bisa di-swipe/drag. */
  variant?: "grid" | "rail";
  /** Nomor urutan hanya untuk kurasi "Populer". */
  ranked?: boolean;
}

/** Bentuk skeleton mengikuti poster 3:4 dan judul di bawahnya. */
function SkeletonCard() {
  return (
    <div>
      <div
        className="hud-game-card aspect-[3/4] animate-pulse overflow-hidden"
        style={{
          backgroundColor: "var(--color-skeleton)",
          border: "1px solid var(--color-border-light)",
        }}
      />
      <div className="mt-2 px-0.5">
        <div
          className="h-3 w-4/5 animate-pulse rounded"
          style={{ backgroundColor: "var(--color-skeleton)" }}
        />
      </div>
    </div>
  );
}

const GRID_CLASS = "grid grid-cols-3 gap-3 sm:flex sm:flex-wrap sm:gap-4";

/* Rail: lebar kartu dikunci (rasio 3:4 tetap dijaga kartunya sendiri) dan
   snap-start supaya swipe berhenti rapi di tepi poster, bukan di tengah. */
const RAIL_CLASS =
  "game-rail flex snap-x snap-mandatory flex-nowrap gap-4 overflow-x-auto px-1 pb-3";
const RAIL_ITEM_CLASS = "w-[132px] shrink-0 snap-start sm:w-[150px]";
const GRID_ITEM_CLASS = "min-w-0 sm:w-[150px] sm:shrink-0";

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
  variant = "grid",
  ranked = false,
}: CategoryGameGridProps) {
  const rail = variant === "rail";
  const wrapClass = rail ? RAIL_CLASS : GRID_CLASS;
  const itemClass = rail ? RAIL_ITEM_CLASS : GRID_ITEM_CLASS;
  const railRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef({ active: false, moved: false, startX: 0, scrollLeft: 0 });

  const railHandlers = rail
    ? {
        ref: railRef,
        onPointerDown: (event: React.PointerEvent<HTMLDivElement>) => {
          if (event.pointerType !== "mouse" || event.button !== 0) return;
          dragRef.current = {
            active: true,
            moved: false,
            startX: event.clientX,
            scrollLeft: event.currentTarget.scrollLeft,
          };
        },
        onPointerMove: (event: React.PointerEvent<HTMLDivElement>) => {
          if (!dragRef.current.active) return;
          const distance = event.clientX - dragRef.current.startX;
          if (Math.abs(distance) > 4 && !dragRef.current.moved) {
            dragRef.current.moved = true;
            event.currentTarget.setPointerCapture(event.pointerId);
          }
          event.currentTarget.scrollLeft = dragRef.current.scrollLeft - distance;
        },
        onPointerUp: (event: React.PointerEvent<HTMLDivElement>) => {
          if (!dragRef.current.active) return;
          dragRef.current.active = false;
          if (event.currentTarget.hasPointerCapture(event.pointerId)) {
            event.currentTarget.releasePointerCapture(event.pointerId);
          }
        },
        onPointerCancel: () => {
          dragRef.current.active = false;
        },
        onClickCapture: (event: React.MouseEvent<HTMLDivElement>) => {
          if (!dragRef.current.moved) return;
          event.preventDefault();
          event.stopPropagation();
          dragRef.current.moved = false;
        },
      }
    : {};

  if (isLoading) {
    return (
      <div className={wrapClass} {...railHandlers}>
        {Array.from({ length: skeletonCount }).map((_, i) => (
          <div key={i} className={itemClass}>
            <SkeletonCard />
          </div>
        ))}
      </div>
    );
  }

  if (brands.length === 0) {
    return (
      <div className="py-16 text-center">
        <p className="label-mono" style={{ color: "var(--color-brand-secondary)" }}>Tidak ada game ditemukan</p>
      </div>
    );
  }

  return (
    <>
      <div className={wrapClass} {...railHandlers}>
        {brands.map((brand, index) => (
          <div key={brand.slug} className={itemClass}>
            <CategoryGameCard brand={brand} rank={ranked ? index + 1 : undefined} />
          </div>
        ))}

        {/* Batch berikutnya menempati slot grid, bukan menggeser tombol. */}
        {isLoadingMore &&
          Array.from({ length: 6 }).map((_, i) => (
            <div key={`more-${i}`} className={itemClass}>
              <SkeletonCard />
            </div>
          ))}
      </div>

      {hasMore && (
        <div className="mt-6 text-center">
          <button
            onClick={onLoadMore}
            disabled={isLoadingMore}
            className="inline-flex items-center gap-1.5 border px-5 py-2.5 transition-colors duration-150 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary) disabled:opacity-60"
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
