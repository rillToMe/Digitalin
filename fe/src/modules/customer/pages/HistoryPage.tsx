import { useState } from "react";
import { useNavigate } from "react-router";
import { Package, ChevronLeft, ChevronRight, Clock, ChevronDown, ChevronUp, CreditCard, Gamepad2 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { orderService } from "../../../services/api";
import type { OrderStatus } from "../../../api/index";
import { ImageWithFallback } from "../../../utils/ImageWithFallback";
import BackButton from "../../../components/common/BackButton";

const STATUS_CONFIG: Record<OrderStatus, { label: string; color: string; bg: string }> = {
  PENDING:      { label: "Menunggu",   color: "var(--color-warning)",   bg: "var(--color-warning-bg)" },
  PAID:         { label: "Dibayar",    color: "var(--color-info)",      bg: "var(--color-info-bg)" },
  PROCESSING:   { label: "Diproses",   color: "var(--color-info)",      bg: "var(--color-info-bg)" },
  COMPLETED:    { label: "Selesai",    color: "var(--color-success)",   bg: "var(--color-success-bg)" },
  NEEDS_ACTION: { label: "Perlu Aksi", color: "var(--color-warning)",   bg: "var(--color-warning-bg)" },
  FAILED:       { label: "Gagal",      color: "var(--color-danger)",    bg: "var(--color-danger-bg)" },
  EXPIRED:      { label: "Expired",    color: "var(--color-brand-secondary)", bg: "var(--color-neutral)" },
  REFUNDED:     { label: "Refunded",   color: "var(--color-info)",      bg: "var(--color-info-bg)" },
};

function StatusBadge({ status }: { status: OrderStatus }) {
  const cfg = STATUS_CONFIG[status] ?? { label: status, color: "var(--color-brand-secondary)", bg: "var(--color-neutral)" };
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
  const [expandedOrders, setExpandedOrders] = useState<Record<number, boolean>>({});
  const navigate = useNavigate();

  const { data, isLoading: loading } = useQuery({
    queryKey: ["orders", page],
    queryFn: () => orderService.getHistory(page),
  });

  const orders = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;

  const toggleExpand = (id: number) => {
    setExpandedOrders((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  return (
    <div className="max-w-3xl mx-auto px-4 py-10">
      <BackButton />
      <h1 style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem", marginBottom: 24 }}>Riwayat Order</h1>

      {loading ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="rounded-2xl h-28 animate-pulse" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          ))}
        </div>
      ) : orders.length === 0 ? (
        <div className="text-center py-20">
          <Package size={40} style={{ color: "var(--color-brand-secondary,#4A5568)", margin: "0 auto 12px" }} />
          <p style={{ color: "var(--color-brand-secondary,#4A5568)" }}>Belum ada riwayat order</p>
        </div>
      ) : (
        <div className="space-y-3">
          {orders.map((order) => {
            const isExpanded = !!expandedOrders[order.id];
            return (
              <div
                key={order.id}
                className="rounded-2xl p-5 cursor-pointer transition-all hover:shadow-sm"
                style={{
                  backgroundColor: "var(--color-surface,#fff)",
                  border: "1px solid var(--color-border-default-light, rgba(0,0,0,0.07))",
                }}
                onClick={() => toggleExpand(order.id)}
              >
                {/* Header Summary */}
                <div className="flex items-start gap-4">
                  {/* Thumbnail Image */}
                  <div
                    className="w-12 h-12 rounded-xl overflow-hidden flex-shrink-0 flex items-center justify-center"
                    style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }}
                  >
                    {order.imageUrl ? (
                      <ImageWithFallback
                        src={order.imageUrl}
                        alt={order.productName}
                        className="w-full h-full object-cover"
                      />
                    ) : (
                      <Gamepad2 size={20} style={{ color: "var(--color-brand-tertiary)" }} />
                    )}
                  </div>

                  {/* Summary Information */}
                  <div className="flex-1 min-w-0">
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem", fontFamily: "monospace" }}>
                      {order.orderNo}
                    </p>
                    <h3
                      style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginTop: 2 }}
                      className="truncate"
                    >
                      {order.productName}
                    </h3>
                    <div className="flex items-center gap-1.5 mt-1.5" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.78rem" }}>
                      <Clock size={12} />
                      <span>
                        {new Date(order.orderDate).toLocaleDateString("id-ID", {
                          day: "numeric",
                          month: "short",
                          year: "numeric",
                          hour: "2-digit",
                          minute: "2-digit",
                        })}
                      </span>
                    </div>
                  </div>

                  {/* Badges and Price */}
                  <div className="text-right flex-shrink-0 flex flex-col items-end gap-1.5">
                    <StatusBadge status={order.status} />
                    <span style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 700, fontSize: "0.95rem" }}>
                      {formatRp(order.finalPrice)}
                    </span>
                    <div style={{ color: "var(--color-brand-secondary)", padding: 2 }}>
                      {isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                    </div>
                  </div>
                </div>

                {/* Collapsible Details */}
                {isExpanded && (
                  <div
                    className="mt-4 pt-4 space-y-3"
                    style={{ borderTop: "1px solid var(--color-border-default-lighter, rgba(0,0,0,0.05))" }}
                    onClick={(e) => e.stopPropagation()}
                  >
                    <h4 style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.85rem", marginBottom: 8 }}>
                      Detail Transaksi
                    </h4>

                    <div className="space-y-2">
                      <div className="flex justify-between items-center" style={{ fontSize: "0.82rem" }}>
                        <span style={{ color: "var(--color-brand-secondary)" }}>Player ID</span>
                        <span style={{ color: "var(--color-brand-primary)", fontWeight: 500, fontFamily: "monospace" }}>
                          {order.playerId}
                        </span>
                      </div>

                      <div className="flex justify-between items-center" style={{ fontSize: "0.82rem" }}>
                        <span style={{ color: "var(--color-brand-secondary)" }}>Harga Item</span>
                        <span style={{ color: "var(--color-brand-primary)" }}>{formatRp(order.price)}</span>
                      </div>

                      {order.discountAmount > 0 && (
                        <div className="flex justify-between items-center" style={{ fontSize: "0.82rem" }}>
                          <span style={{ color: "var(--color-success)" }}>Potongan Voucher</span>
                          <span style={{ color: "var(--color-success)", fontWeight: 600 }}>
                            -{formatRp(order.discountAmount)}
                          </span>
                        </div>
                      )}

                      <div
                        className="flex justify-between items-center pt-2"
                        style={{ borderTop: "1px dashed var(--color-border-default)", fontSize: "0.85rem" }}
                      >
                        <span style={{ color: "var(--color-brand-primary)", fontWeight: 600 }}>Total Pembayaran</span>
                        <span style={{ color: "var(--color-brand-tertiary)", fontWeight: 700 }}>
                          {formatRp(order.finalPrice)}
                        </span>
                      </div>
                    </div>

                    {order.sn && (
                      <div className="mt-3 p-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                        <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem" }}>Serial Number / Kode Voucher</p>
                        <p style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem", fontFamily: "monospace", marginTop: 2 }}>
                          {order.sn}
                        </p>
                      </div>
                    )}

                    {/* Aksi bayar untuk order PENDING: instruksi bayar Core API
                        (QR/VA/deeplink) dirender di halaman status. */}
                    {order.status === "PENDING" && order.paymentType && (
                      <div className="pt-2">
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            navigate(`/order/status/${order.orderNo}`);
                          }}
                          className="w-full py-2.5 rounded-xl flex items-center justify-center gap-2 transition-all hover:opacity-90"
                          style={{
                            backgroundColor: "var(--color-brand-tertiary, #2C5EF5)",
                            color: "var(--color-on-primary, #fff)",
                            fontWeight: 600,
                            fontSize: "0.85rem",
                          }}
                        >
                          <CreditCard size={15} />
                          Lihat Pembayaran
                        </button>
                      </div>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-3 mt-8">
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-primary,#0F1419)" }}>
            <ChevronLeft size={16} />
          </button>
          <span style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>{page} / {totalPages}</span>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-primary,#0F1419)" }}>
            <ChevronRight size={16} />
          </button>
        </div>
      )}
    </div>
  );
}

