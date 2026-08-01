import { useRef } from "react";
import type { GameCategoryItem } from "../../api/index";
import { CATEGORY_ICONS } from "../../data/gameCategories";

interface CategoryTabsProps {
  categories: GameCategoryItem[];
  activeCategory: string;
  onChange: (slug: string) => void;
}

/**
 * Pill tabs kategori storefront.
 * Interaksi: transisi warna CSS antar tab, badge jumlah brand,
 * keyboard navigation (ArrowLeft/ArrowRight/Home/End) dengan roving tabindex.
 */
export default function CategoryTabs({ categories, activeCategory, onChange }: CategoryTabsProps) {
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

  return (
    <div
      role="tablist"
      aria-label="Kategori game"
      className="flex gap-2 overflow-x-auto pb-2"
      style={{ scrollbarWidth: "none" }}
    >
      {categories.map((cat, index) => {
        const active = cat.slug === activeCategory;
        const Icon = CATEGORY_ICONS[cat.slug];

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
            className="flex items-center gap-2 px-4 py-2 rounded-full whitespace-nowrap transition-all duration-200 active:scale-95 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
            style={{
              backgroundColor: active ? "var(--color-brand-tertiary)" : "var(--color-surface)",
              color: active ? "var(--color-on-primary)" : "var(--color-brand-primary)",
              border: active ? "1px solid var(--color-brand-tertiary)" : "1px solid var(--color-border-default)",
              fontWeight: active ? 600 : 400,
              fontSize: "0.85rem",
            }}
          >
            {Icon && <Icon size={15} />}
            {cat.name}
            <span
              className="rounded-full px-1.5 py-0.5 leading-none"
              style={{
                backgroundColor: active ? "var(--color-on-primary)" : "var(--color-neutral)",
                color: active ? "var(--color-brand-tertiary)" : "var(--color-brand-secondary)",
                fontSize: "0.7rem",
                fontWeight: 600,
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
