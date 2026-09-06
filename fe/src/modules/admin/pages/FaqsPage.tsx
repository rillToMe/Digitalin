import { useState, useEffect } from "react";
import { Plus, Search, Pencil, Trash2, X, Loader2, Star } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService, faqService } from "../../../services/api";
import type { FaqItem, CreateFaqPayload } from "../../../api/index";
import { cellProps, type AdminColumn } from "../../../components/admin/adminTable";

const DEBOUNCE_MS = 300;

/* Satu sumber header kolom tabel desktop + label kartu mobile. */
const COLUMNS: AdminColumn[] = [
  { label: "Pertanyaan" },
  { label: "Kategori" },
  { label: "Unggulan" },
  { label: "Aksi" },
];

const cell = cellProps(COLUMNS);

const EMPTY_FORM: CreateFaqPayload = {
  question: "",
  answer: "",
  category: "",
  featured: false,
};

export default function FaqsPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState<string>("all");
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<FaqItem | null>(null);
  const [form, setForm] = useState<CreateFaqPayload>(EMPTY_FORM);
  const [deleteTarget, setDeleteTarget] = useState<FaqItem | null>(null);

  // Debounce search (pola yang sama dengan VouchersPage)
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  // Kategori dari API — tidak ada union type hardcode di FE.
  const { data: categories = [] } = useQuery({
    queryKey: ["faq-categories"],
    queryFn: faqService.getCategories,
  });

  const { data: faqs, isLoading: loading } = useQuery({
    queryKey: ["admin-faqs", debouncedSearch, categoryFilter],
    queryFn: () =>
      adminService.getFaqs({
        search: debouncedSearch.trim() || undefined,
        category: categoryFilter === "all" ? undefined : categoryFilter,
      }),
  });

  const createMutation = useMutation({
    mutationFn: (payload: CreateFaqPayload) => adminService.createFaq(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-faqs"] });
      queryClient.invalidateQueries({ queryKey: ["faq-categories"] });
      queryClient.invalidateQueries({ queryKey: ["faqs"] });
      queryClient.invalidateQueries({ queryKey: ["faqs-featured"] });
      toast.success("FAQ berhasil dibuat");
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal membuat FAQ");
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: CreateFaqPayload }) =>
      adminService.updateFaq(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-faqs"] });
      queryClient.invalidateQueries({ queryKey: ["faq-categories"] });
      queryClient.invalidateQueries({ queryKey: ["faqs"] });
      queryClient.invalidateQueries({ queryKey: ["faqs-featured"] });
      toast.success("FAQ berhasil diperbarui");
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal memperbarui FAQ");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => adminService.deleteFaq(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-faqs"] });
      queryClient.invalidateQueries({ queryKey: ["faq-categories"] });
      queryClient.invalidateQueries({ queryKey: ["faqs"] });
      queryClient.invalidateQueries({ queryKey: ["faqs-featured"] });
      toast.success("FAQ berhasil dihapus");
      setDeleteTarget(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal menghapus FAQ");
      setDeleteTarget(null);
    },
  });

  const openCreate = () => {
    setEditTarget(null);
    setForm({ ...EMPTY_FORM, category: categories[0] ?? "" });
    setModalOpen(true);
  };

  const openEdit = (f: FaqItem) => {
    setEditTarget(f);
    setForm({
      question: f.question,
      answer: f.answer,
      category: f.category,
      featured: f.featured,
    });
    setModalOpen(true);
  };

  const handleSave = () => {
    if (!form.question.trim()) { toast.error("Pertanyaan wajib diisi"); return; }
    if (!form.answer.trim()) { toast.error("Jawaban wajib diisi"); return; }
    if (!form.category.trim()) { toast.error("Kategori wajib diisi"); return; }

    if (editTarget) {
      updateMutation.mutate({ id: editTarget.id, payload: form });
    } else {
      createMutation.mutate(form);
    }
  };

  const saving = createMutation.isPending || updateMutation.isPending;
  const items = faqs ?? [];
  const featuredCount = items.filter((f) => f.featured).length;

  return (
    <AdminLayout title="FAQ" breadcrumb={["Admin", "FAQ"]}>
      {/* Toolbar */}
      <div className="flex flex-col sm:flex-row gap-3 mb-5">
        <div className="flex-1 relative">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2" style={{ color: "var(--color-brand-secondary)" }} />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Cari pertanyaan atau jawaban..."
            className="w-full pl-9 pr-4 py-2.5 rounded-xl outline-none"
            style={{ border: "1.5px solid var(--color-border)", fontSize: "0.9rem", backgroundColor: "var(--color-surface)", color: "var(--color-brand-primary)" }}
          />
        </div>
        <button onClick={openCreate} className="flex items-center gap-2 px-4 py-2.5 rounded-xl" style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600, fontSize: "0.9rem", whiteSpace: "nowrap" }}>
          <Plus size={16} /> Tambah FAQ
        </button>
      </div>

      {/* Filter kategori — dari API */}
      <div className="flex flex-wrap gap-2 mb-4" role="group" aria-label="Filter kategori FAQ">
        {[{ label: "Semua", value: "all" }, ...categories.map((c) => ({ label: c, value: c }))].map((f) => {
          const active = categoryFilter === f.value;
          return (
            <button
              key={f.value}
              onClick={() => setCategoryFilter(f.value)}
              aria-pressed={active}
              className="px-4 py-2 rounded-full whitespace-nowrap transition-all"
              style={{
                backgroundColor: active ? "var(--color-brand-tertiary)" : "var(--color-surface)",
                color: active ? "var(--color-on-primary)" : "var(--color-brand-primary)",
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

      {/* Counter unggulan — konteks aturan maks 6 */}
      <p className="mb-3" style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem" }}>
        FAQ unggulan: {featuredCount}/6 (maksimal 6 — tampil di homepage)
      </p>

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
                    className="px-4 py-3 text-left"
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
                      <td key={c.label} role="cell" className="px-4 py-3">
                        <div className="h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : items.map((f) => (
                <tr key={f.id} role="row" style={{ borderBottom: "1px solid var(--color-border-lighter)" }}>
                  <td {...cell("Pertanyaan")} style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, maxWidth: 380 }}>
                    <span className="block truncate" title={f.question}>{f.question}</span>
                  </td>
                  <td {...cell("Kategori")}>
                    <span className="px-2 py-1 rounded-full" style={{ backgroundColor: "var(--color-info-bg)", color: "var(--color-info)", fontSize: "0.72rem", fontWeight: 600 }}>
                      {f.category}
                    </span>
                  </td>
                  <td {...cell("Unggulan")}>
                    <span className="flex items-center gap-1" style={{ color: f.featured ? "var(--color-warning)" : "var(--color-brand-secondary)", fontSize: "0.8rem", fontWeight: 600 }}>
                      <Star size={14} fill={f.featured ? "currentColor" : "none"} aria-hidden="true" />
                      {f.featured ? "Ya" : "Tidak"}
                    </span>
                  </td>
                  <td {...cell("Aksi")}>
                    <div className="flex items-center gap-1.5">
                      <button
                        onClick={() => openEdit(f)}
                        className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                        style={{ color: "var(--color-brand-tertiary)" }}
                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-info)"; }}
                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                        aria-label={`Edit FAQ ${f.id}`}
                      >
                        <Pencil size={14} aria-hidden="true" />
                      </button>
                      <button
                        onClick={() => setDeleteTarget(f)}
                        className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                        style={{ color: "var(--color-danger)" }}
                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = "var(--color-hover-danger)"; }}
                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = "transparent"; }}
                        aria-label={`Hapus FAQ ${f.id}`}
                      >
                        <Trash2 size={14} aria-hidden="true" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {!loading && items.length === 0 && (
                <tr role="row">
                  <td role="cell" colSpan={COLUMNS.length} className="px-4 py-12 text-center" style={{ color: "var(--color-brand-secondary)" }}>Tidak ada FAQ ditemukan</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Create/Edit Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "var(--color-overlay)" }}>
          <div className="w-full max-w-lg rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface)", maxHeight: "90vh", overflowY: "auto" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1rem" }}>
                {editTarget ? "Edit FAQ" : "Tambah FAQ"}
              </h3>
              <button onClick={() => setModalOpen(false)} style={{ color: "var(--color-brand-secondary)" }}><X size={20} /></button>
            </div>
            <div className="space-y-3">
              {/* Question */}
              <div>
                <label style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Pertanyaan *</label>
                <textarea
                  value={form.question}
                  onChange={(e) => setForm({ ...form, question: e.target.value })}
                  placeholder="Bagaimana cara melakukan top up game?"
                  rows={2}
                  className="w-full px-4 py-2.5 rounded-xl outline-none resize-y"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-brand-primary)" }}
                />
              </div>

              {/* Answer */}
              <div>
                <label style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Jawaban *</label>
                <textarea
                  value={form.answer}
                  onChange={(e) => setForm({ ...form, answer: e.target.value })}
                  placeholder="Pilih game, masukkan Player ID, pilih nominal, lalu bayar."
                  rows={4}
                  className="w-full px-4 py-2.5 rounded-xl outline-none resize-y"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-brand-primary)" }}
                />
              </div>

              {/* Category — dari API, plus input bebas untuk kategori baru */}
              <div>
                <label style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Kategori *</label>
                <input
                  list="faq-category-options"
                  value={form.category}
                  onChange={(e) => setForm({ ...form, category: e.target.value })}
                  placeholder="Pilih atau tulis kategori"
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-brand-primary)" }}
                />
                <datalist id="faq-category-options">
                  {categories.map((c) => (
                    <option key={c} value={c} />
                  ))}
                </datalist>
              </div>

              {/* Featured */}
              <div className="flex items-center justify-between py-2">
                <div>
                  <label style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500 }}>FAQ Unggulan</label>
                  <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem", marginTop: 2 }}>
                    Tampil di homepage (maksimal 6)
                  </p>
                </div>
                <button
                  onClick={() => setForm({ ...form, featured: !form.featured })}
                  className="flex items-center gap-2 px-3 py-1.5 rounded-lg"
                  style={{
                    backgroundColor: form.featured ? "var(--color-success-bg)" : "var(--color-danger-bg)",
                    color: form.featured ? "var(--color-success)" : "var(--color-danger)",
                    fontSize: "0.82rem",
                    fontWeight: 600,
                    border: "none",
                  }}
                >
                  {form.featured ? "Unggulan" : "Biasa"}
                </button>
              </div>
            </div>

            <div className="flex gap-3 mt-6">
              <button onClick={handleSave} disabled={saving} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600 }}>
                {saving && <Loader2 size={15} className="animate-spin" />}
                {saving ? "Menyimpan..." : "Simpan"}
              </button>
              <button onClick={() => setModalOpen(false)} className="px-5 py-3 rounded-xl" style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-secondary)" }}>Batal</button>
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
            <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1rem", marginBottom: 8 }}>Hapus FAQ?</h3>
            <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem", marginBottom: 4 }}>
              Pertanyaan <strong>{deleteTarget.question}</strong> akan dihapus permanen.
            </p>
            <div className="flex gap-3 mt-4">
              <button onClick={() => setDeleteTarget(null)} className="flex-1 py-3 rounded-xl" style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-secondary)" }}>Batal</button>
              <button
                onClick={() => deleteMutation.mutate(deleteTarget.id)}
                disabled={deleteMutation.isPending}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                style={{ backgroundColor: "var(--color-danger)", color: "var(--color-on-primary)", fontWeight: 600 }}
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
