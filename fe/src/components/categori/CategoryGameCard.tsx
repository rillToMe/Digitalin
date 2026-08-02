import { useNavigate } from "react-router";
import { ArrowRight, Gamepad2 } from "lucide-react";
import type { BrandItem } from "../../api/index";

interface CategoryGameCardProps {
  brand: BrandItem;
}

/**
 * Kartu game pada grid kategori.
 * Interaksi (murni CSS): lift saat hover, zoom gambar, dan CTA bar
 * "Top Up" yang slide-up dari bawah gambar (juga muncul saat focus keyboard).
 */
export default function CategoryGameCard({ brand }: CategoryGameCardProps) {
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
      className="group relative cursor-pointer overflow-hidden rounded-2xl p-3 transition-all duration-200 hover:-translate-y-1 hover:shadow-[0_8px_24px_var(--color-border-default)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
      style={{
        backgroundColor: "var(--color-surface)",
        border: "1px solid var(--color-border-light)",
      }}
    >
      <div className="relative aspect-square w-full overflow-hidden rounded-xl">
        {brand.imageUrl ? (
          <img
            src={brand.imageUrl}
            alt={brand.brand}
            className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
            loading="lazy"
            decoding="async"
          />
        ) : (
          <div
            className="flex h-full w-full items-center justify-center"
            style={{ backgroundColor: "var(--color-neutral)" }}
          >
            <Gamepad2 size={32} style={{ color: "var(--color-brand-secondary)" }} />
          </div>
        )}

        {/* CTA bar — slide-up saat hover / focus */}
        <div
          className="absolute inset-x-0 bottom-0 flex translate-y-full items-center justify-between px-3 py-2 transition-transform duration-200 group-hover:translate-y-0 group-focus-within:translate-y-0"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "var(--color-on-primary)",
          }}
        >
          <span style={{ fontSize: "0.78rem", fontWeight: 600 }}>Top Up</span>
          <ArrowRight size={14} />
        </div>
      </div>

      {/* Tinggi judul dikunci 2 baris (sesuai line-clamp-2) supaya kartu dengan
          nama panjang tidak jadi lebih tinggi dari tetangganya di grid. */}
      <div className="px-1 pt-3">
        <p
          className="line-clamp-2"
          style={{
            color: "var(--color-brand-primary)",
            fontWeight: 600,
            fontSize: "0.9rem",
            lineHeight: 1.3,
            minHeight: "2.6em",
          }}
        >
          {brand.brand}
        </p>
      </div>
    </div>
  );
}
