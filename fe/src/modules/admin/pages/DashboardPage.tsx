import { Suspense, lazy, useState } from "react";
import { TrendingUp, ShoppingCart, CheckCircle, Clock, XCircle, DollarSign, AlertTriangle, RefreshCw } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { ChartRange } from "../../../api/index";

/* Chart hanya dimuat saat data tersedia, jadi stat card tampil sebelum
   library grafik ikut diunduh. */
const RevenueChart = lazy(() => import("../../../components/admin/RevenueChart"));

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

const RANGE_OPTIONS: { value: Exclude<ChartRange, "custom">; label: string }[] = [
  { value: "today", label: "Hari Ini" },
  { value: "yesterday", label: "Kemarin" },
  { value: "7days", label: "7 Hari" },
  { value: "30days", label: "30 Hari" },
];

function isoDate(date: Date) {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 10);
}

function StatCard({
  icon,
  label,
  value,
  tone,
  hero,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  tone: "accent" | "success" | "warning" | "danger" | "info";
  hero?: boolean;
}) {
  return (
    <div className={`admin-stat${hero ? " admin-stat--hero" : ""}`}>
      <div className="admin-stat-top">
        <span className="admin-stat-label">{label}</span>
        <span className={`admin-stat-icon admin-stat-icon--${tone}`}>{icon}</span>
      </div>
      <p className="admin-stat-value">{value}</p>
    </div>
  );
}

/**
 * Saldo supplier dipisah dari query stats: angkanya datang dari Digiflazz, jadi
 * kalau supplier lambat/down (BE balas 502) hanya kartu ini yang gagal — bukan
 * seluruh dashboard. staleTime 60s + tombol refresh menahan panggilan berulang
 * ke supplier setiap kali tab dashboard difokuskan kembali.
 */
function SupplierBalanceCard() {
  const { data, isLoading, isError, error, refetch, isFetching } = useQuery({
    queryKey: ["digiflazz-balance"],
    queryFn: adminService.getDigiflazzBalance,
    staleTime: 60_000,
    retry: 1,
  });

  const serverMessage = (error as { response?: { data?: { message?: string } } } | null)
    ?.response?.data?.message;

  return (
    <div className="admin-stat admin-stat--hero">
      <div className="admin-stat-top">
        <span className="admin-stat-label">Saldo Digiflazz</span>
        <button
          type="button"
          onClick={() => void refetch()}
          disabled={isFetching}
          className="admin-iconbtn"
          aria-label="Perbarui saldo Digiflazz"
        >
          <RefreshCw size={16} className={isFetching ? "animate-spin" : undefined} aria-hidden="true" />
        </button>
      </div>
      <p className="admin-stat-value">
        {isLoading ? "…" : isError || !data ? "Gagal dimuat" : formatRp(data.deposit)}
      </p>
      {isError && (
        <p className="admin-empty-meta" role="status">
          {serverMessage ?? "Saldo tidak bisa diambil dari supplier."}
        </p>
      )}
    </div>
  );
}

