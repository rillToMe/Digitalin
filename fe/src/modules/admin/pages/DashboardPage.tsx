import { useState } from "react";
import { TrendingUp, ShoppingCart, CheckCircle, Clock, XCircle, DollarSign, AlertTriangle, RefreshCw } from "lucide-react";
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from "recharts";
import { useQuery } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { ChartRange } from "../../../api/index";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

const RANGE_OPTIONS: { value: ChartRange; label: string }[] = [
  { value: "today", label: "Hari Ini" },
  { value: "yesterday", label: "Kemarin" },
  { value: "7days", label: "7 Hari" },
  { value: "30days", label: "30 Hari" },
];

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

export default function DashboardPage() {
  const [chartRange, setChartRange] = useState<ChartRange>("7days");

  const { data: stats, isLoading: statsLoading, isError: statsError, error: statsErr, refetch: refetchStats } = useQuery({
    queryKey: ["dashboard-stats"],
    queryFn: adminService.getDashboardStats,
    retry: 1,
  });

  const { data: chart = [], isLoading: chartLoading, isError: chartError, error: chartErr, refetch: refetchChart } = useQuery({
    queryKey: ["dashboard-chart", chartRange],
    queryFn: () => adminService.getChart({ range: chartRange }),
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
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
            {Array.from({ length: 2 }).map((_, i) => (
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
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
            <StatCard hero icon={<DollarSign size={18} />} label="Revenue Hari Ini" value={formatRp(stats.totalRevenueToday)} tone="accent" />
            <StatCard hero icon={<TrendingUp size={18} />} label="Profit Hari Ini" value={formatRp(stats.totalProfitToday)} tone="success" />
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
              </div>
            </div>
            {chart.length === 0 ? (
              <div className="admin-empty">
                <p className="admin-empty-title">Belum ada transaksi</p>
                <p className="admin-empty-text">Data grafik akan muncul setelah order pertama masuk.</p>
              </div>
            ) : (
              <ResponsiveContainer width="100%" height={280}>
                <AreaChart data={chart} margin={{ top: 5, right: 10, left: 0, bottom: 0 }}>
                  <defs>
                    <linearGradient id="pendapatan" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--color-brand-tertiary)" stopOpacity={0.2} />
                      <stop offset="95%" stopColor="var(--color-brand-tertiary)" stopOpacity={0} />
                    </linearGradient>
                    <linearGradient id="profit" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="var(--color-success)" stopOpacity={0.2} />
                      <stop offset="95%" stopColor="var(--color-success)" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border-lighter)" />
                  <XAxis dataKey="name" tickLine={false} axisLine={false} tick={{ fontSize: 12, fill: "var(--color-brand-secondary)" }} />
                  <YAxis
                    tickLine={false}
                    axisLine={false}
                    tick={{ fontSize: 11, fill: "var(--color-brand-secondary)" }}
                    tickFormatter={(v) => "Rp " + (v / 1000).toLocaleString("id-ID") + "rb"}
                  />
                  <Tooltip
                    formatter={(value: number, name: string) => [formatRp(value), name === "pendapatan" ? "Pendapatan" : "Profit"]}
                    contentStyle={{
                      borderRadius: "var(--admin-radius-md)",
                      border: "1px solid var(--color-border-default)",
                      boxShadow: "var(--shadow-raised)",
                      fontSize: 13,
                    }}
                  />
                  <Legend iconType="plainline" wrapperStyle={{ fontSize: 13, paddingTop: 8 }} />
                  <Area type="monotone" dataKey="pendapatan" stroke="var(--color-brand-tertiary)" fill="url(#pendapatan)" strokeWidth={2} name="Pendapatan" />
                  <Area type="monotone" dataKey="profit" stroke="var(--color-success)" fill="url(#profit)" strokeWidth={2} name="Profit" />
                </AreaChart>
              </ResponsiveContainer>
            )}
          </div>
        </>
      ) : null}
    </AdminLayout>
  );
}
