import { useState } from "react";
import { Pencil, X, Loader2, Save, DollarSign } from "lucide-react";
import type { AdminProductResponse } from "../../../../api/index";
import { formatRp } from "./utils";

interface ProductMarginListProps {
  products: AdminProductResponse[];
  brand: string;
  marginMutation: {
    mutate: (vars: { id: number; margin: number | null }) => void;
    isPending: boolean;
  };
  loading: boolean;
  error: Error | null;
  totalCount: number;
}

export function ProductMarginList({ products, brand, marginMutation, loading, error, totalCount }: ProductMarginListProps) {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [marginValue, setMarginValue] = useState("");

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
        <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.8rem", marginTop: 4 }}>{error.message}</p>
      </div>
    );
  }

  if (totalCount === 0) {
    return (
      <div className="text-center py-10">
        <DollarSign size={32} style={{ color: "var(--color-secondary,#4A5568)", margin: "0 auto 8px" }} />
        <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>Belum ada produk di database.</p>
        <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.8rem", marginTop: 4 }}>Gunakan tombol "Sync Digiflazz" untuk mengimpor produk.</p>
      </div>
    );
  }

  if (products.length === 0) {
    return (
      <div className="text-center py-10">
        <DollarSign size={32} style={{ color: "var(--color-secondary,#4A5568)", margin: "0 auto 8px" }} />
        <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>Belum ada produk untuk <strong>{brand}</strong></p>
        <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 4 }}>Gunakan tombol "Sync Digiflazz" untuk mengimpor produk.</p>
      </div>
    );
  }

  const startEditMargin = (product: AdminProductResponse) => {
    setEditingId(product.id);
    setMarginValue(product.customMargin?.toString() ?? "");
  };

  const saveMargin = (productId: number) => {
    const margin = marginValue.trim() === "" ? null : Number(marginValue);
    marginMutation.mutate({ id: productId, margin });
    setEditingId(null);
  };

  return (
    <div className="space-y-2">
      <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.8rem", marginBottom: 8 }}>
        {products.length} produk. Atur margin keuntungan untuk setiap nominal.
      </p>
      {products.map((product) => (
        <div key={product.id} className="flex items-center gap-3 px-4 py-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
          <div className="flex-1 min-w-0">
            <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem" }} className="truncate">{product.name}</p>
            <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 2 }}>
              Harga: {formatRp(product.price)}
              {product.customMargin != null && (
                <span> · Margin: <strong style={{ color: "#15803d" }}>{formatRp(product.customMargin)}</strong></span>
              )}
            </p>
          </div>
          {editingId === product.id ? (
            <div className="flex items-center gap-2">
              <input
                value={marginValue}
                onChange={(e) => setMarginValue(e.target.value.replace(/[^0-9]/g, ""))}
                placeholder="0"
                className="w-24 px-3 py-1.5 rounded-lg outline-none text-right"
                style={{ border: "1.5px solid var(--color-tertiary,#2C5EF5)", fontSize: "0.85rem", color: "var(--color-primary,#0F1419)" }}
                autoFocus
              />
              <button onClick={() => saveMargin(product.id)} disabled={marginMutation.isPending} className="px-3 py-1.5 rounded-lg" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.8rem", fontWeight: 600 }}>
                {marginMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Save size={12} />}
              </button>
              <button onClick={() => setEditingId(null)} className="px-2 py-1.5 rounded-lg" style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.8rem" }}>
                <X size={14} />
              </button>
            </div>
          ) : (
            <button onClick={() => startEditMargin(product)} className="flex items-center gap-1 px-3 py-1.5 rounded-lg" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-tertiary,#2C5EF5)", fontSize: "0.8rem" }}>
              <Pencil size={11} /> Margin
            </button>
          )}
        </div>
      ))}
    </div>
  );
}


