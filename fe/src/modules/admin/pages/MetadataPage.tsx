import { useState } from "react";
import { Pencil, Image } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService, brandService } from "../../../services/api";
import type { GameMetadataItem, InputType, UpdateMetadataPayload, AdminProductResponse } from "../../../api/index";
import { getSlugForBrand } from "./metadata/utils";
import type { ModalTab } from "./metadata/utils";
import { SyncButton } from "./metadata/SyncButton";
import { GameEditModal } from "./metadata/GameEditModal";

export default function MetadataPage() {
  const queryClient = useQueryClient();
  const [editTarget, setEditTarget] = useState<GameMetadataItem | null>(null);
  const [activeTab, setActiveTab] = useState<ModalTab>("info");
  const [form, setForm] = useState<{ inputType: InputType; serverList: string }>({ inputType: "USER_ID_ONLY", serverList: "" });
  const [coverFile, setCoverFile] = useState<File | null>(null);
  const [coverPreview, setCoverPreview] = useState<string | null>(null);

  // --- Data Fetching ---

  const { data: items = [], isLoading: loading } = useQuery({
    queryKey: ["admin-metadata"],
    queryFn: adminService.getMetadata,
  });

  const { data: brands = [] } = useQuery({
    queryKey: ["brands"],
    queryFn: () => brandService.getAll(),
  });

  const { data: productsData, isLoading: productsLoading, error: productsError } = useQuery({
    queryKey: ["brand-products", editTarget?.brand],
    queryFn: () => {
      const slug = getSlugForBrand(editTarget!.brand, brands);
      return brandService.getBySlug(slug);
    },
    enabled: !!editTarget && activeTab === "products",
    select: (data) => data?.products ?? [],
  });

  const products = (productsData ?? []) as unknown as AdminProductResponse[];
  const totalProducts = products.length;

  // --- Mutations ---

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateMetadataPayload }) => adminService.updateMetadata(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-metadata"] });
      toast.success("Metadata berhasil diperbarui");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal memperbarui metadata");
    },
  });

  const coverMutation = useMutation({
    mutationFn: ({ id, formData }: { id: number; formData: FormData }) => adminService.uploadGameCover(id, formData),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["admin-metadata"] });
      toast.success("Cover game berhasil diperbarui!");
      if (editTarget && data.url) {
        setEditTarget({ ...editTarget, imageUrl: data.url });
      }
      setCoverFile(null);
      setCoverPreview(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal mengupload cover");
    },
  });

  const marginMutation = useMutation({
    mutationFn: ({ id, margin }: { id: number; margin: number | null }) => adminService.setMargin(id, { margin }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["brand-products"] });
      toast.success("Margin berhasil diatur!");
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal mengatur margin");
    },
  });

  // --- Handlers ---

  const openEdit = (item: GameMetadataItem) => {
    setEditTarget(item);
    setForm({ inputType: item.inputType, serverList: item.serverList ?? "" });
    setActiveTab("info");
    setCoverFile(null);
    setCoverPreview(null);
  };

  const closeModal = () => setEditTarget(null);

  // --- Render ---

  return (
    <AdminLayout title="Metadata Game" breadcrumb={["Admin", "Metadata"]}>
      {/* Sync button */}
      <div className="flex justify-end mb-4">
        <SyncButton />
      </div>

      {/* Game cards grid */}
      {loading ? (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
          {Array.from({ length: 10 }).map((_, i) => (
            <div key={i} className="rounded-2xl h-48 animate-pulse" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
          {items.map((item) => (
            <div key={item.id} className="rounded-2xl overflow-hidden cursor-pointer transition-shadow hover:shadow-md" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }} onClick={() => openEdit(item)}>
              <div style={{ height: 100, overflow: "hidden", position: "relative" }}>
                {item.imageUrl ? (
                  <img src={item.imageUrl} alt={item.brand} className="w-full h-full object-cover" />
                ) : (
                  <div className="w-full h-full flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                    <Image size={24} style={{ color: "var(--color-brand-secondary,#4A5568)" }} />
                  </div>
                )}
                <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(0,0,0,0.5) 0%, transparent 50%)" }} />
                <div className="absolute bottom-2 left-3 right-3 flex items-end justify-between">
                  <span style={{ color: "#fff", fontWeight: 700, fontSize: "0.85rem", textShadow: "0 1px 3px rgba(0,0,0,0.5)" }} className="truncate">{item.brand}</span>
                  <span className="px-1.5 py-0.5 rounded" style={{
                    backgroundColor: item.inputType === "USER_ID_ONLY" ? "#1d4ed8" : "#6d28d9",
                    color: "#fff", fontSize: "0.6rem", fontWeight: 700,
                  }}>
                    {item.inputType === "USER_ID_ONLY" ? "ID" : "ID+Zone"}
                  </span>
                </div>
              </div>
              <div className="p-3 flex items-center justify-between">
                <span style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem" }}>
                  {item.serverList ? `${item.serverList.split(",").length} server` : "No server list"}
                </span>
                <button onClick={(e) => { e.stopPropagation(); openEdit(item); }} className="flex items-center gap-1 px-2 py-1 rounded-lg" style={{ color: "var(--color-brand-tertiary,#2C5EF5)", fontSize: "0.75rem" }}>
                  <Pencil size={11} /> Kelola
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Edit Modal */}
      {editTarget && (
        <GameEditModal
          item={editTarget}
          form={form}
          setForm={setForm}
          activeTab={activeTab}
          setActiveTab={setActiveTab}
          coverFile={coverFile}
          setCoverFile={setCoverFile}
          coverPreview={coverPreview}
          setCoverPreview={setCoverPreview}
          products={products}
          productsLoading={productsLoading}
          productsError={productsError as Error | null}
          totalProducts={totalProducts}
          updateMutation={updateMutation}
          coverMutation={coverMutation}
          marginMutation={marginMutation}
          onClose={closeModal}
        />
      )}
    </AdminLayout>
  );
}
