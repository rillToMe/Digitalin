import { useState, useEffect } from "react";
import { Plus, Search, Pencil, Trash2, X, Loader2, ChevronLeft, ChevronRight, ToggleLeft, ToggleRight } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { AdminVoucherItem, CreateVoucherPayload, DiscountType, VoucherStatus } from "../../../api/index";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" });
}

// Status badge uses CSS variables — no hex values in component
const STATUS_STYLES: Record<VoucherStatus, { bg: string; text: string }> = {
  Active: { bg: "var(--color-success-bg)", text: "var(--color-success)" },
  Disabled: { bg: "var(--color-danger-bg)", text: "var(--color-danger)" },
  Expired: { bg: "var(--color-warning-bg)", text: "var(--color-warning)" },
  QuotaExceeded: { bg: "var(--color-info-bg)", text: "var(--color-info)" },
};

const STATUS_LABELS: Record<VoucherStatus, string> = {
  Active: "Active",
  Disabled: "Disabled",
  Expired: "Expired",
  QuotaExceeded: "Quota Full",
};

const STATUS_FILTERS: { label: string; value: VoucherStatus | "all" }[] = [
  { label: "Semua", value: "all" },
  { label: "Active", value: "Active" },
  { label: "Disabled", value: "Disabled" },
  { label: "Expired", value: "Expired" },
  { label: "Quota Full", value: "QuotaExceeded" },
];

const EMPTY_FORM: CreateVoucherPayload = {
  code: "",
  discountType: "Percentage",
  discountValue: 0,
  minimumTransaction: 0,
  maximumDiscount: null,
  usageLimit: 1,
  expiredAt: "",
  isActive: true,
};

const PAGE_SIZE = 10;
const DEBOUNCE_MS = 300;

