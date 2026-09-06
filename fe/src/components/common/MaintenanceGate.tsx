import { useQuery } from "@tanstack/react-query";
import { Navigate, Outlet } from "react-router";
import api from "../../services/api";
import PageLoader from "./PageLoader";
import { useAuthStore } from "../../store/authStore";

/* Probe maintenance ala MaintenancePage: request publik ringan ke katalog.
   200 = maintenance OFF; 503 + body maintenance = ON (payload ikut serta).
   Error lain (jaringan mati, 500) dibiarkan gagal → gate fail-open, konsisten
   dengan desain lama: halaman tetap tampil dan layer reaktif (interceptor
   503 di api.ts) yang menangkap maintenance sungguhan. */
async function probeMaintenance(): Promise<{ active: boolean }> {
  try {
    await api.get("/games/brands", {
      params: { limit: 1 },
      skipMaintenanceRedirect: true,
    });
    return { active: false };
  } catch (err) {
    const status = (err as { response?: { status?: number } }).response?.status;
    const data = (err as { response?: { data?: { maintenance?: boolean } } })
      .response?.data;

    if (status === 503 && data?.maintenance === true) {
      return { active: true };
    }

    throw err;
  }
}

/**
 * Guard global maintenance: membungkus SEMUA route customer sebagai layout
 * route tanpa path (lihat App.tsx). Route /maintenance dan /admin/* berada
 * di luar guard — admin panel selalu bisa diakses (paritas dengan
 * MaintenanceMiddleware BE yang mendaftarkan /api/v1/admin sebagai whitelist),
 * dan halaman maintenance tidak pernah me-redirect ke dirinya sendiri.
 *
 * Dua lapis deteksi:
 * 1. Probe di sini (mount + tiap 30 detik) — menutup celah route yang tidak
 *    memanggil API sama sekali (login/register) dan deep link/refresh.
 * 2. Interceptor 503 di api.ts (sudah ada) — reaktif saat maintenance
 *    dinyalakan di tengah sesi.
 */
export default function MaintenanceGate() {
  const hasHydrated = useAuthStore((s) => s.hasHydrated);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: ["maintenance-gate"],
    queryFn: probeMaintenance,
    retry: 1,
    staleTime: 15_000,
    refetchInterval: 30_000,
  });

  // Status belum diketahui (auth rehydrate atau probe pertama): jangan
  // merender konten customer dulu — speks: tanpa flash konten terlindungi.
  if (!hasHydrated || isLoading) {
    return <PageLoader />;
  }

  if (data?.active) {
    // Cache bilang aktif tapi sedang verifikasi ulang: tunggu hasil sebelum
    // me-redirect. Tanpa ini, maintenance yang baru dimatikan membuat user
    // mental-bounce antara "/" (cache aktif) dan /maintenance (probe terbaru
    // bilang nonaktif) — cache 15 detik masih dipakai saat mount ulang gate.
    if (isFetching) {
      return <PageLoader />;
    }
    return <Navigate to="/maintenance" replace />;
  }

  // data.active === false, atau probe gagal (fail-open, lihat header
  // probeMaintenance) → routing normal.
  return <Outlet />;
}
