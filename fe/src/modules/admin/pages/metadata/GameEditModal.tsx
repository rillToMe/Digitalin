import { useRef } from "react";
import { X, Loader2, Save, Upload, Image } from "lucide-react";
import { toast } from "sonner";
import type { InputType, GameEditModalProps, ModalTab } from "../../../../api/index";
import { ProductMarginList } from "./ProductMarginList";

export function GameEditModal({
  item, form, setForm, activeTab, setActiveTab,
  coverFile, setCoverFile, coverPreview, setCoverPreview,
  products, productsLoading, productsError, totalProducts,
  updateMutation, coverMutation, marginMutation, onClose,
}: GameEditModalProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);

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
    updateMutation.mutate({
      id: item.id,
      payload: { inputType: form.inputType, serverList: form.serverList || null },
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "rgba(0,0,0,0.4)" }}>
      <div className="w-full max-w-2xl rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface,#fff)", maxHeight: "90vh", display: "flex", flexDirection: "column" }}>
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4" style={{ borderBottom: "1px solid rgba(0,0,0,0.07)" }}>
          <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1rem" }}>Kelola: {item.brand}</h3>
          <button onClick={onClose} style={{ color: "var(--color-secondary,#4A5568)" }}><X size={20} /></button>
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
                backgroundColor: activeTab === tab.key ? "var(--color-tertiary,#2C5EF5)" : "transparent",
                color: activeTab === tab.key ? "#fff" : "var(--color-secondary,#4A5568)",
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
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 8 }}>Cover Game</label>
                <div className="flex gap-4 items-start">
                  <div className="w-32 h-20 rounded-xl overflow-hidden flex-shrink-0" style={{ border: "1px solid rgba(0,0,0,0.1)" }}>
                    {(coverPreview || item.imageUrl) ? (
                      <img src={coverPreview || item.imageUrl!} alt="cover" className="w-full h-full object-cover" />
                    ) : (
                      <div className="w-full h-full flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                        <Image size={20} style={{ color: "var(--color-secondary,#4A5568)" }} />
                      </div>
                    )}
                  </div>
                  <div className="flex-1">
                    <input ref={fileInputRef} type="file" accept="image/*" onChange={handleFileChange} className="hidden" />
                    <button onClick={() => fileInputRef.current?.click()} className="flex items-center gap-2 px-4 py-2.5 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-primary,#0F1419)", fontSize: "0.85rem" }}>
                      <Upload size={14} /> Pilih Gambar
                    </button>
                    {coverFile && (
                      <div className="mt-2 flex items-center gap-2">
                        <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.8rem" }} className="truncate">{coverFile.name}</span>
                        <button onClick={handleCoverUpload} disabled={coverMutation.isPending} className="flex items-center gap-1 px-3 py-1.5 rounded-lg" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.8rem", fontWeight: 600 }}>
                          {coverMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Save size={12} />}
                          Upload
                        </button>
                      </div>
                    )}
                    <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 6 }}>Maks 2 MB. Gambar lama akan dihapus otomatis.</p>
                  </div>
                </div>
              </div>

              {/* Input Type */}
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Input Type</label>
                <div className="flex gap-3">
                  {(["USER_ID_ONLY", "USER_ID_AND_ZONE_ID"] as InputType[]).map((t) => (
                    <button
                      key={t}
                      onClick={() => setForm({ ...form, inputType: t })}
                      className="flex-1 py-2.5 rounded-xl transition-all"
                      style={{
                        border: form.inputType === t ? "2px solid var(--color-tertiary,#2C5EF5)" : "1.5px solid rgba(0,0,0,0.1)",
                        backgroundColor: form.inputType === t ? "rgba(44,94,245,0.06)" : "transparent",
                        color: form.inputType === t ? "var(--color-tertiary,#2C5EF5)" : "var(--color-secondary,#4A5568)",
                        fontSize: "0.8rem", fontWeight: form.inputType === t ? 600 : 400,
                      }}
                    >
                      {t === "USER_ID_ONLY" ? "User ID Only" : "User ID + Zone ID"}
                    </button>
                  ))}
                </div>
              </div>

              {/* Server List */}
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>
                  Server List <span style={{ color: "var(--color-secondary,#4A5568)", fontWeight: 400 }}>(pisahkan dengan koma)</span>
                </label>
                <input
                  value={form.serverList}
                  onChange={(e) => setForm({ ...form, serverList: e.target.value })}
                  placeholder="Asia,Europe,America"
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                />
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

        {/* Footer */}
        {activeTab === "info" && (
          <div className="flex gap-3 px-6 py-4" style={{ borderTop: "1px solid rgba(0,0,0,0.07)" }}>
            <button onClick={handleSaveMetadata} disabled={updateMutation.isPending} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600 }}>
              {updateMutation.isPending ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
              {updateMutation.isPending ? "Menyimpan..." : "Simpan Metadata"}
            </button>
            <button onClick={onClose} className="px-5 py-3 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-secondary,#4A5568)" }}>Tutup</button>
          </div>
        )}
      </div>
    </div>
  );
}