import { useState, useEffect, useRef } from "react";
import { Search, Eye, RefreshCw, Undo2, X, Loader2, ChevronLeft, ChevronRight, AlertTriangle, Clock } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { OrderResponse, OrderStatus } from "../../../api/index";
import { ImageWithFallback } from "../../../utils/ImageWithFallback";
import { cellProps, type AdminColumn } from "../../../components/admin/adminTable";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("id-ID", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

const STATUS_STYLES: Record<OrderStatus, { bg: string; text: string; label: string }> = {
  PENDING:      { bg: "var(--color-warning-bg)",   text: "var(--color-warning)",   label: "Menunggu" },
  PAID:         { bg: "var(--color-info-bg)",      text: "var(--color-info)",      label: "Dibayar" },
  PROCESSING:   { bg: "var(--color-info-bg)",      text: "var(--color-info)",      label: "Diproses" },
  COMPLETED:    { bg: "var(--color-success-bg)",   text: "var(--color-success)",   label: "Selesai" },
  NEEDS_ACTION: { bg: "var(--color-warning-bg)",   text: "var(--color-warning)",   label: "Perlu Aksi" },
  FAILED:       { bg: "var(--color-danger-bg)",    text: "var(--color-danger)",    label: "Gagal" },
  EXPIRED:      { bg: "var(--color-neutral)",      text: "var(--color-brand-secondary)", label: "Expired" },
  REFUNDED:     { bg: "var(--color-info-bg)",      text: "var(--color-info)",      label: "Refunded" },
};

const PAGE_SIZE = 10;

/* Satu sumber untuk header kolom dan label kartu di mobile, supaya keduanya
   tidak bisa lepas sinkron. wide = disembunyikan di layar kecil; semuanya
   tetap ada di modal Detail Order, jadi tidak ada data yang hilang. */
const COLUMNS: AdminColumn[] = [
  { label: "Invoice" },
  { label: "Tanggal", wide: true },
  { label: "Player ID" },
  { label: "Produk" },
  { label: "Harga Asli", wide: true },
  { label: "Diskon", wide: true },
  { label: "Total Bayar" },
  { label: "Status" },
  { label: "SN / Token", wide: true },
  { label: "Aksi" },
];

const cell = cellProps(COLUMNS);

export default function OrdersPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  
  // Modals state
  const [detailTarget, setDetailTarget] = useState<OrderResponse | null>(null);
  const [retryTarget, setRetryTarget] = useState<OrderResponse | null>(null);
  const [refundTarget, setRefundTarget] = useState<OrderResponse | null>(null);

  // Refs for accessibility focusing
  const detailCloseRef = useRef<HTMLButtonElement>(null);
  const retryConfirmRef = useRef<HTMLButtonElement>(null);
  const refundConfirmRef = useRef<HTMLButtonElement>(null);

  // Fetch orders
  const { data, isLoading: loading } = useQuery({
    queryKey: ["admin-orders", page],
    queryFn: () => adminService.getOrders({ page, limit: PAGE_SIZE }),
  });

  const allOrders = data?.data ?? [];
  const totalCount = data?.totalItems ?? 0;
  const totalPages = data?.totalPages ?? 1;

  // Filter orders locally by search query if search text is typed
  const orders = allOrders.filter(o => 
    o.orderNo.toLowerCase().includes(search.toLowerCase()) || 
    o.playerId.toLowerCase().includes(search.toLowerCase()) ||
    o.productName.toLowerCase().includes(search.toLowerCase())
  );

  // Keyboard navigation: Escape to close modals
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setDetailTarget(null);
        setRetryTarget(null);
        setRefundTarget(null);
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, []);

  // Accessibility: Focus target element when modal opens
  useEffect(() => {
    if (detailTarget) detailCloseRef.current?.focus();
  }, [detailTarget]);

  useEffect(() => {
    if (retryTarget) retryConfirmRef.current?.focus();
  }, [retryTarget]);

  useEffect(() => {
    if (refundTarget) refundConfirmRef.current?.focus();
  }, [refundTarget]);

  // Mutations
  const retryMutation = useMutation({
    mutationFn: (orderNo: string) => adminService.retryOrder(orderNo),
    onSuccess: (res) => {
      toast.success(res.message || "Fulfillment berhasil dikirim ulang!");
      // Invalidate queries to refresh state
      queryClient.invalidateQueries({ queryKey: ["admin-orders"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] });
      
      // If detail modal is open for this order, update it with fresh data
      if (detailTarget && detailTarget.orderNo === retryTarget?.orderNo) {
        setDetailTarget(prev => prev ? { ...prev, status: "PROCESSING" as OrderStatus } : null);
      }
      
      setRetryTarget(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const errMsg = err?.response?.data?.message ?? "Gagal memproses ulang order.";
      toast.error(errMsg);
      setRetryTarget(null);
    },
  });

  const refundMutation = useMutation({
    mutationFn: (orderNo: string) => adminService.refundOrder(orderNo),
    onSuccess: (res) => {
      toast.success(res.message || "Order berhasil ditandai sebagai REFUNDED.");
      queryClient.invalidateQueries({ queryKey: ["admin-orders"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard-stats"] });
      
      if (detailTarget && detailTarget.orderNo === refundTarget?.orderNo) {
        setDetailTarget(prev => prev ? { ...prev, status: "REFUNDED" as OrderStatus } : null);
      }
      
      setRefundTarget(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const errMsg = err?.response?.data?.message ?? "Gagal memproses refund order.";
      toast.error(errMsg);
      setRefundTarget(null);
    },
  });

  const handleRetrySubmit = () => {
    if (retryTarget) {
      retryMutation.mutate(retryTarget.orderNo);
    }
  };

  const handleRefundSubmit = () => {
    if (refundTarget) {
      refundMutation.mutate(refundTarget.orderNo);
    }
  };

  return (
    <AdminLayout title="Order Management" breadcrumb={["Admin", "Orders"]}>
      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row gap-3 mb-5">
        <div className="flex-1 relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: "var(--color-brand-secondary)" }} />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Cari order no, player ID, atau nama produk..."
            className="w-full pl-9 pr-4 py-2.5 rounded-xl outline-none"
            style={{ border: "1.5px solid var(--color-border)", fontSize: "0.9rem", backgroundColor: "var(--color-surface)", color: "var(--color-brand-primary)" }}
          />
        </div>
      </div>

      {/* Table */}
      <div className="rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
        <div className="overflow-x-auto">
          <table className="admin-table" role="table">
            <thead role="rowgroup">
              <tr role="row" style={{ borderBottom: "1px solid var(--color-border-light)", backgroundColor: "var(--color-neutral)" }}>
                {COLUMNS.map((c) => (
                  <th
                    key={c.label}
                    role="columnheader"
                    scope="col"
                    className={`px-4 py-3 text-left${c.wide ? " admin-col-wide" : ""}`}
                    style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}
                  >
                    {c.label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody role="rowgroup">
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} role="row">
                    {COLUMNS.map((c) => (
                      <td key={c.label} role="cell" className={`px-4 py-3${c.wide ? " admin-col-wide" : ""}`}>
                        <div className="h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : orders.map((o) => {
                const styleCfg = STATUS_STYLES[o.status] ?? { bg: "var(--color-neutral)", text: "var(--color-brand-secondary)", label: o.status };
                return (
                  <tr key={o.id} role="row" style={{ borderBottom: "1px solid var(--color-border-lighter)" }}>
                    <td {...cell("Invoice")}>
                      <span style={{ fontFamily: "monospace", fontSize: "0.85rem", color: "var(--color-brand-primary)", fontWeight: 600 }}>{o.orderNo}</span>
                    </td>
                    <td {...cell("Tanggal")} style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem" }}>
                      {formatDate(o.orderDate)}
                    </td>
                    <td {...cell("Player ID")}>
                      <span style={{ fontFamily: "monospace", fontSize: "0.85rem", color: "var(--color-brand-primary)" }}>{o.playerId}</span>
                    </td>
                    <td {...cell("Produk")} style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500 }}>
                      {o.productName}
                    </td>
                    <td {...cell("Harga Asli")} style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
                      {formatRp(o.price)}
                    </td>
                    <td {...cell("Diskon")} style={{ color: o.discountAmount > 0 ? "var(--color-success)" : "var(--color-brand-secondary)", fontSize: "0.85rem", fontWeight: o.discountAmount > 0 ? 600 : 400 }}>
                      {o.discountAmount > 0 ? `-${formatRp(o.discountAmount)}` : "-"}
                    </td>
                    <td {...cell("Total Bayar")} style={{ color: "var(--color-brand-tertiary)", fontSize: "0.9rem", fontWeight: 700 }}>
                      {formatRp(o.finalPrice)}
                    </td>
                    <td {...cell("Status")}>
                      <span className="px-2.5 py-1 rounded-full whitespace-nowrap" style={{
                        backgroundColor: styleCfg.bg,
                        color: styleCfg.text,
                        fontSize: "0.72rem",
                        fontWeight: 600,
                      }}>
                        {styleCfg.label}
                      </span>
                    </td>
                    <td {...cell("SN / Token")}>
                      {o.sn ? (
                        <span style={{ fontFamily: "monospace", fontSize: "0.82rem", color: "var(--color-brand-secondary)" }} className="block max-w-[150px] truncate" title={o.sn}>{o.sn}</span>
                      ) : (
                        <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.78rem" }}>-</span>
                      )}
                    </td>
                    <td {...cell("Aksi")}>
                      <div className="flex items-center gap-1.5">
                        <button
                          onClick={() => setDetailTarget(o)}
                          className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                          style={{ color: "var(--color-brand-secondary)" }}
                          onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-neutral)"; }}
                          onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                          aria-label={`Detail order ${o.orderNo}`}
                          title="Detail Order"
                        >
                          <Eye size={15} aria-hidden="true" />
                        </button>
                        {o.status === "NEEDS_ACTION" && (
                          <>
                            <button
                              onClick={() => setRetryTarget(o)}
                              className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                              style={{ color: "var(--color-brand-tertiary)" }}
                              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-info)"; }}
                              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                              aria-label={`Retry order ${o.orderNo}`}
                              title="Retry Order"
                            >
                              <RefreshCw size={15} aria-hidden="true" />
                            </button>
                            <button
                              onClick={() => setRefundTarget(o)}
                              className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                              style={{ color: "var(--color-danger)" }}
                              onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-danger)"; }}
                              onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                              aria-label={`Refund order ${o.orderNo}`}
                              title="Refund Order"
                            >
                              <Undo2 size={15} aria-hidden="true" />
                            </button>
                          </>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
              {!loading && orders.length === 0 && (
                <tr role="row">
                  <td role="cell" colSpan={COLUMNS.length} className="px-4 py-12 text-center" style={{ color: "var(--color-brand-secondary)" }}>Tidak ada order ditemukan</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3" style={{ borderTop: "1px solid var(--color-border-light)" }}>
            <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>Total {totalCount} order</span>
            <div className="flex items-center gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ border: "1px solid var(--color-border)" }}>
                <ChevronLeft size={15} />
              </button>
              <span style={{ fontSize: "0.85rem", color: "var(--color-brand-primary)" }}>{page}/{totalPages}</span>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ border: "1px solid var(--color-border)" }}>
                <ChevronRight size={15} />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* 1. Detail Order Modal */}
      {detailTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }}>
          <div className="w-full max-w-lg rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface)", maxHeight: "90vh", overflowY: "auto" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1rem" }}>Detail Transaksi Snapshot</h3>
              <button ref={detailCloseRef} onClick={() => setDetailTarget(null)} style={{ color: "var(--color-brand-secondary)" }} aria-label="Close modal"><X size={20} /></button>
            </div>
            
            <div className="flex items-center gap-4 p-4 rounded-xl mb-4" style={{ backgroundColor: "var(--color-neutral)" }}>
              <div className="w-16 h-16 rounded-xl overflow-hidden flex-shrink-0 flex items-center justify-center" style={{ backgroundColor: "var(--color-surface)" }}>
                {detailTarget.imageUrl ? (
                  <ImageWithFallback src={detailTarget.imageUrl} alt={detailTarget.productName} className="w-full h-full object-cover" />
                ) : (
                  <Clock size={24} style={{ color: "var(--color-brand-secondary)" }} />
                )}
              </div>
              <div>
                <span className="px-2 py-0.5 rounded-full text-xs font-semibold" style={{
                  backgroundColor: STATUS_STYLES[detailTarget.status]?.bg ?? "var(--color-neutral)",
                  color: STATUS_STYLES[detailTarget.status]?.text ?? "var(--color-brand-secondary)",
                }}>
                  {STATUS_STYLES[detailTarget.status]?.label ?? detailTarget.status}
                </span>
                <h4 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "0.95rem", marginTop: 4 }}>{detailTarget.productName}</h4>
                <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem", fontFamily: "monospace" }}>{detailTarget.orderNo}</p>
              </div>
            </div>

            <div className="space-y-3 mb-6">
              <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                <span style={{ color: "var(--color-brand-secondary)" }}>Tanggal Transaksi</span>
                <span style={{ color: "var(--color-brand-primary)", fontWeight: 500 }}>{formatDate(detailTarget.orderDate)}</span>
              </div>
              <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                <span style={{ color: "var(--color-brand-secondary)" }}>Player ID</span>
                <span style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontFamily: "monospace" }}>{detailTarget.playerId}</span>
              </div>
              
              <div className="pt-2" style={{ borderTop: "1px dashed var(--color-border-light)" }} />

              <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                <span style={{ color: "var(--color-brand-secondary)" }}>Harga Snapshot</span>
                <span style={{ color: "var(--color-brand-primary)" }}>{formatRp(detailTarget.price)}</span>
              </div>
              {detailTarget.discountAmount > 0 && (
                <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                  <span style={{ color: "var(--color-success)" }}>Diskon Voucher</span>
                  <span style={{ color: "var(--color-success)", fontWeight: 600 }}>-{formatRp(detailTarget.discountAmount)}</span>
                </div>
              )}
              <div className="flex justify-between pt-2" style={{ borderTop: "1px solid var(--color-border-light)", fontSize: "0.9rem" }}>
                <span style={{ color: "var(--color-brand-primary)", fontWeight: 600 }}>Total Dibayar</span>
                <span style={{ color: "var(--color-brand-tertiary)", fontWeight: 700 }}>{formatRp(detailTarget.finalPrice)}</span>
              </div>

              {detailTarget.sn && (
                <div className="mt-4 p-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral)" }}>
                  <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem" }}>Serial Number / Token</p>
                  <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.85rem", fontFamily: "monospace", marginTop: 2 }}>{detailTarget.sn}</p>
                </div>
              )}
            </div>

            <div className="flex gap-2">
              {detailTarget.status === "NEEDS_ACTION" && (
                <>
                  <button
                    onClick={() => { setRetryTarget(detailTarget); }}
                    className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2 text-white font-semibold transition-opacity hover:opacity-90"
                    style={{ backgroundColor: "var(--color-brand-tertiary)" }}
                  >
                    <RefreshCw size={15} /> Retry
                  </button>
                  <button
                    onClick={() => { setRefundTarget(detailTarget); }}
                    className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2 text-white font-semibold transition-opacity hover:opacity-90"
                    style={{ backgroundColor: "var(--color-danger)" }}
                  >
                    <Undo2 size={15} /> Refund
                  </button>
                </>
              )}
              <button onClick={() => setDetailTarget(null)} className="flex-1 py-3 rounded-xl border font-semibold" style={{ borderColor: "var(--color-border)", color: "var(--color-brand-secondary)" }}>
                Tutup
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 2. Retry Confirmation Modal */}
      {retryTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }}>
          <div className="w-full max-w-md rounded-2xl p-6 text-center" style={{ backgroundColor: "var(--color-surface)" }}>
            <div className="w-12 h-12 rounded-full mx-auto mb-4 flex items-center justify-center" style={{ backgroundColor: "var(--color-info-bg)" }}>
              <RefreshCw size={20} style={{ color: "var(--color-info)" }} />
            </div>
            <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.1rem", marginBottom: 8 }}>Retry Order</h3>
            <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.88rem", marginBottom: 20 }}>
              Order <strong style={{ fontFamily: "monospace" }}>{retryTarget.orderNo}</strong> ini akan dikirim ulang ke Digiflazz.<br />Apakah Anda yakin ingin melanjutkan?
            </p>
            <div className="flex gap-3">
              <button
                ref={retryConfirmRef}
                onClick={handleRetrySubmit}
                disabled={retryMutation.isPending}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2 text-white font-semibold transition-opacity hover:opacity-90"
                style={{ backgroundColor: "var(--color-brand-tertiary)" }}
              >
                {retryMutation.isPending && <Loader2 size={15} className="animate-spin" />}
                {retryMutation.isPending ? "Memproses..." : "Retry"}
              </button>
              <button
                onClick={() => setRetryTarget(null)}
                disabled={retryMutation.isPending}
                className="flex-1 py-3 rounded-xl border font-semibold"
                style={{ borderColor: "var(--color-border)", color: "var(--color-brand-secondary)" }}
              >
                Batal
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 3. Refund Confirmation Modal */}
      {refundTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }}>
          <div className="w-full max-w-md rounded-2xl p-6 text-center" style={{ backgroundColor: "var(--color-surface)" }}>
            <div className="w-12 h-12 rounded-full mx-auto mb-4 flex items-center justify-center" style={{ backgroundColor: "var(--color-danger-bg)" }}>
              <Undo2 size={20} style={{ color: "var(--color-danger)" }} />
            </div>
            <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.1rem", marginBottom: 8 }}>Refund Order</h3>
            <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.88rem", marginBottom: 10 }}>
              Order <strong style={{ fontFamily: "monospace" }}>{refundTarget.orderNo}</strong> akan ditandai sebagai <strong>REFUNDED</strong>.
            </p>
            <div className="p-3 rounded-xl text-left mb-5" style={{ backgroundColor: "var(--color-warning-bg)" }}>
              <div className="flex gap-2 items-start">
                <AlertTriangle size={16} className="flex-shrink-0" style={{ color: "var(--color-warning)", marginTop: 1 }} />
                <p style={{ color: "var(--color-warning)", fontSize: "0.78rem", fontWeight: 600, lineHeight: 1.4 }}>
                  PENTING: Pengembalian dana dilakukan secara manual. Harap selesaikan pengembalian ke rekening/e-wallet pembeli terlebih dahulu!
                </p>
              </div>
            </div>
            <div className="flex gap-3">
              <button
                ref={refundConfirmRef}
                onClick={handleRefundSubmit}
                disabled={refundMutation.isPending}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2 text-white font-semibold transition-opacity hover:opacity-90"
                style={{ backgroundColor: "var(--color-danger)" }}
              >
                {refundMutation.isPending && <Loader2 size={15} className="animate-spin" />}
                {refundMutation.isPending ? "Memproses..." : "Refund"}
              </button>
              <button
                onClick={() => setRefundTarget(null)}
                disabled={refundMutation.isPending}
                className="flex-1 py-3 rounded-xl border font-semibold"
                style={{ borderColor: "var(--color-border)", color: "var(--color-brand-secondary)" }}
              >
                Batal
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
