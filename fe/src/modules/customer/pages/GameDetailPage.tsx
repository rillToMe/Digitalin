import { useEffect } from "react";
import { useParams, useNavigate } from "react-router";
import { Check, AlertCircle, Loader2, ShieldCheck } from "lucide-react";
import { motion } from "motion/react";
import { toast } from "sonner";
import { useQuery, useMutation } from "@tanstack/react-query";
import { brandService, orderService } from "../../../services/api";
import { useCheckoutStore } from "../../../store/checkoutStore";
import { useAuthStore } from "../../../store/authStore";
import type { ProductItem, CreateOrderPayload } from "../../../api/index";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

export default function GameDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const { isAuthenticated } = useAuthStore();

  const {
    selectedProduct, setSelectedProduct,
    playerId, setPlayerId,
    zoneId, setZoneId,
    setBrand, reset,
  } = useCheckoutStore();

  const { data: brand, isLoading: loading, isError } = useQuery({
    queryKey: ["brand", slug],
    queryFn: () => brandService.getBySlug(slug!),
    enabled: !!slug,
  });

  useEffect(() => {
    reset();
  }, [slug]);

  useEffect(() => {
    if (brand) setBrand(brand);
  }, [brand]);

  const createOrder = useMutation({
    mutationFn: (payload: CreateOrderPayload) => orderService.create(payload),
    onSuccess: (res) => {
      toast.success("Pesanan berhasil dibuat!");
      if (res.redirectUrl) {
        window.location.href = res.redirectUrl;
      } else if (isAuthenticated()) {
        // Logged-in users can see their history
        navigate("/history");
      } else {
        // Guest users go back to home
        navigate("/");
      }
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? "Gagal membuat pesanan. Silakan coba lagi.";
      toast.error(msg);
    },
  });

  if (loading) {
    return (
      <div className="max-w-6xl mx-auto px-4 py-8">
        <div className="animate-pulse space-y-4">
          <div className="rounded-xl h-48" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          <div className="grid md:grid-cols-3 gap-6">
            <div className="md:col-span-2 space-y-4">
              <div className="rounded-xl h-40" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
              <div className="rounded-xl h-64" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
            </div>
            <div className="rounded-xl h-64" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          </div>
        </div>
      </div>
    );
  }

  if (isError || !brand) {
    return (
      <div className="max-w-6xl mx-auto px-4 py-16 text-center">
        <AlertCircle size={40} style={{ color: "var(--color-secondary,#4A5568)", margin: "0 auto 12px" }} />
        <p style={{ color: "var(--color-secondary,#4A5568)" }}>Game tidak ditemukan</p>
      </div>
    );
  }

  const handleNumericInput = (value: string, setter: (v: string) => void) => {
    // Only allow digits
    setter(value.replace(/[^0-9]/g, ""));
  };

  const handleSubmit = () => {
    if (!selectedProduct) { toast.error("Pilih nominal terlebih dahulu"); return; }
    if (!playerId.trim()) { toast.error("Masukkan Player ID terlebih dahulu"); return; }
    if (!/^\d+$/.test(playerId)) { toast.error("Player ID harus berupa angka"); return; }
    if (brand.inputType === "USER_ID_AND_ZONE_ID") {
      if (!zoneId.trim()) { toast.error("Masukkan Zone ID terlebih dahulu"); return; }
      if (!/^\d+$/.test(zoneId)) { toast.error("Zone ID harus berupa angka"); return; }
    }

    createOrder.mutate({
      productId: selectedProduct.id,
      targetId: playerId,
      zoneId: brand.inputType === "USER_ID_AND_ZONE_ID" ? zoneId : null,
      voucherCode: null,
    });
  };

  const total = selectedProduct?.price ?? 0;
  const isSubmitting = createOrder.isPending;

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Banner */}
      <div className="rounded-2xl overflow-hidden mb-8" style={{ height: 200 }}>
        {brand.imageUrl ? (
          <img src={brand.imageUrl} alt={brand.brand} className="w-full h-full object-cover" />
        ) : (
          <div className="w-full h-full flex items-center justify-center" style={{ backgroundColor: "var(--color-primary,#0F1419)" }}>
            <p style={{ color: "#fff", fontWeight: 700, fontSize: "1.5rem" }}>{brand.brand}</p>
          </div>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left - Main content */}
        <div className="lg:col-span-2 space-y-5">
          {/* Game info */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h1 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem" }}>{brand.brand}</h1>
            <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem", marginTop: 4 }}>
              Kode: {brand.gameCode} · Isi saldo dengan mudah dan cepat
            </p>
          </div>

          {/* Player Info */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "1rem", marginBottom: 16 }}>
              1. Informasi Akun
            </h3>
            <div className="space-y-3">
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>
                  Player ID *
                </label>
                <input
                  value={playerId}
                  onChange={(e) => handleNumericInput(e.target.value, setPlayerId)}
                  inputMode="numeric"
                  pattern="[0-9]*"
                  placeholder="Contoh: 123456789"
                  className="w-full px-4 py-3 rounded-xl outline-none transition-all"
                  style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", backgroundColor: "var(--color-surface,#fff)", color: "var(--color-primary,#0F1419)" }}
                  onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                  onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                />
              </div>
              {brand.inputType === "USER_ID_AND_ZONE_ID" && (
                <div>
                  <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>
                    Zone ID *
                  </label>
                  <input
                    value={zoneId}
                    onChange={(e) => handleNumericInput(e.target.value, setZoneId)}
                    inputMode="numeric"
                    pattern="[0-9]*"
                    placeholder="Contoh: 2345"
                    className="w-full px-4 py-3 rounded-xl outline-none transition-all"
                    style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", backgroundColor: "var(--color-surface,#fff)", color: "var(--color-primary,#0F1419)" }}
                    onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                    onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Product Selection */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "1rem", marginBottom: 16 }}>
              2. Pilih Nominal
            </h3>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {brand.products.map((product: ProductItem) => {
                const active = selectedProduct?.id === product.id;
                return (
                  <motion.button
                    key={product.id}
                    whileHover={{ scale: 1.02 }}
                    whileTap={{ scale: 0.98 }}
                    onClick={() => setSelectedProduct(product)}
                    className="relative p-3 rounded-xl text-left transition-all"
                    style={{
                      border: active ? "2px solid var(--color-tertiary,#2C5EF5)" : "1.5px solid rgba(0,0,0,0.1)",
                      backgroundColor: active ? "rgba(44,94,245,0.05)" : "var(--color-surface,#fff)",
                    }}
                  >
                    {active && (
                      <div className="absolute top-2 right-2 w-5 h-5 rounded-full flex items-center justify-center" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)" }}>
                        <Check size={11} color="#fff" />
                      </div>
                    )}
                    <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem" }}>{product.name}</p>
                    <p style={{ color: "var(--color-tertiary,#2C5EF5)", fontSize: "0.8rem", marginTop: 3, fontWeight: 600 }}>{formatRp(product.price)}</p>
                  </motion.button>
                );
              })}
            </div>
          </div>
        </div>

        {/* Right - Summary (sticky on desktop) */}
        <div className="lg:sticky lg:top-24 h-fit">
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "1rem", marginBottom: 16 }}>
              Ringkasan Order
            </h3>
            <div className="space-y-3 mb-5">
              {[
                { label: "Game", value: brand.brand },
                { label: "Player ID", value: playerId || "-" },
                ...(brand.inputType === "USER_ID_AND_ZONE_ID" ? [{ label: "Zone ID", value: zoneId || "-" }] : []),
                { label: "Produk", value: selectedProduct?.name ?? "-" },
                { label: "Harga", value: selectedProduct ? formatRp(selectedProduct.price) : "-" },
              ].map((item, i) => (
                <div key={i} className="flex justify-between items-start">
                  <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>{item.label}</span>
                  <span style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, textAlign: "right", maxWidth: "55%" }}>{item.value}</span>
                </div>
              ))}
            </div>

            <div className="pt-4" style={{ borderTop: "1px solid rgba(0,0,0,0.08)" }}>
              <div className="flex justify-between items-center mb-4">
                <span style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600 }}>Total</span>
                <span style={{ color: "var(--color-tertiary,#2C5EF5)", fontWeight: 700, fontSize: "1.1rem" }}>
                  {total > 0 ? formatRp(total) : "-"}
                </span>
              </div>

              <button
                onClick={handleSubmit}
                disabled={isSubmitting}
                className="w-full py-3.5 rounded-xl flex items-center justify-center gap-2 transition-all"
                style={{
                  backgroundColor: isSubmitting ? "rgba(44,94,245,0.5)" : "var(--color-tertiary,#2C5EF5)",
                  color: "#fff",
                  fontWeight: 600,
                  fontSize: "0.95rem",
                  cursor: isSubmitting ? "not-allowed" : "pointer",
                }}
              >
                {isSubmitting && <Loader2 size={16} className="animate-spin" />}
                {isSubmitting ? "Memproses..." : "Bayar Sekarang"}
              </button>

              <div className="flex items-center gap-2 mt-3 justify-center">
                <ShieldCheck size={13} style={{ color: "var(--color-secondary,#4A5568)" }} />
                <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem" }}>Transaksi aman & terenkripsi</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
