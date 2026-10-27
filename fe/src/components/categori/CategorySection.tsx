import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import type { GameCategoryItem } from "../../api/index";
import { useGameCategories } from "../../hooks/useGameCategories";
import { useRecentGames } from "../../hooks/useRecentGames";
import {
  ALL_CATEGORY_SLUG,
  CATEGORY_NAMES,
  MORE_FETCH_LIMIT,
  POPULAR_CATEGORY_SLUG,
  RECENT_CATEGORY_SLUG,
  UNCATEGORIZED_CATEGORY_SLUG,
  categoryIcon,
  initialFetchLimit,
  nextBrandOffset,
} from "../../data/gameCategories";
import { brandService } from "../../services/api";
import CategoryTabs from "./CategoryTabs";
import CategoryGameGrid from "./CategoryGameGrid";

const CATALOG_STALE_TIME = 1000 * 60 * 5;

/** Judul + jumlah + grid — satu tampilan untuk section kategori & "Populer". */
function SectionShell({
  slug,
  name,
  total,
  isFirst,
  children,
}: {
  slug: string;
  name: string;
  total?: number;
  isFirst: boolean;
  children: React.ReactNode;
}) {
  const Icon = categoryIcon(slug);
  const titleId = `category-${slug}-title`;

  return (
    <section
      id={`category-${slug}`}
      aria-labelledby={titleId}
      className={`scroll-mt-24 ${isFirst ? "mt-3" : "mt-12"}`}
    >
      <div className="hud-section-label mb-4">
        <h2
          id={titleId}
          className="flex items-center gap-2.5"
          style={{
            color: "var(--color-brand-primary)",
            fontWeight: 700,
            fontSize: "1.35rem",
          }}
        >
          {/* Ikon dalam chip netral, bukan kobalt: aksen disimpan untuk aksi
              (design.md), sementara chip-nya yang memberi kesan panel HUD. */}
          <span
            className="flex h-7 w-7 shrink-0 items-center justify-center border"
            style={{
              backgroundColor: "var(--color-hover-surface)",
              color: "var(--color-brand-secondary)",
            }}
          >
            <Icon size={15} aria-hidden="true" />
          </span>
          {slug === ALL_CATEGORY_SLUG ? "Semua Game" : name}
        </h2>

        {total !== undefined && (
          <span
            className="label-mono whitespace-nowrap border px-2 py-1"
            style={{
              color: "var(--color-brand-secondary)",
              border: "1px solid var(--color-border-light)",
            }}
          >
            {total} game
          </span>
        )}
      </div>

      {children}
    </section>
  );
}

/**
 * Satu section kategori dengan fetch-nya sendiri: batch pertama kecil
 * (12, atau 7 untuk "Lainnya"), batch berikutnya 24 saat tombol diklik.
 * Total datang dari brandCount /games/categories, jadi tombol "lebih banyak"
 * bisa disembunyikan tanpa request tambahan.
 */
function PagedCategorySection({
  category,
  isFirst,
}: {
  category: GameCategoryItem;
  isFirst: boolean;
}) {
  const firstBatch = initialFetchLimit(category.slug);

  const { data, isLoading, fetchNextPage, hasNextPage, isFetchingNextPage } =
    useInfiniteQuery({
      queryKey: ["brands", category.slug],
      queryFn: ({ pageParam }) =>
        brandService.getAll({
          category: category.slug,
          offset: pageParam,
          limit: pageParam === 0 ? firstBatch : MORE_FETCH_LIMIT,
        }),
      initialPageParam: 0,
      getNextPageParam: (lastPage, allPages) =>
        nextBrandOffset(
          allPages.reduce((sum, page) => sum + page.length, 0),
          category.brandCount,
          lastPage.length,
        ),
      staleTime: CATALOG_STALE_TIME,
    });

  return (
    <SectionShell
      slug={category.slug}
      name={category.name}
      total={category.brandCount}
      isFirst={isFirst}
    >
      <CategoryGameGrid
        brands={data?.pages.flat() ?? []}
        isLoading={isLoading}
        variant={
          category.slug === UNCATEGORIZED_CATEGORY_SLUG ? "grid" : "rail"
        }
        skeletonCount={firstBatch}
        hasMore={hasNextPage}
        isLoadingMore={isFetchingNextPage}
        onLoadMore={() => void fetchNextPage()}
      />
    </SectionShell>
  );
}

