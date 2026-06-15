import { useParams, Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowLeft, Gamepad2 } from "lucide-react";
import { getGameDetail } from "@/services/public";
import { TopUpForm } from "@/components/customer/TopUpForm";
import { cn } from "@/lib/utils";
import type { BrandDetailResponse } from "@/types";

// ─── Skeleton Loaders ────────────────────────────────────────────────────────

function DetailPageSkeleton() {
  return (
    <div className="space-y-6">
      {/* Banner skeleton */}
      <div className="glass h-40 animate-pulse rounded-lg sm:h-48" />

      <div className="grid gap-8 lg:grid-cols-[1fr_380px]">
        {/* Left column skeleton */}
        <div className="space-y-6">
          <div className="glass rounded-lg p-6">
            <div className="mb-4 h-5 w-40 animate-pulse rounded bg-neutral" />
            <div className="space-y-4">
              <div className="h-10 animate-pulse rounded-md bg-neutral" />
              <div className="h-10 animate-pulse rounded-md bg-neutral" />
            </div>
          </div>
          <div className="glass rounded-lg p-6">
            <div className="mb-4 h-5 w-40 animate-pulse rounded bg-neutral" />
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="h-16 animate-pulse rounded-md bg-neutral" />
              ))}
            </div>
          </div>
        </div>
        {/* Right column skeleton */}
        <div className="h-64 animate-pulse rounded-lg bg-neutral lg:sticky lg:top-24" />
      </div>
    </div>
  );
}

// ─── Page Component ──────────────────────────────────────────────────────────

export function GameDetailPage() {
  const { brandSlug } = useParams<{ brandSlug: string }>();

  const {
    data: game,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ["game-detail", brandSlug],
    queryFn: () => getGameDetail(brandSlug!),
    enabled: !!brandSlug,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });

  // ── Loading ──
  if (isLoading || !brandSlug) {
    return <DetailPageSkeleton />;
  }

  // ── Error ──
  if (isError || !game) {
    return (
      <div className="glass flex flex-col items-center gap-4 rounded-lg p-10 text-center">
        <Gamepad2 className="h-10 w-10 text-red-400" />
        <div>
          <p className="text-sm font-medium text-primary">Gagal memuat detail game.</p>
          <p className="mt-1 text-xs text-secondary">
            {error instanceof Error ? error.message : "Game tidak ditemukan."}
          </p>
        </div>
        <Link
          to="/games"
          className="mt-2 flex items-center gap-1.5 text-sm font-medium text-tertiary transition-colors hover:text-tertiary/80"
        >
          <ArrowLeft className="h-4 w-4" />
          Kembali ke katalog
        </Link>
      </div>
    );
  }

  // ── Success ──
  return (
    <div className="space-y-6">
      {/* ── Game Banner ── */}
      <div className={cn(
        "glass relative flex items-end overflow-hidden rounded-lg",
        "h-36 sm:h-44 lg:h-48",
      )}>
        {/* Background image or solid */}
        {game.imageUrl ? (
          <img
            src={game.imageUrl}
            alt={game.brand}
            className="absolute inset-0 h-full w-full object-cover opacity-30"
          />
        ) : (
          <div className="absolute inset-0 bg-secondary/10" />
        )}

        {/* Content overlay */}
        <div className="relative z-10 flex items-center gap-3 p-6">
          {game.imageUrl && (
            <img
              src={game.imageUrl}
              alt={game.brand}
              className="h-16 w-16 rounded-lg object-cover shadow-md sm:h-20 sm:w-20"
            />
          )}
          <div>
            <Link
              to="/games"
              className="mb-1 flex items-center gap-1 text-xs text-secondary transition-colors hover:text-primary"
            >
              <ArrowLeft className="h-3 w-3" />
              Semua Game
            </Link>
            <h1 className="text-xl font-semibold text-primary sm:text-2xl">
              {game.brand}
            </h1>
            <p className="text-xs text-secondary">
              {game.products.length} produk tersedia
            </p>
          </div>
        </div>
      </div>

      {/* ── Two-Column Layout ── */}
      <div className="grid gap-8 lg:grid-cols-[1fr_380px]">
        {/* Left Column: Form steps */}
        <div>
          <TopUpForm game={game} />
        </div>

        {/* Right Column: Sticky summary (desktop only — mobile sees it inline) */}
        <aside className="hidden lg:block">
          <div className="sticky top-24">
            <DesktopSummary game={game} />
          </div>
        </aside>
      </div>
    </div>
  );
}

// ─── Desktop-Only Sticky Summary ─────────────────────────────────────────────

function DesktopSummary({ game }: { game: BrandDetailResponse }) {
  return (
    <div className="glass rounded-lg p-5">
      <h3 className="mb-3 text-sm font-semibold text-primary">Info Game</h3>
      <div className="space-y-2 text-sm">
        <div className="flex justify-between">
          <span className="text-secondary">Game</span>
          <span className="font-medium text-primary">{game.brand}</span>
        </div>
        <div className="flex justify-between">
          <span className="text-secondary">Tipe Input</span>
          <span className="text-label text-secondary">
            {game.inputType === "USER_ID_AND_ZONE_ID" ? "User ID + Zone" : "User ID only"}
          </span>
        </div>
        <div className="flex justify-between">
          <span className="text-secondary">Jumlah Produk</span>
          <span className="font-medium text-primary">{game.products.length}</span>
        </div>
      </div>
    </div>
  );
}
