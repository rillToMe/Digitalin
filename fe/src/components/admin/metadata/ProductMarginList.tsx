import { useRef, useState, type ChangeEvent } from "react";
import { Pencil, X, Loader2, Save, DollarSign, Image, Upload } from "lucide-react";
import { toast } from "sonner";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AdminProductResponse } from "../../../api/index";
import { adminService } from "../../../services/api";
import { formatNumberInput, formatRp } from "./utils";

interface ProductMarginListProps {
  products: AdminProductResponse[];
  brand: string;
  marginMutation: {
    mutate: (
      vars: { id: number; margin: number | null },
      options?: {
        onSuccess?: () => void;
      }
    ) => void;
    isPending: boolean;
  };
  loading: boolean;
  error: Error | null;
  totalCount: number;
}

export function ProductMarginList({ products, brand, marginMutation, loading, error, totalCount }: ProductMarginListProps) {
  const queryClient = useQueryClient();
  const [editingId, setEditingId] = useState<number | null>(null);
  const [marginValue, setMarginValue] = useState("");

  // Ikon per item nominal — upload ke R2 via endpoint upload-item-image.
  const iconInputRef = useRef<HTMLInputElement>(null);
  const [iconTargetId, setIconTargetId] = useState<number | null>(null);
  const [iconFile, setIconFile] = useState<File | null>(null);
  const [iconPreview, setIconPreview] = useState<string | null>(null);

  const clearIcon = () => {
    setIconTargetId(null);
    setIconFile(null);
    setIconPreview(null);
  };

  const iconMutation = useMutation({
    mutationFn: ({ id, formData }: { id: number; formData: FormData }) => adminService.uploadItemImage(id, formData),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["admin-products", brand],
      });
      toast.success("Ikon item berhasil diperbarui!");
      clearIcon();
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal mengupload ikon");
    },
  });

  const startEditIcon = (productId: number) => {
    if (iconMutation.isPending) return;
    setIconFile(null);
    setIconPreview(null);
    setIconTargetId(productId);
    iconInputRef.current?.click();
  };

  const handleIconChange = (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file || iconTargetId == null) {
      clearIcon();
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      toast.error("Ukuran file maksimal 2 MB");
      clearIcon();
      return;
    }
    setIconFile(file);
    setIconPreview(URL.createObjectURL(file));
  };

  const uploadIcon = () => {
    if (!iconFile || iconTargetId == null) return;
    const formData = new FormData();
    formData.append("file", iconFile);
    iconMutation.mutate({ id: iconTargetId, formData });
  };

  if (loading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="h-14 rounded-xl animate-pulse" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }} />
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <div className="text-center py-10">
        <p style={{ color: "#ef4444", fontSize: "0.85rem", fontWeight: 600 }}>Gagal memuat produk</p>
        <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem", marginTop: 4 }}>{error.message}</p>
      </div>
    );
  }

  if (totalCount === 0) {
    return (
      <div className="text-center py-10">
        <DollarSign size={32} style={{ color: "var(--color-brand-secondary,#4A5568)", margin: "0 auto 8px" }} />
        <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>Belum ada produk di database.</p>
        <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem", marginTop: 4 }}>Gunakan tombol "Sync Digiflazz" untuk mengimpor produk.</p>
      </div>
    );
  }

  if (products.length === 0) {
    return (
      <div className="text-center py-10">
        <DollarSign size={32} style={{ color: "var(--color-brand-secondary,#4A5568)", margin: "0 auto 8px" }} />
        <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>Belum ada produk untuk <strong>{brand}</strong></p>
        <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 4 }}>Gunakan tombol "Sync Digiflazz" untuk mengimpor produk.</p>
      </div>
    );
  }

  const startEditMargin = (product: AdminProductResponse) => {
    setEditingId(product.id);
    setMarginValue(product.customMargin?.toString() ?? "");
  };

  const saveMargin = (productId: number) => {
    const margin =
      marginValue.trim() === ""
        ? null
        : Number(marginValue);

    marginMutation.mutate(
      {
        id: productId,
        margin,
      },
      {
        onSuccess: () => {
          setEditingId(null);
          setMarginValue("");
        },
      }
    );
  };

  return (
    <div className="space-y-2">
      <input ref={iconInputRef} type="file" accept="image/*" className="hidden" onChange={handleIconChange} />
      <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem", marginBottom: 8 }}>
        {products.length} produk. Atur margin keuntungan & ikon untuk setiap nominal.
      </p>
      {products.map((product) => {
        const iconEditing = iconTargetId === product.id;
        const iconSrc = iconEditing && iconPreview ? iconPreview : product.imageUrl;
        return (
        <div key={product.id} className="flex items-center gap-3 px-4 py-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
          <button
            type="button"
            onClick={() => startEditIcon(product.id)}
            title="Ubah ikon"
            aria-label={`Ubah ikon ${product.name}`}
            className="w-10 h-10 rounded-lg overflow-hidden flex items-center justify-center shrink-0"
            style={{ border: "1px solid rgba(0,0,0,0.1)", backgroundColor: "var(--color-surface,#fff)" }}
          >
            {iconSrc ? (
              <img src={iconSrc} alt="" className="w-full h-full object-contain" />
            ) : (
              <Image size={16} style={{ color: "var(--color-brand-secondary,#4A5568)" }} />
            )}
          </button>
          <div className="flex-1 min-w-0">
            <p style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem" }} className="truncate">{product.name}</p>
            <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 2 }}>
              Harga: {formatRp(product.price)}
              {product.customMargin != null && (
                <span> · Margin: <strong style={{ color: "#15803d" }}>{formatRp(product.customMargin)}</strong></span>
              )}
            </p>
          </div>
          {iconEditing && iconFile ? (
            <div className="flex items-center gap-2 shrink-0">
              <button onClick={uploadIcon} disabled={iconMutation.isPending} className="px-3 py-1.5 rounded-lg" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.8rem", fontWeight: 600 }}>
                {iconMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Upload size={12} />}
              </button>
              <button onClick={clearIcon} disabled={iconMutation.isPending} className="px-2 py-1.5 rounded-lg" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem" }}>
                <X size={14} />
              </button>
            </div>
          ) : editingId === product.id ? (
            <div className="flex items-center gap-2">
              <div
                className="flex w-40 shrink-0 items-center rounded-lg overflow-hidden"
                style={{
                  border: "1.5px solid var(--color-brand-tertiary,#2C5EF5)",
                  backgroundColor: "var(--color-surface,#fff)",
                }}
              >
                <span
                  className="shrink-0 pl-3"
                  style={{
                    color: "var(--color-brand-secondary,#4A5568)",
                    fontSize: "0.85rem",
                  }}
                >
                  Rp
                </span>
                <input
                  value={formatNumberInput(marginValue)}
                  onChange={(e) => setMarginValue(e.target.value.replace(/\D/g, ""))}
                  inputMode="numeric"
                  placeholder="0"
                  className="min-w-0 flex-1 px-2 py-1.5 outline-none text-right"
                  style={{
                    backgroundColor: "transparent",
                    border: 0,
                    borderRadius: 0,
                    boxShadow: "none",
                    fontSize: "0.85rem",
                    color: "var(--color-brand-primary,#0F1419)",
                    outline: "none",
                  }}
                  autoFocus
                />
              </div>
              <button onClick={() => saveMargin(product.id)} disabled={marginMutation.isPending} className="px-3 py-1.5 rounded-lg" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.8rem", fontWeight: 600 }}>
                {marginMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Save size={12} />}
              </button>
              <button onClick={() => setEditingId(null)} className="px-2 py-1.5 rounded-lg" style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem" }}>
                <X size={14} />
              </button>
            </div>
          ) : (
            <button onClick={() => startEditMargin(product)} className="flex items-center gap-1 px-3 py-1.5 rounded-lg" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-tertiary,#2C5EF5)", fontSize: "0.8rem" }}>
              <Pencil size={11} /> Margin
            </button>
          )}
        </div>
        );
      })}
    </div>
  );
}