export default function VouchersPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<VoucherStatus | "all">("all");
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<AdminVoucherItem | null>(null);
  const [form, setForm] = useState<CreateVoucherPayload>(EMPTY_FORM);
  const [deleteTarget, setDeleteTarget] = useState<AdminVoucherItem | null>(null);

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => { setDebouncedSearch(search); setPage(1); }, DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  const { data, isLoading: loading } = useQuery({
    queryKey: ["admin-vouchers", page, debouncedSearch, statusFilter],
    queryFn: () => adminService.getVouchers({
      page, limit: PAGE_SIZE,
      search: debouncedSearch || undefined,
      status: statusFilter === "all" ? undefined : statusFilter,
    }),
  });

  const vouchers = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;

  // Mutations
  const createMutation = useMutation({
    mutationFn: (payload: CreateVoucherPayload) => adminService.createVoucher(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-vouchers"] });
      toast.success("Voucher berhasil dibuat");
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal membuat voucher");
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: CreateVoucherPayload }) => adminService.updateVoucher(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-vouchers"] });
      toast.success("Voucher berhasil diperbarui");
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal memperbarui voucher");
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) => adminService.updateVoucherStatus(id, { isActive }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-vouchers"] });
      toast.success("Status voucher diperbarui");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal mengubah status voucher");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => adminService.deleteVoucher(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-vouchers"] });
      toast.success("Voucher berhasil dihapus");
      setDeleteTarget(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal menghapus voucher");
      setDeleteTarget(null);
    },
  });

  // Form handlers
  const openCreate = () => {
    setEditTarget(null);
    setForm(EMPTY_FORM);
    setModalOpen(true);
  };

  const openEdit = (v: AdminVoucherItem) => {
    setEditTarget(v);
    setForm({
      code: v.code,
      discountType: v.discountType,
      discountValue: v.discountValue,
      minimumTransaction: v.minimumTransaction,
      maximumDiscount: v.maximumDiscount,
      usageLimit: v.usageLimit,
      expiredAt: v.expiredAt.slice(0, 16),
      isActive: v.isActive,
    });
    setModalOpen(true);
  };

  const handleSave = () => {
    if (!form.code.trim()) { toast.error("Kode voucher wajib diisi"); return; }
    if (form.discountValue <= 0) { toast.error("Nilai diskon harus lebih dari 0"); return; }
    if (form.usageLimit < 1) { toast.error("Batas penggunaan minimal 1"); return; }
    if (!form.expiredAt) { toast.error("Tanggal kedaluwarsa wajib diisi"); return; }

    const payload: CreateVoucherPayload = {
      ...form,
      expiredAt: new Date(form.expiredAt).toISOString(),
      maximumDiscount: form.discountType === "FixedAmount" ? null : form.maximumDiscount,
    };

    if (editTarget) {
      updateMutation.mutate({ id: editTarget.id, payload });
    } else {
      createMutation.mutate(payload);
    }
  };

  const saving = createMutation.isPending || updateMutation.isPending;

  return (
    <AdminLayout title="Voucher Discount" breadcrumb={["Admin", "Vouchers"]}>
      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row gap-3 mb-5">
        <div className="flex-1 relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: "var(--color-secondary)" }} />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Cari kode voucher..."
            className="w-full pl-9 pr-4 py-2.5 rounded-xl outline-none"
            style={{ border: "1.5px solid var(--color-border)", fontSize: "0.9rem", backgroundColor: "var(--color-surface)", color: "var(--color-primary)" }}
          />
        </div>
        <button onClick={openCreate} className="flex items-center gap-2 px-4 py-2.5 rounded-xl" style={{ backgroundColor: "var(--color-tertiary)", color: "var(--color-on-primary)", fontWeight: 600, fontSize: "0.9rem", whiteSpace: "nowrap" }}>
          <Plus size={16} /> Tambah Voucher
        </button>
      </div>

      {/* Filter tabs */}
      <div className="flex gap-2 mb-4 overflow-x-auto pb-1" style={{ scrollbarWidth: "none" }}>
        {STATUS_FILTERS.map((f) => {
          const active = statusFilter === f.value;
          return (
            <button
              key={f.value}
              onClick={() => { setStatusFilter(f.value); setPage(1); }}
              className="px-4 py-2 rounded-full whitespace-nowrap transition-all"
              style={{
                backgroundColor: active ? "var(--color-tertiary)" : "var(--color-surface)",
                color: active ? "var(--color-on-primary)" : "var(--color-primary)",
                border: active ? "none" : "1px solid var(--color-border)",
                fontWeight: active ? 600 : 400,
                fontSize: "0.82rem",
              }}
            >
              {f.label}
            </button>
          );
        })}
      </div>

      {/* Table */}
      <div className="rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
        <div className="overflow-x-auto">
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid var(--color-border-light)", backgroundColor: "var(--color-neutral)" }}>
                {["Kode", "Tipe", "Nilai", "Min. Transaksi", "Penggunaan", "Berakhir", "Status", "Aksi"].map((h) => (
                  <th key={h} className="px-4 py-3 text-left" style={{ color: "var(--color-secondary)", fontSize: "0.75rem", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 8 }).map((__, j) => (
                      <td key={j} className="px-4 py-3">
                        <div className="h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : vouchers.map((v) => (
                <tr key={v.id} style={{ borderBottom: "1px solid var(--color-border-lighter)" }}>
                  <td className="px-4 py-3">
                    <span style={{ fontFamily: "monospace", fontSize: "0.85rem", color: "var(--color-primary)", fontWeight: 600 }}>{v.code}</span>
                  </td>
                  <td className="px-4 py-3" style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>
                    {v.discountType === "Percentage" ? "Persentase" : "Nominal"}
                  </td>
                  <td className="px-4 py-3" style={{ color: "var(--color-tertiary)", fontSize: "0.9rem", fontWeight: 600 }}>
                    {v.discountType === "Percentage" ? `${v.discountValue}%` : formatRp(v.discountValue)}
                  </td>
                  <td className="px-4 py-3" style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>
                    {formatRp(v.minimumTransaction)}
                  </td>
                  <td className="px-4 py-3" style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>
                    {v.usedCount}/{v.usageLimit}
                  </td>
                  <td className="px-4 py-3" style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>
                    {formatDate(v.expiredAt)}
                  </td>
                  <td className="px-4 py-3">
                    <span className="px-2 py-1 rounded-full" style={{
                      backgroundColor: STATUS_STYLES[v.status].bg,
                      color: STATUS_STYLES[v.status].text,
                      fontSize: "0.72rem",
                      fontWeight: 600,
                    }}>
                      {STATUS_LABELS[v.status]}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1.5">
                      <button
                        onClick={() => toggleMutation.mutate({ id: v.id, isActive: !v.isActive })}
                        disabled={toggleMutation.isPending}
                        className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                        style={{ color: v.isActive ? "var(--color-danger)" : "var(--color-success)" }}
                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-neutral)"; }}
                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                        title={v.isActive ? "Nonaktifkan" : "Aktifkan"}
                      >
                        {v.isActive ? <ToggleLeft size={15} /> : <ToggleRight size={15} />}
                      </button>
                      <button
                        onClick={() => openEdit(v)}
                        className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                        style={{ color: "var(--color-tertiary)" }}
                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-info)"; }}
                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                      >
                        <Pencil size={14} />
                      </button>
                      <button
                        onClick={() => setDeleteTarget(v)}
                        className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                        style={{ color: "var(--color-danger)" }}
                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-danger)"; }}
                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {!loading && vouchers.length === 0 && (
                <tr>
                  <td colSpan={8} className="px-4 py-12 text-center" style={{ color: "var(--color-secondary)" }}>Tidak ada voucher ditemukan</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3" style={{ borderTop: "1px solid var(--color-border-light)" }}>
            <span style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>{totalCount} voucher</span>
            <div className="flex items-center gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ border: "1px solid var(--color-border)" }}>
                <ChevronLeft size={15} />
              </button>
              <span style={{ fontSize: "0.85rem", color: "var(--color-primary)" }}>{page}/{totalPages}</span>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ border: "1px solid var(--color-border)" }}>
                <ChevronRight size={15} />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Create/Edit Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }}>
          <div className="w-full max-w-lg rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface)", maxHeight: "90vh", overflowY: "auto" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-primary)", fontWeight: 700, fontSize: "1rem" }}>
                {editTarget ? "Edit Voucher" : "Tambah Voucher"}
              </h3>
              <button onClick={() => setModalOpen(false)} style={{ color: "var(--color-secondary)" }}><X size={20} /></button>
            </div>
            <div className="space-y-3">
              {/* Code */}
              <div>
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Kode Voucher *</label>
                <input
                  value={form.code}
                  onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, "") })}
                  placeholder="HEMAT20"
                  maxLength={50}
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-primary)", fontFamily: "monospace" }}
                />
              </div>

              {/* Discount Type */}
              <div>
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Tipe Diskon *</label>
                <div className="flex gap-2">
                  {(["Percentage", "FixedAmount"] as DiscountType[]).map((t) => (
                    <button
                      key={t}
                      onClick={() => setForm({ ...form, discountType: t, maximumDiscount: t === "FixedAmount" ? null : form.maximumDiscount })}
                      className="flex-1 py-2.5 rounded-xl transition-all"
                      style={{
                        backgroundColor: form.discountType === t ? "var(--color-tertiary)" : "var(--color-surface)",
                        color: form.discountType === t ? "var(--color-on-primary)" : "var(--color-primary)",
                        border: form.discountType === t ? "none" : "1.5px solid var(--color-border-input)",
                        fontWeight: 500,
                        fontSize: "0.85rem",
                      }}
                    >
                      {t === "Percentage" ? "Persentase" : "Nominal"}
                    </button>
                  ))}
                </div>
              </div>

              {/* Discount Value */}
              <div>
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>
                  Nilai Diskon * {form.discountType === "Percentage" ? "(%)" : "(Rp)"}
                </label>
                <input
                  type="number"
                  value={form.discountValue || ""}
                  onChange={(e) => setForm({ ...form, discountValue: Number(e.target.value) })}
                  placeholder={form.discountType === "Percentage" ? "10" : "5000"}
                  min={0}
                  max={form.discountType === "Percentage" ? 100 : undefined}
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-primary)" }}
                />
              </div>

              {/* Min Transaction */}
              <div>
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Minimal Transaksi (Rp) *</label>
                <input
                  type="number"
                  value={form.minimumTransaction || ""}
                  onChange={(e) => setForm({ ...form, minimumTransaction: Number(e.target.value) })}
                  placeholder="25000"
                  min={0}
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-primary)" }}
                />
              </div>

              {/* Max Discount (only for Percentage) */}
              {form.discountType === "Percentage" && (
                <div>
                  <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Maks. Diskon (Rp, opsional)</label>
                  <input
                    type="number"
                    value={form.maximumDiscount ?? ""}
                    onChange={(e) => setForm({ ...form, maximumDiscount: e.target.value ? Number(e.target.value) : null })}
                    placeholder="15000 (kosongkan = tanpa batas)"
                    min={0}
                    className="w-full px-4 py-2.5 rounded-xl outline-none"
                    style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-primary)" }}
                  />
                </div>
              )}

              {/* Usage Limit */}
              <div>
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Batas Penggunaan *</label>
                <input
                  type="number"
                  value={form.usageLimit || ""}
                  onChange={(e) => setForm({ ...form, usageLimit: Number(e.target.value) })}
                  placeholder="100"
                  min={1}
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-primary)" }}
                />
              </div>

              {/* Expired At */}
              <div>
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Tanggal Kedaluwarsa *</label>
                <input
                  type="datetime-local"
                  value={form.expiredAt}
                  onChange={(e) => setForm({ ...form, expiredAt: e.target.value })}
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-primary)" }}
                />
              </div>

              {/* Status */}
              <div className="flex items-center justify-between py-2">
                <label style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500 }}>Status Aktif</label>
                <button
                  onClick={() => setForm({ ...form, isActive: !form.isActive })}
                  className="flex items-center gap-2 px-3 py-1.5 rounded-lg"
                  style={{
                    backgroundColor: form.isActive ? "var(--color-success-bg)" : "var(--color-danger-bg)",
                    color: form.isActive ? "var(--color-success)" : "var(--color-danger)",
                    fontSize: "0.82rem",
                    fontWeight: 600,
                    border: "none",
                  }}
                >
                  {form.isActive ? "Aktif" : "Nonaktif"}
                </button>
              </div>
            </div>

            <div className="flex gap-3 mt-6">
              <button onClick={handleSave} disabled={saving} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-tertiary)", color: "var(--color-on-primary)", fontWeight: 600 }}>
                {saving && <Loader2 size={15} className="animate-spin" />}
                {saving ? "Menyimpan..." : "Simpan"}
              </button>
              <button onClick={() => setModalOpen(false)} className="px-5 py-3 rounded-xl" style={{ border: "1px solid var(--color-border)", color: "var(--color-secondary)" }}>Batal</button>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {deleteTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }}>
          <div className="w-full max-w-sm rounded-2xl p-6 text-center" style={{ backgroundColor: "var(--color-surface)" }}>
            <div className="w-12 h-12 rounded-full mx-auto mb-4 flex items-center justify-center" style={{ backgroundColor: "var(--color-danger-bg)" }}>
              <Trash2 size={20} style={{ color: "var(--color-danger)" }} />
            </div>
            <h3 style={{ color: "var(--color-primary)", fontWeight: 700, fontSize: "1rem", marginBottom: 8 }}>Hapus Voucher?</h3>
            <p style={{ color: "var(--color-secondary)", fontSize: "0.85rem", marginBottom: 4 }}>
              Voucher <strong style={{ fontFamily: "monospace" }}>{deleteTarget.code}</strong> akan dihapus permanen.
            </p>
            {deleteTarget.usedCount > 0 && (
              <p style={{ color: "var(--color-danger)", fontSize: "0.8rem", marginBottom: 16 }}>
                Voucher ini sudah digunakan {deleteTarget.usedCount} kali dan tidak dapat dihapus. Nonaktifkan sebagai gantinya.
              </p>
            )}
            <div className="flex gap-3 mt-4">
              <button onClick={() => setDeleteTarget(null)} className="flex-1 py-3 rounded-xl" style={{ border: "1px solid var(--color-border)", color: "var(--color-secondary)" }}>Batal</button>
              <button
                onClick={() => deleteMutation.mutate(deleteTarget.id)}
                disabled={deleteTarget.usedCount > 0 || deleteMutation.isPending}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                style={{
                  backgroundColor: deleteTarget.usedCount > 0 ? "var(--color-danger-muted)" : "var(--color-danger)",
                  color: "var(--color-on-primary)",
                  fontWeight: 600,
                }}
              >
                {deleteMutation.isPending && <Loader2 size={15} className="animate-spin" />}
                {deleteMutation.isPending ? "Menghapus..." : "Hapus"}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
