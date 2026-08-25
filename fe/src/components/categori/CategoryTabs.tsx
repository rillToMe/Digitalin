import { useRef } from "react";
import type { GameCategoryItem } from "../../api/index";
import { categoryIcon } from "../../data/gameCategories";

interface CategoryTabsProps {
  categories: GameCategoryItem[];
  activeCategory: string;
  onChange: (slug: string) => void;
  isLoading?: boolean;
}

/** Lebar pill dibedakan supaya tidak terlihat seperti deret blok identik */
const SKELETON_PILL_WIDTHS = [96, 128, 108, 120, 100];

/**
 * Pill tabs kategori storefront.
 * Interaksi: transisi warna CSS antar tab, badge jumlah brand,
 * keyboard navigation (ArrowLeft/ArrowRight/Home/End) dengan roving tabindex.
 */
export default function CategoryTabs({ categories, activeCategory, onChange, isLoading }: CategoryTabsProps) {
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);

  const handleKeyDown = (e: React.KeyboardEvent, index: number) => {
    let nextIndex: number | null = null;

    if (e.key === "ArrowRight") nextIndex = (index + 1) % categories.length;
    else if (e.key === "ArrowLeft") nextIndex = (index - 1 + categories.length) % categories.length;
    else if (e.key === "Home") nextIndex = 0;
    else if (e.key === "End") nextIndex = categories.length - 1;

    if (nextIndex === null) return;
    e.preventDefault();
    onChange(categories[nextIndex].slug);
    tabRefs.current[nextIndex]?.focus();
  };

  // Padding, font-size, dan border disamakan dengan pill asli supaya tinggi
  // baris tab tidak berubah saat kategori selesai dimuat.
  if (isLoading) {
    return (
      <div className="flex gap-2 overflow-x-auto pb-2" aria-hidden="true" style={{ scrollbarWidth: "none" }}>
        {SKELETON_PILL_WIDTHS.map((width, i) => (
          <div
            key={i}
            className="flex-shrink-0 animate-pulse px-4 py-2"
            style={{
              width,
              backgroundColor: "var(--color-skeleton)",
              border: "1px solid transparent",
              fontSize: "0.85rem",
            }}
          >
            &nbsp;
          </div>
        ))}
      </div>
    );
  }

  return (
    <div
      role="tablist"
      aria-label="Kategori game"
      className="flex gap-5 overflow-x-auto border-b pb-0"
      style={{ scrollbarWidth: "none" }}
    >
      {categories.map((cat, index) => {
        const active = cat.slug === activeCategory;
        const Icon = categoryIcon(cat.slug);

        return (
          <button
            key={cat.slug}
            ref={(el) => {
              tabRefs.current[index] = el;
            }}
            role="tab"
            aria-selected={active}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(cat.slug)}
            onKeyDown={(e) => handleKeyDown(e, index)}
            className="relative flex items-center gap-2 whitespace-nowrap border-b-2 border-transparent px-1 py-2.5 transition-colors duration-150 active:scale-95 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
            style={{
              backgroundColor: "transparent",
              color: active ? "var(--color-brand-primary)" : "var(--color-brand-secondary)",
              borderBottomColor: active ? "var(--color-brand-tertiary)" : "transparent",
              fontWeight: active ? 600 : 400,
              fontSize: "0.85rem",
            }}
          >
            <Icon size={15} aria-hidden="true" />
            {cat.name}
            <span
              className="label-mono border px-1.5 py-0.5 leading-none"
              style={{
                backgroundColor: "transparent",
                color: "var(--color-brand-secondary)",
              }}
            >
              {cat.brandCount}
            </span>
          </button>
        );
      })}
    </div>
  );
}