export default function DashboardPage() {
  const [chartRange, setChartRange] = useState<ChartRange>("7days");
  const [customStartDate, setCustomStartDate] = useState(() => isoDate(new Date(Date.now() - 29 * 86_400_000)));
  const [customEndDate, setCustomEndDate] = useState(() => isoDate(new Date()));
  const customDatesValid = customStartDate <= customEndDate;

  const { data: stats, isLoading: statsLoading, isError: statsError, error: statsErr, refetch: refetchStats } = useQuery({
    queryKey: ["dashboard-stats"],
    queryFn: adminService.getDashboardStats,
    retry: 1,
  });

  const { data: chart = [], isLoading: chartLoading, isError: chartError, error: chartErr, refetch: refetchChart } = useQuery({
    queryKey: ["dashboard-chart", chartRange, customStartDate, customEndDate],
    queryFn: () => adminService.getChart(
      chartRange === "custom"
        ? { range: chartRange, startDate: customStartDate, endDate: customEndDate }
        : { range: chartRange },
    ),
    enabled: chartRange !== "custom" || customDatesValid,
    retry: 1,
  });

  const loading = statsLoading || chartLoading;
  const hasError = statsError || chartError;

  return (
    <AdminLayout title="Dashboard" breadcrumb={["Admin", "Dashboard"]}>
      {hasError ? (
        <div className="admin-card">
          <div className="admin-empty">
            <AlertTriangle size={36} className="admin-empty-icon" aria-hidden="true" />
            <h3 className="admin-empty-title">Gagal memuat data dashboard</h3>
            <p className="admin-empty-text">
              Server mengembalikan error internal (500). Pastikan backend sudah berjalan dengan benar.
            </p>
            {(statsErr as { message?: string })?.message && (
              <p className="admin-empty-meta">stats: {(statsErr as { message?: string }).message}</p>
            )}
            {(chartErr as { message?: string })?.message && (
              <p className="admin-empty-meta">chart: {(chartErr as { message?: string }).message}</p>
            )}
            <button
              onClick={() => {
                refetchStats();
                refetchChart();
              }}
              className="admin-btn admin-btn--primary mt-4"
            >
              <RefreshCw size={15} aria-hidden="true" /> Coba Lagi
            </button>
          </div>
        </div>
      ) : loading ? (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="admin-skeleton h-32" />
            ))}
          </div>
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="admin-skeleton h-28" />
            ))}
          </div>
          <div className="admin-skeleton h-80" />
        </>
      ) : stats ? (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4 mb-4">
            <StatCard hero icon={<DollarSign size={18} />} label="Revenue Hari Ini" value={formatRp(stats.totalRevenueToday)} tone="accent" />
            <StatCard hero icon={<TrendingUp size={18} />} label="Profit Hari Ini" value={formatRp(stats.totalProfitToday)} tone="success" />
            <SupplierBalanceCard />
          </div>

          <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
            <StatCard icon={<ShoppingCart size={18} />} label="Total Order" value={stats.totalOrdersToday.toString()} tone="info" />
            <StatCard icon={<CheckCircle size={18} />} label="Order Sukses" value={stats.successOrdersToday.toString()} tone="success" />
            <StatCard icon={<Clock size={18} />} label="Order Pending" value={stats.pendingOrdersToday.toString()} tone="warning" />
            <StatCard icon={<XCircle size={18} />} label="Order Gagal" value={stats.failedOrdersToday.toString()} tone="danger" />
          </div>

          {/* Chart */}
          <div className="admin-card">
            <div className="admin-card-head">
              <h3 className="admin-card-title">Grafik Pendapatan &amp; Profit</h3>
              <div className="admin-segmented" role="group" aria-label="Rentang grafik">
                {RANGE_OPTIONS.map((opt) => (
                  <button
                    key={opt.value}
                    onClick={() => setChartRange(opt.value)}
                    className={`admin-segment${chartRange === opt.value ? " admin-segment--active" : ""}`}
                    aria-pressed={chartRange === opt.value}
                  >
                    {opt.label}
                  </button>
                ))}
                <button
                  type="button"
                  onClick={() => setChartRange("custom")}
                  className={`admin-segment${chartRange === "custom" ? " admin-segment--active" : ""}`}
                  aria-pressed={chartRange === "custom"}
                >
                  Pilih Tanggal
                </button>
              </div>
            </div>
            {chartRange === "custom" && (
              <div className="flex flex-wrap items-end gap-3 mb-4" role="group" aria-label="Rentang tanggal kustom">
                <label className="flex flex-col gap-1 text-sm font-medium text-[var(--color-brand-primary)]">
                  Dari
                  <input
                    type="date"
                    value={customStartDate}
                    max={customEndDate}
                    onChange={(event) => setCustomStartDate(event.target.value)}
                    className="admin-input h-10"
                  />
                </label>
                <label className="flex flex-col gap-1 text-sm font-medium text-[var(--color-brand-primary)]">
                  Sampai
                  <input
                    type="date"
                    value={customEndDate}
                    min={customStartDate}
                    max={isoDate(new Date())}
                    onChange={(event) => setCustomEndDate(event.target.value)}
                    className="admin-input h-10"
                  />
                </label>
                {!customDatesValid && <p className="admin-empty-meta mb-2" role="alert">Tanggal mulai tidak boleh setelah tanggal selesai.</p>}
              </div>
            )}
            {chart.length === 0 ? (
              <div className="admin-empty">
                <p className="admin-empty-title">Belum ada transaksi</p>
                <p className="admin-empty-text">Data grafik akan muncul setelah order pertama masuk.</p>
              </div>
            ) : (
              <Suspense fallback={<div className="admin-skeleton" style={{ height: 280 }} />}>
                <RevenueChart data={chart} />
              </Suspense>
            )}
          </div>
        </>
      ) : null}
    </AdminLayout>
  );
}
