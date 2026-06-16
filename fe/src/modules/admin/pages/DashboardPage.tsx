import { useState } from "react";
import { TrendingUp, ShoppingCart, CheckCircle, Clock, XCircle, DollarSign } from "lucide-react";
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from "recharts";
import { useQuery } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { ChartRange } from "../../../api/index";

function formatRp(n: number) {
  if (n >= 1000000) return "Rp " + (n / 1000000).toFixed(1) + "jt";
  return "Rp " + n.toLocaleString("id-ID");
}

const RANGE_OPTIONS: { value: ChartRange; label: string }[] = [
  { value: "today", label: "Hari Ini" },
  { value: "yesterday", label: "Kemarin" },
  { value: "7days", label: "7 Hari" },
  { value: "30days", label: "30 Hari" },
];

function StatCard({ icon, label, value, color }: { icon: React.ReactNode; label: string; value: string; color: string }) {
  return (
    <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
      <div className="flex items-center justify-between mb-4">
        <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>{label}</span>
        <div className="w-9 h-9 rounded-xl flex items-center justify-center" style={{ backgroundColor: `${color}18` }}>
          <span style={{ color }}>{icon}</span>
        </div>
      </div>
      <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.4rem" }}>{value}</p>
    </div>
  );
}

export default function DashboardPage() {
  const [chartRange, setChartRange] = useState<ChartRange>("7days");

  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ["dashboard-stats"],
    queryFn: adminService.getDashboardStats,
  });

  const { data: chart = [], isLoading: chartLoading } = useQuery({
    queryKey: ["dashboard-chart", chartRange],
    queryFn: () => adminService.getChart({ range: chartRange }),
  });

  const loading = statsLoading || chartLoading;

  return (
    <AdminLayout title="Dashboard" breadcrumb={["Admin", "Dashboard"]}>
      {loading ? (
        <div className="grid grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="rounded-2xl h-28 animate-pulse" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          ))}
        </div>
      ) : stats ? (
        <>
          <div className="grid grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-4 mb-8">
            <StatCard icon={<DollarSign size={18} />} label="Revenue Hari Ini" value={formatRp(stats.totalRevenueToday)} color="#2C5EF5" />
            <StatCard icon={<TrendingUp size={18} />} label="Profit Hari Ini" value={formatRp(stats.totalProfitToday)} color="#22c55e" />
            <StatCard icon={<ShoppingCart size={18} />} label="Total Order" value={stats.totalOrdersToday.toString()} color="#8b5cf6" />
            <StatCard icon={<CheckCircle size={18} />} label="Order Sukses" value={stats.successOrdersToday.toString()} color="#22c55e" />
            <StatCard icon={<Clock size={18} />} label="Order Pending" value={stats.pendingOrdersToday.toString()} color="#f59e0b" />
            <StatCard icon={<XCircle size={18} />} label="Order Gagal" value={stats.failedOrdersToday.toString()} color="#ef4444" />
          </div>

          {/* Chart */}
          <div className="rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "1rem" }}>Grafik Pendapatan & Profit</h3>
              <div className="flex gap-2">
                {RANGE_OPTIONS.map((opt) => (
                  <button
                    key={opt.value}
                    onClick={() => setChartRange(opt.value)}
                    className="px-3 py-1.5 rounded-lg transition-all"
                    style={{
                      backgroundColor: chartRange === opt.value ? "var(--color-tertiary,#2C5EF5)" : "transparent",
                      color: chartRange === opt.value ? "#fff" : "var(--color-secondary,#4A5568)",
                      border: chartRange === opt.value ? "none" : "1px solid rgba(0,0,0,0.1)",
                      fontSize: "0.8rem",
                      fontWeight: chartRange === opt.value ? 600 : 400,
                    }}
                  >
                    {opt.label}
                  </button>
                ))}
              </div>
            </div>
            <ResponsiveContainer width="100%" height={280}>
              <AreaChart data={chart} margin={{ top: 5, right: 10, left: 0, bottom: 0 }}>
                <defs>
                  <linearGradient id="pendapatan" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#2C5EF5" stopOpacity={0.2} />
                    <stop offset="95%" stopColor="#2C5EF5" stopOpacity={0} />
                  </linearGradient>
                  <linearGradient id="profit" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#22c55e" stopOpacity={0.2} />
                    <stop offset="95%" stopColor="#22c55e" stopOpacity={0} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="rgba(0,0,0,0.05)" />
                <XAxis dataKey="name" tick={{ fontSize: 12, fill: "#4A5568" }} />
                <YAxis tick={{ fontSize: 11, fill: "#4A5568" }} tickFormatter={(v) => `${(v / 1000000).toFixed(1)}jt`} />
                <Tooltip
                  formatter={(value: number, name: string) => [formatRp(value), name === "pendapatan" ? "Pendapatan" : "Profit"]}
                  contentStyle={{ borderRadius: 12, border: "1px solid rgba(0,0,0,0.1)", fontSize: 13 }}
                />
                <Legend />
                <Area type="monotone" dataKey="pendapatan" stroke="#2C5EF5" fill="url(#pendapatan)" strokeWidth={2} name="Pendapatan" />
                <Area type="monotone" dataKey="profit" stroke="#22c55e" fill="url(#profit)" strokeWidth={2} name="Profit" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </>
      ) : null}
    </AdminLayout>
  );
}
