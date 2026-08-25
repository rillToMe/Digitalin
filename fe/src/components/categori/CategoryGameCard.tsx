import { useNavigate } from "react-router";
import { ArrowRight } from "lucide-react";
import type { BrandItem } from "../../api/index";

interface CategoryGameCardProps {
  brand: BrandItem;
  /** Urutan kurasi admin pada section "Populer". Tanpa ini, badge tidak muncul. */
  rank?: number;
}

/**
 * Kartu game pada grid kategori. Poster mempertahankan rasio 3:4, sedangkan
 * judul berada di bawah poster agar artwork tetap bersih.
 */
export default function CategoryGameCard({ brand, rank }: CategoryGameCardProps) {
  const navigate = useNavigate();

  const goToDetail = () => navigate(`/game/${brand.slug}`);

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      goToDetail();
    }
  };

  return (
    <div
      role="link"
      tabIndex={0}
      aria-label={`Top up ${brand.brand}`}
      onClick={goToDetail}
      onKeyDown={handleKeyDown}
      className="group cursor-pointer focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
    >
      <div
        className="hud-game-card relative aspect-[3/4] overflow-hidden transition-transform duration-150"
        style={{
          backgroundColor: "var(--color-surface)",
          border: "1px solid var(--color-border-light)",
          boxShadow: "inset 0 1px 0 rgba(255,255,255,0.06)",
        }}
      >
        {brand.imageUrl ? (
          <img
            src={brand.imageUrl}
            alt={brand.brand}
            className="h-full w-full object-cover transition-transform duration-150"
            loading="lazy"
            decoding="async"
          />
        ) : (
          <div
            className="flex h-full w-full items-center justify-center"
            style={{ backgroundColor: "var(--color-surface-raised)" }}
          >
            <span className="label-mono" style={{ color: "var(--color-brand-secondary)" }}>Artwork unavailable</span>
          </div>
        )}

        {/* Nomor urutan kurasi admin pada section Populer. */}
        {rank !== undefined && (
          <div
            className="label-mono absolute left-2 top-2 border px-1.5 py-0.5"
            style={{
              backgroundColor: "rgba(8, 11, 15, 0.82)",
              color: "var(--color-on-primary)",
            }}
          >
            {String(rank).padStart(2, "0")}
          </div>
        )}

        {/* CTA — fade-in di sudut saat hover / fokus keyboard. */}
        <div
          className="absolute right-2 top-2 flex items-center gap-1 border px-2 py-1 opacity-0 transition-opacity duration-150 group-hover:opacity-100 group-focus-within:opacity-100 motion-reduce:transition-none"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "var(--color-on-primary)",
          }}
        >
          <span className="label-mono">Top Up</span>
          <ArrowRight size={12} aria-hidden="true" />
        </div>
      </div>

      <p
        className="mt-2 line-clamp-2 px-0.5 text-center"
        style={{
          color: "var(--color-brand-primary)",
          fontWeight: 600,
          fontSize: "0.85rem",
          lineHeight: 1.3,
        }}
      >
        {brand.brand}
      </p>
    </div>
  );
}
