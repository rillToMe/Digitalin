import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Loader2, Plus, Save, Settings2, Trash2, X } from "lucide-react";
import { toast } from "sonner";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { AdminCategoryRow, GameCategoryItem, UpdateGameCategoriesPayload } from "../../../api/index";
import { cellProps, type AdminColumn } from "../../../components/admin/adminTable";
import { MAX_CATEGORY_NAME_LENGTH, MAX_CATEGORIES, categorySlugPreview } from "../../../data/gameCategories";

const COLUMNS: AdminColumn[] = [
  { label: "Urutan" },
  { label: "Nama Kategori" },
  { label: "Slug", wide: true },
  { label: "Dipakai" },
];

const cell = cellProps(COLUMNS);

const MODAL_TITLE_ID = "kelola-kategori-title";

function toDraft(categories: GameCategoryItem[]): AdminCategoryRow[] {
  return categories.map((category, index) => ({
    localId: index,
    slug: category.slug,
    name: category.name,
    brandCount: category.brandCount,
  }));
}

export default function CategoriesPage() {
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [draft, setDraft] = useState<AdminCategoryRow[]>([]);
  const [nextId, setNextId] = useState(0);

  const { data, isLoading } = useQuery({
    queryKey: ["adminGameCategories"],
    queryFn: adminService.getGameCategories,
  });

  const categories = data ?? [];

  const saveMutation = useMutation({
    mutationFn: (payload: UpdateGameCategoriesPayload) => adminService.updateGameCategories(payload),
    onSuccess: (result) => {
      queryClient.setQueryData<GameCategoryItem[]>(["adminGameCategories"], result.categories);
      // Storefront ikut berubah: urutan section, nama, dan daftar kategori.
      queryClient.invalidateQueries({ queryKey: ["gameCategories"] });
      toast.success(result.message);
      setModalOpen(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal menyimpan kategori");
    },
  });

  // Draft dibuat saat modal dibuka, jadi tutup modal = buang perubahan.
  const openModal = () => {
    setDraft(toDraft(categories));
    setNextId(categories.length);
    setModalOpen(true);
  };

  const closeModal = () => {
    if (saveMutation.isPending) return;
    setModalOpen(false);
  };

  useEffect(() => {
    if (!modalOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !saveMutation.isPending) setModalOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [modalOpen, saveMutation.isPending]);

  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= draft.length) return;
    const next = [...draft];
    [next[index], next[target]] = [next[target], next[index]];
    setDraft(next);
  };

  const rename = (localId: number, name: string) =>
    setDraft((prev) => prev.map((row) => (row.localId === localId ? { ...row, name } : row)));

  const remove = (localId: number) =>
    setDraft((prev) => prev.filter((row) => row.localId !== localId));

  const addRow = () => {
    if (draft.length >= MAX_CATEGORIES) {
      toast.error(`Maksimal ${MAX_CATEGORIES} kategori.`);
      return;
    }
    setDraft((prev) => [...prev, { localId: nextId, slug: null, name: "", brandCount: 0 }]);
    setNextId((id) => id + 1);
  };

  const save = () => {
    if (draft.length === 0) {
      toast.error("Minimal satu kategori harus ada.");
      return;
    }
    if (draft.some((row) => !row.name.trim())) {
      toast.error("Nama kategori tidak boleh kosong.");
      return;
    }
    saveMutation.mutate({
      categories: draft.map((row) => ({ slug: row.slug, name: row.name.trim() })),
    });
  };

  return (
    <AdminLayout title="Kategori Storefront" breadcrumb={["Admin", "Kategori"]}>
      <div className="flex flex-col sm:flex-row sm:items-start gap-3 mb-5">
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem", maxWidth: "62ch" }}>
          Urutan baris di bawah adalah urutan section kategori di homepage. Ganti urutan, ubah nama,
          atau tambah kategori custom dari tombol Kelola Kategori.
        </p>
        <button
          onClick={openModal}
          disabled={isLoading}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl sm:ml-auto"
          style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600, fontSize: "0.9rem", whiteSpace: "nowrap" }}
        >
          <Settings2 size={16} aria-hidden="true" /> Kelola Kategori
        </button>
      </div>

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
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} role="row">
                    {COLUMNS.map((c) => (
                      <td key={c.label} role="cell" className={`px-4 py-3${c.wide ? " admin-col-wide" : ""}`}>
                        <div className="h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : categories.map((category, index) => (
                <tr key={category.slug} role="row" style={{ borderBottom: "1px solid var(--color-border-lighter)" }}>
                  <td {...cell("Urutan")} style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
                    {index + 1}
                  </td>
                  <td {...cell("Nama Kategori")} style={{ color: "var(--color-brand-primary)", fontSize: "0.9rem", fontWeight: 600 }}>
                    {category.name}
                  </td>
                  <td {...cell("Slug")} style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", fontFamily: "monospace" }}>
                    {category.slug}
                  </td>
                  <td {...cell("Dipakai")} style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
                    {category.brandCount} game
                  </td>
                </tr>
              ))}
              {!isLoading && categories.length === 0 && (
                <tr role="row">
                  <td role="cell" colSpan={COLUMNS.length} className="px-4 py-12 text-center" style={{ color: "var(--color-brand-secondary)" }}>
                    Belum ada kategori.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {modalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4"
          style={{ backgroundColor: "var(--color-overlay)" }}
          onClick={closeModal}
        >
          {/* Klik di dalam panel tidak boleh menutup modal (draft hilang). */}
          <div
            role="dialog"
            aria-modal="true"
            aria-labelledby={MODAL_TITLE_ID}
            onClick={(e) => e.stopPropagation()}
            className="w-full max-w-xl rounded-2xl p-6 flex flex-col"
            style={{ backgroundColor: "var(--color-surface)", maxHeight: "90vh" }}
          >
            <div className="flex items-center justify-between mb-4">
              <h3 id={MODAL_TITLE_ID} style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1rem" }}>
                Kelola Kategori
              </h3>
              <button onClick={closeModal} aria-label="Tutup" style={{ color: "var(--color-brand-secondary)" }}>
                <X size={20} aria-hidden="true" />
              </button>
            </div>

            <p className="mb-4" style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem" }}>
              Nama boleh diganti kapan saja — slug tetap, jadi game yang sudah memakainya tidak lepas.
              Kategori yang masih dipakai game tidak bisa dihapus.
            </p>

            <div className="flex-1 overflow-y-auto -mx-1 px-1 space-y-2">
              {draft.map((row, index) => (
                <div
                  key={row.localId}
                  className="flex items-start gap-2 p-2 rounded-xl"
                  style={{ border: "1px solid var(--color-border-light)" }}
                >
                  <div className="flex flex-col gap-1">
                    <button
                      onClick={() => move(index, -1)}
                      disabled={index === 0}
                      className="w-7 h-7 rounded-lg flex items-center justify-center"
                      style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-primary)", opacity: index === 0 ? 0.4 : 1 }}
                      aria-label={`Naikkan ${row.name || "kategori baru"}`}
                    >
                      <ArrowUp size={13} aria-hidden="true" />
                    </button>
                    <button
                      onClick={() => move(index, 1)}
                      disabled={index === draft.length - 1}
                      className="w-7 h-7 rounded-lg flex items-center justify-center"
                      style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-primary)", opacity: index === draft.length - 1 ? 0.4 : 1 }}
                      aria-label={`Turunkan ${row.name || "kategori baru"}`}
                    >
                      <ArrowDown size={13} aria-hidden="true" />
                    </button>
                  </div>

                  <div className="flex-1 min-w-0">
                    <input
                      value={row.name}
                      onChange={(e) => rename(row.localId, e.target.value)}
                      maxLength={MAX_CATEGORY_NAME_LENGTH}
                      placeholder="Konsol Game"
                      aria-label={`Nama kategori urutan ${index + 1}`}
                      className="w-full px-3 py-2 rounded-lg outline-none"
                      style={{ border: "1.5px solid var(--color-border-input)", fontSize: "0.9rem", color: "var(--color-brand-primary)", backgroundColor: "var(--color-surface)" }}
                    />
                    <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.72rem" }}>
                      <span style={{ fontFamily: "monospace" }}>
                        {row.slug ?? (categorySlugPreview(row.name) || "—")}
                      </span>
                      {row.slug === null ? " (baru)" : ` · ${row.brandCount} game`}
                    </span>
                  </div>

                  <button
                    onClick={() => remove(row.localId)}
                    disabled={row.brandCount > 0}
                    className="w-8 h-8 rounded-lg flex items-center justify-center shrink-0"
                    style={{ color: "var(--color-danger)", opacity: row.brandCount > 0 ? 0.4 : 1 }}
                    aria-label={`Hapus kategori ${row.name || "baru"}`}
                    title={row.brandCount > 0 ? "Masih dipakai game — pindahkan game-nya dulu" : "Hapus kategori"}
                  >
                    <Trash2 size={14} aria-hidden="true" />
                  </button>
                </div>
              ))}

              <button
                onClick={addRow}
                className="w-full flex items-center justify-center gap-2 py-2.5 rounded-xl"
                style={{ border: "1.5px dashed var(--color-border)", color: "var(--color-brand-secondary)", fontWeight: 600, fontSize: "0.85rem" }}
              >
                <Plus size={15} aria-hidden="true" /> Kategori Custom
              </button>
            </div>

            <div className="flex gap-3 mt-5">
              <button
                onClick={save}
                disabled={saveMutation.isPending}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600 }}
              >
                {saveMutation.isPending
                  ? <Loader2 size={15} className="animate-spin" aria-hidden="true" />
                  : <Save size={15} aria-hidden="true" />}
                {saveMutation.isPending ? "Menyimpan..." : "Simpan"}
              </button>
              <button
                onClick={closeModal}
                className="px-5 py-3 rounded-xl"
                style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-secondary)" }}
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
