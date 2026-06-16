import { useState } from "react";
import { Package, ChevronLeft, ChevronRight, Clock } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { orderService } from "../../../services/api";
import type { OrderStatus } from "../../../api/index";

const STATUS_CONFIG: Record<OrderStatus, { label: string; color: string; bg: string }> = {
  PENDING:    { label: "Menunggu",   color: "#b45309", bg: "#fef3c7" },
  PAID:       { label: "Dibayar",    color: "#1d4ed8", bg: "#dbeafe" },
  PROCESSING: { label: "Diproses",   color: "#6d28d9", bg: "#ede9fe" },
  COMPLETED:  { label: "Selesai",    color: "#15803d", bg: "#dcfce7" },
  NEEDS_ACTION: { label: "Perlu Aksi", color: "#c2410c", bg: "#ffedd5" },
  FAILED:     { label: "Gagal",      color: "#b91c1c", bg: "#fee2e2" },
  EXPIRED:    { label: "Expired",    color: "#6b7280", bg: "#f3f4f6" },
  REFUNDED:   { label: "Refunded",   color: "#0369a1", bg: "#e0f2fe" },
};

function StatusBadge({ status }: { status: OrderStatus }) {
  const cfg = STATUS_CONFIG[status] ?? { label: status, color: "#6b7280", bg: "#f3f4f6" };
  return (
    <span className="px-2.5 py-1 rounded-full" style={{ backgroundColor: cfg.bg, color: cfg.color, fontSize: "0.75rem", fontWeight: 600 }}>
      {cfg.label}
    </span>
  );
}

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

export default function HistoryPage() {
  const [page, setPage] = useState(1);

  const { data, isLoading: loading } = useQuery({
    queryKey: ["orders", page],
    queryFn: () => orderService.getHistory(page),
  });

  const orders = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="max-w-3xl mx-auto px-4 py-10">
      <h1 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem", marginBottom: 24 }}>Riwayat Order</h1>

      {loading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="rounded-2xl h-28 animate-pulse" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          ))}
        </div>
      ) : orders.length === 0 ? (
        <div className="text-center py-20">
          <Package size={40} style={{ color: "var(--color-secondary,#4A5568)", margin: "0 auto 12px" }} />
          <p style={{ color: "var(--color-secondary,#4A5568)" }}>Belum ada riwayat order</p>
        </div>
      ) : (
        <div className="space-y-3">
          {orders.map((order) => (
            <div key={order.id} className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
              <div className="flex items-start justify-between mb-3">
                <div>
                  <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem", fontFamily: "monospace" }}>{order.orderNo}</p>
                  <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginTop: 2 }}>{order.productName}</p>
                </div>
                <StatusBadge status={order.status} />
              </div>

              <div className="flex items-center justify-between pt-3" style={{ borderTop: "1px solid rgba(0,0,0,0.06)" }}>
                <div className="flex items-center gap-1.5" style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.8rem" }}>
                  <Clock size={13} />
                  {new Date(order.orderDate).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" })}
                </div>
                <span style={{ color: "var(--color-tertiary,#2C5EF5)", fontWeight: 700, fontSize: "0.95rem" }}>
                  {formatRp(order.finalPrice)}
                </span>
              </div>

              {order.sn && (
                <div className="mt-3 px-3 py-2 rounded-lg" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                  <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem" }}>Serial Number</p>
                  <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem", fontFamily: "monospace" }}>{order.sn}</p>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 mt-8">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-primary,#0F1419)" }}>
            <ChevronLeft size={16} />
          </button>
          <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-primary,#0F1419)" }}>
            <ChevronRight size={16} />
          </button>
        </div>
      )}
    </div>
  );
}