/**
 * Katalog game HomePage: tabs kategori → satu section per kategori
 * (judul + jumlah + grid), masing-masing mengambil batch-nya sendiri.
 * id="game-list" wajib dipertahankan (anchor "/#game-list" dari Footer).
 */
export default function CategorySection() {
  const { categories, activeCategory, setActiveCategory, activeCategoryName, isLoading } =
    useGameCategories();

  /* Kurasi admin dari BE. Urutan respons = urutan tampil, tanpa load-more. */
  const { data: popularBrands = [] } = useQuery({
    queryKey: ["popularBrands"],
    queryFn: brandService.getPopular,
    staleTime: CATALOG_STALE_TIME,
  });

  /* "Terakhir Dibeli": riwayat pembelian (login) / cookie guest. Kosong =
     disembunyikan — sama seperti "Populer", bukan judul tanpa kartu. */
  const { recentBrands } = useRecentGames();

  /* "Semua"  → "Terakhir Dibeli" + "Populer" lalu satu section per kategori;
                daftar rata ratusan game tidak bisa di-scan sementara katalognya
                sudah terkategori.
     kategori → satu section saja. */
  const sections =
    activeCategory === ALL_CATEGORY_SLUG
      ? categories.filter((category) => category.slug !== ALL_CATEGORY_SLUG)
      : categories.filter((category) => category.slug === activeCategory);

  /* Section virtual hanya di mode "Semua": di dalam tab kategori, section ini
     akan menampilkan game dari kategori lain. */
  const showRecent =
    activeCategory === ALL_CATEGORY_SLUG && recentBrands.length > 0;
  const showPopular =
    activeCategory === ALL_CATEGORY_SLUG && popularBrands.length > 0;

  return (
    <section id="game-list" aria-label="Katalog game" className="scroll-mt-24">
      <CategoryTabs
        categories={categories}
        activeCategory={activeCategory}
        onChange={setActiveCategory}
        isLoading={isLoading}
      />

      {/* Daftar kategori belum ada → skeleton satu section, bukan halaman kosong. */}
      {isLoading ? (
        <SectionShell slug={activeCategory} name={activeCategoryName} isFirst>
          <CategoryGameGrid brands={[]} isLoading variant="rail" />
        </SectionShell>
      ) : (
        <>
          {showRecent && (
            <SectionShell
              slug={RECENT_CATEGORY_SLUG}
              name={CATEGORY_NAMES[RECENT_CATEGORY_SLUG]}
              total={recentBrands.length}
              isFirst
            >
              <CategoryGameGrid
                brands={recentBrands}
                isLoading={false}
                variant="rail"
              />
            </SectionShell>
          )}

          {showPopular && (
            <SectionShell
              slug={POPULAR_CATEGORY_SLUG}
              name={CATEGORY_NAMES[POPULAR_CATEGORY_SLUG]}
              total={popularBrands.length}
              isFirst={!showRecent}
            >
              <CategoryGameGrid
                brands={popularBrands}
                isLoading={false}
                variant="rail"
                ranked
              />
            </SectionShell>
          )}

          {sections.map((category, index) => (
            /* key ikut activeCategory: ganti tab me-remount section supaya
               animasi grid replay (query-nya sendiri tetap dari cache). */
            <PagedCategorySection
              key={`${activeCategory}-${category.slug}`}
              category={category}
              isFirst={!showPopular && !showRecent && index === 0}
            />
          ))}
        </>
      )}
    </section>
  );
}
