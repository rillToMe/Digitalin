import { useQuery } from "@tanstack/react-query";
import { brandService } from "../services/api";
import { useAuthStore } from "../store/authStore";
import { readRecentSlugs } from "../utils/recentGames";
import type { BrandItem } from "../api/index";

interface UseRecentGamesReturn {
  /** Brand "Terakhir Dibeli" terbaru dulu; kosong = section disembunyikan */
  recentBrands: BrandItem[];
  isLoading: boolean;
}

/**
 * Section "Terakhir Dibeli" homepage.
 *
 * SSOT pembelian: user login → riwayat DB lewat GET /games/brands/recent
 * (backend membaca klaim auth, cookie guest diabaikan). Guest → slug cookie
 * recentGames (hanya identifier katalog publik, divalidasi ulang backend).
 * Tanpa riwayat / tanpa cookie → [] → section tidak dirender.
 */
export function useRecentGames(): UseRecentGamesReturn {
  const token = useAuthStore((state) => state.token);
  const hasHydrated = useAuthStore((state) => state.hasHydrated);
  const isAuthenticated = !!token;

  // Cookie dibaca per render (murah, read-only) supaya invalidasi query
  // setelah pembelian sekaligus menangkap slug yang baru ditulis.
  const guestSlugs = isAuthenticated ? [] : readRecentSlugs();
  const guestSlugsKey = guestSlugs.join(",");

  const { data, isLoading } = useQuery({
    queryKey: ["recentGames", isAuthenticated ? "auth" : "guest", guestSlugsKey],
    queryFn: () =>
      brandService.getRecent(
        isAuthenticated ? undefined : { slugs: guestSlugsKey },
      ),
    // Hydrasi zustand (persist localStorage) harus selesai dulu supaya mode
    // auth/guest tidak salah di render pertama; guest tanpa cookie tidak fetch.
    enabled: hasHydrated && (isAuthenticated || guestSlugs.length > 0),
    staleTime: 1000 * 60 * 5,
  });

  return {
    recentBrands: data ?? [],
    isLoading,
  };
}
