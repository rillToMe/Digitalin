import { useState, useRef } from "react";
import { Plus, Trash2, ToggleLeft, ToggleRight, Upload, X, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";

export default function BannersPage() {
  const queryClient = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [newFile, setNewFile] = useState<File | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const { data: banners = [], isLoading: loading } = useQuery({
    queryKey: ["admin-banners"],
    queryFn: adminService.getBanners,
  });

  const createMutation = useMutation({
    mutationFn: (formData: FormData) => adminService.createBanner(formData),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      toast.success("Banner ditambahkan");
      setModalOpen(false);
      setNewTitle("");
      setNewFile(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal menambahkan banner");
    },
  });

  const toggleMutation = useMutation({
    mutationFn: (id: number) => adminService.toggleBanner(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      toast.success("Status banner diperbarui");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal mengubah status banner");
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: number) => adminService.deleteBanner(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-banners"] });
      toast.success("Banner dihapus");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal menghapus banner");
    },
  });

  const handleToggle = (id: number) => {
    toggleMutation.mutate(id);
  };

  const handleDelete = (id: number) => {
    deleteMutation.mutate(id);
  };

  const handleCreate = () => {
    if (!newTitle || !newFile) { toast.error("Isi judul dan pilih gambar"); return; }
    const formData = new FormData();
    formData.append("title", newTitle);
    formData.append("file", newFile);
    createMutation.mutate(formData);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0] ?? null;
    setNewFile(file);
  };

  return (
    <AdminLayout title="Banners" breadcrumb={["Admin", "Banners"]}>
      <div className="flex justify-between items-center mb-5">
        <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.9rem" }}>{banners.length} banner</span>
        <button onClick={() => setModalOpen(true)} className="flex items-center gap-2 px-4 py-2.5 rounded-xl" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem" }}>
          <Plus size={16} /> Tambah Banner
        </button>
      </div>

      {loading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3].map((i) => <div key={i} className="rounded-2xl h-48 animate-pulse" style={{ backgroundColor: "var(--color-surface,#fff)" }} />)}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {banners.map((banner) => (
            <div key={banner.id} className="rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
              <div style={{ height: 140, overflow: "hidden", position: "relative" }}>
                <img src={banner.imageUrl} alt={banner.title} className="w-full h-full object-cover" />
                <div className="absolute top-2 right-2">
                  <span className="px-2 py-1 rounded-full" style={{ backgroundColor: banner.isActive ? "#dcfce7" : "#fee2e2", color: banner.isActive ? "#15803d" : "#b91c1c", fontSize: "0.72rem", fontWeight: 600 }}>
                    {banner.isActive ? "Aktif" : "Nonaktif"}
                  </span>
                </div>
              </div>
              <div className="p-4">
                <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.9rem", marginBottom: 3 }} className="truncate">{banner.title}</p>
                <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem" }}>
                  {new Date(banner.createdAt).toLocaleDateString("id-ID")}
                </p>
                <div className="flex items-center gap-2 mt-3">
                  <button
                    onClick={() => handleToggle(banner.id)}
                    disabled={toggleMutation.isPending}
                    className="flex-1 flex items-center justify-center gap-1.5 py-2 rounded-lg transition-colors"
                    style={{ border: "1px solid rgba(0,0,0,0.1)", color: banner.isActive ? "#ef4444" : "#22c55e", fontSize: "0.8rem" }}
                  >
                    {banner.isActive ? <ToggleLeft size={15} /> : <ToggleRight size={15} />}
                    {banner.isActive ? "Nonaktifkan" : "Aktifkan"}
                  </button>
                  <button
                    onClick={() => handleDelete(banner.id)}
                    disabled={deleteMutation.isPending}
                    className="w-9 h-9 rounded-lg flex items-center justify-center"
                    style={{ color: "#ef4444", border: "1px solid rgba(239,68,68,0.2)" }}
                  >
                    <Trash2 size={15} />
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "rgba(0,0,0,0.4)" }}>
          <div className="w-full max-w-md rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface,#fff)" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1rem" }}>Tambah Banner</h3>
              <button onClick={() => setModalOpen(false)} style={{ color: "var(--color-secondary,#4A5568)" }}><X size={20} /></button>
            </div>
            <div className="space-y-3">
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Judul Banner</label>
                <input value={newTitle} onChange={(e) => setNewTitle(e.target.value)} placeholder="Promo Spesial!" className="w-full px-4 py-2.5 rounded-xl outline-none" style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }} />
              </div>
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 5 }}>Gambar Banner</label>
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="image/*"
                  onChange={handleFileChange}
                  className="hidden"
                />
                <button
                  onClick={() => fileInputRef.current?.click()}
                  className="w-full flex items-center justify-center gap-2 px-4 py-3 rounded-xl transition-all"
                  style={{ border: "1.5px dashed rgba(0,0,0,0.2)", color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}
                >
                  <Upload size={16} />
                  {newFile ? newFile.name : "Pilih gambar (max 2MB)"}
                </button>
                {newFile && (
                  <img src={URL.createObjectURL(newFile)} alt="preview" className="w-full rounded-xl object-cover mt-2" style={{ height: 120 }} />
                )}
              </div>
            </div>
            <div className="flex gap-3 mt-5">
              <button onClick={handleCreate} disabled={createMutation.isPending} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600 }}>
                {createMutation.isPending && <Loader2 size={15} className="animate-spin" />}
                {createMutation.isPending ? "Menyimpan..." : "Tambah"}
              </button>
              <button onClick={() => setModalOpen(false)} className="px-5 py-3 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-secondary,#4A5568)" }}>Batal</button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
