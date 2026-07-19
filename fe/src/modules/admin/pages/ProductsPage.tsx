import { useState } from "react";
import { Plus, Pencil, Trash2, X, Loader2, ChevronLeft, ChevronRight } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { AdminProductResponse, CreateProductPayload } from "../../../api/index";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

const EMPTY_FORM: CreateProductPayload = {
  name: "", price: 0, gameCode: "", buyerSkuCode: "",
  brand: "", category: "", imageUrl: null, customMargin: null,
};

const PAGE_SIZE = 10;

export default function ProductsPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<AdminProductResponse | null>(null);
  const [form, setForm] = useState<CreateProductPayload>(EMPTY_FORM);
  const [deleteId, setDeleteId] = useState<number | null>(null);

  const { data, isLoading: loading } = useQuery({
    queryKey: ["admin-products", page, search],
    queryFn: () => adminService.getProducts({ page, limit: PAGE_SIZE, search: search || undefined }),
  });

  const products = data?.data ?? [];
  const totalCount = data?.totalItems ?? 0;
  const totalPages = data?.totalPages ?? 1;

  const createMutation = useMutation({
    mutationFn: (payload: CreateProductPayload) => adminService.createProduct(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-products"] });
      toast.success("Produk dibuat");
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal membuat produk");
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: Partial<CreateProductPayload> }) => adminService.updateProduct(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-products"] });
      toast.success("Produk diperbarui");
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal memperbarui produk");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => adminService.deleteProduct(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-products"] });
      toast.success("Produk dihapus");
      setDeleteId(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal menghapus produk");
      setDeleteId(null);
    },
  });

  const openCreate = () => {
    setEditTarget(null);
    setForm(EMPTY_FORM);
    setModalOpen(true);
  };

  const openEdit = (p: AdminProductResponse) => {
    setEditTarget(p);
    setForm({ name: p.name, price: p.price, gameCode: p.gameCode, buyerSkuCode: p.buyerSkuCode, brand: p.brand, category: p.category, imageUrl: p.imageUrl, customMargin: p.customMargin });
    setModalOpen(true);
  };

  const handleSave = () => {
    if (!form.name || !form.brand || !form.gameCode) { toast.error("Isi field wajib"); return; }
    if (editTarget) {
      updateMutation.mutate({ id: editTarget.id, payload: form });
    } else {
      createMutation.mutate(form);
    }
  };

  const handleDelete = (id: number) => {
    setDeleteId(id);
    deleteMutation.mutate(id);
  };

  const saving = createMutation.isPending || updateMutation.isPending;

  return (
    <AdminLayout title="Products" breadcrumb={["Admin", "Products"]}>
      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row gap-3 mb-5">
        <div className="flex-1 relative">
          <input
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            placeholder="Cari produk atau brand..."
            className="w-full pl-9 pr-4 py-2.5 rounded-xl outline-none"
            style={{ border: "1.5px solid rgba(0,0,0,0.1)", fontSize: "0.9rem", backgroundColor: "var(--color-surface,#fff)", color: "var(--color-brand-primary,#0F1419)" }}
          />
        </div>
        <button onClick={openCreate} className="flex items-center gap-2 px-4 py-2.5 rounded-xl" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem", whiteSpace: "nowrap" }}>
          <Plus size={16} /> Tambah Produk
        </button>
      </div>

      {/* Table */}
      <div className="rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
        <div className="overflow-x-auto">
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid rgba(0,0,0,0.07)", backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                {["Nama", "Brand", "Kategori", "Game Code", "Harga", "Aksi"].map((h) => (
                  <th key={h} className="px-4 py-3 text-left" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.78rem", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 6 }).map((__, j) => (
                      <td key={j} className="px-4 py-3">
                        <div className="h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : products.map((p) => (
                <tr key={p.id} style={{ borderBottom: "1px solid rgba(0,0,0,0.05)" }}>
                  <td className="px-4 py-3" style={{ color: "var(--color-brand-primary,#0F1419)", fontSize: "0.9rem", fontWeight: 500 }}>{p.name}</td>
                  <td className="px-4 py-3" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>{p.brand}</td>
                  <td className="px-4 py-3" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>{p.category}</td>
                  <td className="px-4 py-3"><span style={{ fontFamily: "monospace", fontSize: "0.8rem", color: "var(--color-brand-secondary,#4A5568)", backgroundColor: "var(--color-neutral,#F1F3F5)", padding: "2px 6px", borderRadius: 4 }}>{p.gameCode}</span></td>
                  <td className="px-4 py-3" style={{ color: "var(--color-brand-tertiary,#2C5EF5)", fontSize: "0.9rem", fontWeight: 600 }}>{formatRp(p.price)}</td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <button onClick={() => openEdit(p)} className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors hover:bg-blue-50" style={{ color: "var(--color-brand-tertiary,#2C5EF5)" }}>
                        <Pencil size={14} />
                      </button>
                      <button onClick={() => handleDelete(p.id)} disabled={deleteId === p.id} className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors hover:bg-red-50" style={{ color: "#ef4444" }}>
                        {deleteId === p.id ? <Loader2 size={14} className="animate-spin" /> : <Trash2 size={14} />}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {!loading && products.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center" style={{ color: "var(--color-brand-secondary,#4A5568)" }}>Tidak ada produk ditemukan</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3" style={{ borderTop: "1px solid rgba(0,0,0,0.07)" }}>
            <span style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>{totalCount} produk</span>
            <div className="flex items-center gap-2">
              <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ border: "1px solid rgba(0,0,0,0.1)" }}>
                <ChevronLeft size={15} />
              </button>
              <span style={{ fontSize: "0.85rem", color: "var(--color-brand-primary,#0F1419)" }}>{page}/{totalPages}</span>
              <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page === totalPages} className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ border: "1px solid rgba(0,0,0,0.1)" }}>
                <ChevronRight size={15} />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "rgba(0,0,0,0.4)" }}>
          <div className="w-full max-w-lg rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface,#fff)", maxHeight: "90vh", overflowY: "auto" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 700, fontSize: "1rem" }}>
                {editTarget ? "Edit Produk" : "Tambah Produk"}
              </h3>
              <button onClick={() => setModalOpen(false)} style={{ color: "var(--color-brand-secondary,#4A5568)" }}><X size={20} /></button>
            </div>
            <div className="space-y-3">
              {[
                { label: "Nama *", key: "name", type: "text", placeholder: "86 Diamonds" },
                { label: "Brand *", key: "brand", type: "text", placeholder: "Mobile Legends" },
                { label: "Game Code *", key: "gameCode", type: "text", placeholder: "MOBA-ML" },
                { label: "Buyer SKU Code", key: "buyerSkuCode", type: "text", placeholder: "ml-86" },
                { label: "Kategori", key: "category", type: "text", placeholder: "Diamond" },
                { label: "Harga", key: "price", type: "number", placeholder: "19000" },
              ].map((field) => (
                <div key={field.key}>
                  <label style={{ color: "var(--color-brand-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>{field.label}</label>
                  <input
                    type={field.type}
                    value={String(form[field.key as keyof CreateProductPayload] ?? "")}
                    onChange={(e) => setForm({ ...form, [field.key]: field.type === "number" ? Number(e.target.value) : e.target.value })}
                    placeholder={field.placeholder}
                    className="w-full px-4 py-2.5 rounded-xl outline-none"
                    style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-brand-primary,#0F1419)" }}
                  />
                </div>
              ))}
            </div>
            <div className="flex gap-3 mt-6">
              <button onClick={handleSave} disabled={saving} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600 }}>
                {saving && <Loader2 size={15} className="animate-spin" />}
                {saving ? "Menyimpan..." : "Simpan"}
              </button>
              <button onClick={() => setModalOpen(false)} className="px-5 py-3 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-secondary,#4A5568)" }}>Batal</button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
