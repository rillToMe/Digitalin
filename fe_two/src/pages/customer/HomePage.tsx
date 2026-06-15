import { useQuery } from "@tanstack/react-query";
import { AlertCircle, Gamepad2 } from "lucide-react";
import { getGameBrands } from "@/services/public";
import { GameCard } from "@/components/customer/GameCard";
import { cn } from "@/lib/utils";

/**
 * Skeleton placeholder for a single game card during loading.
 */
function GameCardSkeleton() {
  return (
    <div
      className={cn(
        "flex flex-col overflow-hidden rounded-lg",
        "bg-surface/60 backdrop-blur-md border border-surface/40 shadow-sm",
      )}
    >
      <div className="aspect-square w-full animate-pulse bg-neutral" />
      <div className="px-3 py-3">
        <div className="mx-auto h-4 w-3/4 animate-pulse rounded bg-neutral" />
      </div>
    </div>
  );
}

/**
 * HomePage — Displays hero section + game catalog grid.
 * Fetches brands via GET /games/brands using React Query.
 */
export function HomePage() {
  const {
    data: brands,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ["game-brands"],
    queryFn: getGameBrands,
    staleTime: 10 * 60 * 1000, // 10 minutes — brands rarely change
  });

  return (
    <div className="space-y-10">
      {/* ── Hero Section ── */}
      <section className="glass rounded-lg p-8 text-center">
        <h1 className="text-h1 text-primary">Selamat Datang di Digitalin</h1>
        <p className="mt-2 text-secondary">
          Platform top-up game terpercaya. Pilih game favoritmu dan isi saldo secara instan.
        </p>
      </section>

      {/* ── Game Catalog ── */}
      <section>
        <div className="mb-6 flex items-center gap-2">
          <Gamepad2 className="h-5 w-5 text-tertiary" />
          <h2 className="text-lg font-semibold text-primary">Pilih Game</h2>
        </div>

        {/* Loading State — Skeleton Grid */}
        {isLoading && (
          <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-4 lg:grid-cols-5">
            {Array.from({ length: 10 }).map((_, i) => (
              <GameCardSkeleton key={i} />
            ))}
          </div>
        )}

        {/* Error State */}
        {isError && (
          <div className="glass flex flex-col items-center gap-3 rounded-lg p-10 text-center">
            <AlertCircle className="h-8 w-8 text-red-400" />
            <p className="text-sm font-medium text-primary">
              Gagal memuat daftar game.
            </p>
            <p className="text-xs text-secondary">
              {error instanceof Error
                ? error.message
                : "Terjadi kesalahan yang tidak diketahui."}
            </p>
          </div>
        )}

        {/* Empty State */}
        {!isLoading && !isError && brands && brands.length === 0 && (
          <div className="glass flex flex-col items-center gap-3 rounded-lg p-10 text-center">
            <Gamepad2 className="h-8 w-8 text-secondary/40" />
            <p className="text-sm font-medium text-primary">
              Belum ada game tersedia.
            </p>
            <p className="text-xs text-secondary">
              Silakan cek kembali nanti.
            </p>
          </div>
        )}

        {/* Success State — Game Grid */}
        {!isLoading && !isError && brands && brands.length > 0 && (
          <div className="grid grid-cols-2 gap-4 sm:gap-6 md:grid-cols-4 lg:grid-cols-5">
            {brands.map((brand) => (
              <GameCard key={brand.id} brand={brand} />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
