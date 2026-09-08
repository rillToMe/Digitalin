import { useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  X,
  Loader2,
  Save,
  Upload,
  Image,
  Info
} from "lucide-react";
import { toast } from "sonner";
import type {
  GameEditModalProps,
  ModalTab,
} from "../../../api/index";
import { adminService } from "../../../services/api";
import { CATEGORY_DEFINITIONS } from "../../../data/gameCategories";
import { ProductMarginList } from "./ProductMarginList";
import { DynamicInputFieldsEditor } from "./DynamicInputFieldsEditor";
import { PresetTemplatePicker } from "./PresetTemplatePicker";

export function GameEditModal({
  item, form, setForm, activeTab, setActiveTab,
  coverFile, setCoverFile, coverPreview, setCoverPreview,
  products, productsLoading, productsError, totalProducts,
  updateMutation, coverMutation, marginMutation, onClose,
}: GameEditModalProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") onClose();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [onClose]);

  // Registry kategori dari admin (termasuk kategori custom & yang belum dipakai).
  // Cache dibagi dengan CategoriesPage, fallback ke definisi statis selama loading.
  const { data: registry } = useQuery({
    queryKey: ["adminGameCategories"],
    queryFn: adminService.getGameCategories,
    staleTime: 60_000,
  });
  const categories = registry ?? CATEGORY_DEFINITIONS;

  const handleCoverUpload = () => {
    if (!coverFile) return;
    const formData = new FormData();
    formData.append("file", coverFile);
    coverMutation.mutate({ id: item.id, formData });
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) {
      toast.error("Ukuran file maksimal 2 MB");
      return;
    }
    setCoverFile(file);
    setCoverPreview(URL.createObjectURL(file));
  };

  const handleSaveMetadata = () => {
    updateMutation.mutate(
      {
        id: item.id,
        payload: form,
      },
      { onSuccess: onClose }
    );
  };

  // Satu game boleh masuk beberapa kategori (game PC yang juga rilis mobile).
  const selectedCategories = form.categories ?? [];

  // Slug yang sudah tersimpan tapi tidak ada di registry (dihapus admin, atau
  // registry belum termuat) tetap ditampilkan — kalau tidak, tercentangnya
  // hilang dari UI lalu terhapus diam-diam saat simpan.
  const categoryOptions = [
    ...categories,
    ...selectedCategories
      .filter((slug) => !categories.some((c) => c.slug === slug))
      .map((slug) => ({ slug, name: slug })),
  ];

  const toggleCategory = (slug: string) => {
    setForm({
      ...form,
      categories: selectedCategories.includes(slug)
        ? selectedCategories.filter((s) => s !== slug)
        : [...selectedCategories, slug],
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "rgba(0,0,0,0.4)" }}>
      {/* Tinggi dikunci supaya kedua tab punya frame yang sama — tab produk tidak
          menyusut saat produknya cuma satu. Isi yang lebih panjang di-scroll. */}
      <div className="w-full max-w-2xl rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface,#fff)", height: "min(90vh, 680px)", display: "flex", flexDirection: "column" }}>
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4" style={{ borderBottom: "1px solid rgba(0,0,0,0.07)" }}>
          <h3 style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 700, fontSize: "1rem" }}>Kelola: {item.brand}</h3>
          <button onClick={onClose} style={{ color: "var(--color-brand-secondary,#4A5568)" }}><X size={20} /></button>
        </div>

        {/* Tabs */}
        <div className="flex px-6 pt-3 gap-2">
          {([
            { key: "info" as ModalTab, label: "Info & Cover" },
            { key: "products" as ModalTab, label: "Produk & Margin" },
          ]).map((tab) => (
            <button
              key={tab.key}
              onClick={() => setActiveTab(tab.key)}
              className="px-4 py-2 rounded-t-lg transition-all"
              style={{
                backgroundColor: activeTab === tab.key ? "var(--color-brand-tertiary,#2C5EF5)" : "transparent",
                color: activeTab === tab.key ? "#fff" : "var(--color-brand-secondary,#4A5568)",
                fontSize: "0.85rem", fontWeight: activeTab === tab.key ? 600 : 400,
                border: activeTab === tab.key ? "none" : "1px solid rgba(0,0,0,0.1)",
              }}
            >
              {tab.label}
            </button>
          ))}
        </div>

        {/* Tab Content */}
        <div className="flex-1 overflow-y-auto px-6 py-5">
          {activeTab === "info" ? (
            <div className="space-y-5">
              {/* Cover Upload */}
              <div>
                <label style={{ color: "var(--color-brand-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 8 }}>Cover Game</label>
                <div className="flex gap-4 items-start">
                  <div className="w-32 h-20 rounded-xl overflow-hidden flex-shrink-0" style={{ border: "1px solid rgba(0,0,0,0.1)" }}>
                    {(coverPreview || item.imageUrl) ? (
                      <img src={coverPreview || item.imageUrl!} alt="cover" className="w-full h-full object-cover" />
                    ) : (
                      <div className="w-full h-full flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                        <Image size={20} style={{ color: "var(--color-brand-secondary,#4A5568)" }} />
                      </div>
                    )}
                  </div>
                  <div className="flex-1">
                    <input ref={fileInputRef} type="file" accept="image/*" onChange={handleFileChange} className="hidden" />
                    <button onClick={() => fileInputRef.current?.click()} className="flex items-center gap-2 px-4 py-2.5 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-primary,#0F1419)", fontSize: "0.85rem" }}>
                      <Upload size={14} /> Pilih Gambar
                    </button>
                    {coverFile && (
                      <div className="mt-2 flex items-center gap-2">
                        <span style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem" }} className="truncate">{coverFile.name}</span>
                        <button onClick={handleCoverUpload} disabled={coverMutation.isPending} className="flex items-center gap-1 px-3 py-1.5 rounded-lg" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.8rem", fontWeight: 600 }}>
                          {coverMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Save size={12} />}
                          Upload
                        </button>
                      </div>
                    )}
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 6 }}>Maks 2 MB. Gambar lama akan dihapus otomatis.</p>
                  </div>
                </div>
              </div>

              {/* Catatan supplier Digiflazz (read-only). Petunjuk isi target,
                  bukan sumber format — format tetap dari preset + input fields. */}
              {item.supplierNote && (
                <div className="flex gap-2 p-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)", border: "1px solid rgba(0,0,0,0.07)" }}>
                  <Info size={15} className="flex-shrink-0" style={{ color: "var(--color-brand-tertiary,#2C5EF5)", marginTop: 2 }} />
                  <div>
                    <p style={{ color: "var(--color-brand-primary,#0F1419)", fontSize: "0.8rem", fontWeight: 600 }}>Catatan Supplier</p>
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem", whiteSpace: "pre-wrap" }}>{item.supplierNote}</p>
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.72rem", marginTop: 4 }}>
                      Hanya informasi dari Digiflazz. Format tujuan tetap ditentukan preset & input fields di bawah.
                    </p>
                  </div>
                </div>
              )}

              <PresetTemplatePicker
                form={form}
                setForm={setForm}
              />

              {/* Kategori Storefront — checkbox, bukan select: satu game bisa
                  masuk beberapa kategori sekaligus. */}
              <fieldset>
                <legend style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, marginBottom: 8 }}>Kategori Storefront</legend>
                <div className="flex flex-wrap gap-2">
                  {categoryOptions.map((c) => {
                    const checked = selectedCategories.includes(c.slug);
                    return (
                      <label
                        key={c.slug}
                        className="flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer"
                        style={{
                          border: `1px solid ${checked ? "var(--color-brand-tertiary)" : "var(--color-border-input)"}`,
                          backgroundColor: checked ? "var(--color-accent-tint)" : "var(--color-surface)",
                          color: "var(--color-brand-primary)",
                          fontSize: "0.85rem",
                        }}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleCategory(c.slug)}
                          style={{ accentColor: "var(--color-brand-tertiary)" }}
                        />
                        {c.name}
                      </label>
                    );
                  })}
                </div>
                <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem", marginTop: 6 }}>
                  Tab kategori di halaman utama. Boleh pilih lebih dari satu; kosongkan agar game masuk "Lainnya".
                </p>
              </fieldset>

              <DynamicInputFieldsEditor
                form={form}
                setForm={setForm}
              />

              <div>
                <label
                  htmlFor="username-check-product"
                  style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 8 }}
                >
                  Produk Cek Nama Akun
                </label>
                <select
                  id="username-check-product"
                  value={form.usernameCheckProductId ?? ""}
                  onChange={(event) => setForm({
                    ...form,
                    usernameCheckProductId: event.target.value ? Number(event.target.value) : null,
                  })}
                  disabled={productsLoading}
                  className="w-full rounded-xl px-3 py-2.5"
                  style={{ backgroundColor: "var(--color-surface-raised)", border: "1px solid var(--color-border-input)", color: "var(--color-brand-primary)" }}
                >
                  <option value="">Tidak aktif</option>
                  {products.map((product) => (
                    <option key={product.id} value={product.id}>{product.name}</option>
                  ))}
                </select>
                <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.75rem", marginTop: 6 }}>
                  Produk terpilih disembunyikan dari nominal pembelian dan dipakai tombol cek nama akun.
                </p>
              </div>

            </div>
          ) : (
            <ProductMarginList
              products={products}
              brand={item.brand}
              marginMutation={marginMutation}
              loading={productsLoading}
              error={productsError}
              totalCount={totalProducts}
            />
          )}
        </div>

        {/* Footer — selalu tampil supaya frame kedua tab identik */}
        <div className="flex gap-3 px-6 py-4" style={{ borderTop: "1px solid rgba(0,0,0,0.07)" }}>
          {activeTab === "info" && (
            <button onClick={handleSaveMetadata} disabled={updateMutation.isPending} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600 }}>
              {updateMutation.isPending ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
              {updateMutation.isPending ? "Menyimpan..." : "Simpan Metadata"}
            </button>
          )}
          <button onClick={onClose} className={`${activeTab === "info" ? "px-5" : "flex-1"} py-3 rounded-xl`} style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-secondary,#4A5568)" }}>Tutup</button>
        </div>
      </div>
    </div>
  );
}
