import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import { Check, AlertCircle, Loader2, ShieldCheck, CheckCircle2 } from "lucide-react";
import { motion } from "motion/react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { brandService, orderService } from "../../../services/api";
import { useCheckoutStore } from "../../../store/checkoutStore";
import type { ProductItem, CreateOrderPayload, CheckoutPreviewResponse } from "../../../api/index";
import VoucherCard from "../../../components/common/VoucherCard";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogClose,
} from "../../../app/components/ui/dialog";
import { Button } from "../../../app/components/ui/button";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

export default function GameDetailPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [isPaying, setIsPaying] = useState(false);
  const [showSuccessDialog, setShowSuccessDialog] = useState(false);
  const [successMessage, setSuccessMessage] = useState<{ title: string; description: string } | null>(null);
  const [discountInfo, setDiscountInfo] = useState<{ discountAmount: number; finalPrice: number; price: number } | null>(null);
  const [previewResult, setPreviewResult] = useState<CheckoutPreviewResponse | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);

  const {
    selectedProduct, setSelectedProduct,
    playerId, setPlayerId,
    zoneId, setZoneId,
    setBrand, reset,
    voucherCode, setVoucherCode,
  } = useCheckoutStore();

  const { data: brand, isLoading: loading, isError } = useQuery({
    queryKey: ["brand", slug],
    queryFn: () => brandService.getBySlug(slug!),
    enabled: !!slug,
  });

  useEffect(() => {
    reset();
  }, [slug]);

  // Reset voucher and preview when product or player data changes
  useEffect(() => {
    setVoucherCode(null);
    setPreviewResult(null);
    setPreviewError(null);
  }, [selectedProduct?.id, playerId, zoneId]);

  // Voucher preview mutation
  const previewMutation = useMutation({
    mutationFn: (payload: { productId: number; voucherCode: string }) =>
      orderService.preview(payload),
    onSuccess: (res) => {
      if (res.isVoucherApplied) {
        setPreviewResult(res);
        setPreviewError(null);
        setVoucherCode(res.voucherCode);
      } else {
        setPreviewResult(null);
        setVoucherCode(null);
      }
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? "Gagal memvalidasi voucher.";
      setPreviewError(msg);
      setPreviewResult(null);
      setVoucherCode(null);
      toast.error(msg);
    },
  });

  const handleVoucherApply = (code: string) => {
    if (!selectedProduct) {
      toast.error("Pilih nominal terlebih dahulu");
      return;
    }
    setPreviewError(null);
    previewMutation.mutate({ productId: selectedProduct.id, voucherCode: code });
  };

  const handleVoucherRemove = () => {
    setVoucherCode(null);
    setPreviewResult(null);
    setPreviewError(null);
  };

  useEffect(() => {
    if (brand) setBrand(brand);
  }, [brand]);

  const createOrder = useMutation({
    mutationFn: (payload: CreateOrderPayload) => orderService.create(payload),
    onSuccess: (res) => {
      if (res.snapToken && window.snap) {
        // Open Midtrans Snap popup
        setIsPaying(true);
        window.snap.pay(res.snapToken, {
          onSuccess: () => {
            setIsPaying(false);
            queryClient.invalidateQueries({ queryKey: ["orders"] });
            queryClient.invalidateQueries({ queryKey: ["brand", slug] });
            setDiscountInfo(res.discountAmount > 0 ? { discountAmount: res.discountAmount, finalPrice: res.finalPrice, price: res.price } : null);
            setSuccessMessage({
              title: "Pembayaran Berhasil",
              description:
                "Pesanan Anda sedang diproses. Anda dapat melihat status transaksi pada halaman Riwayat Order kapan saja.",
            });
            setShowSuccessDialog(true);
            // Reset checkout form so user can make another purchase immediately
            setSelectedProduct(null as unknown as ProductItem);
            setVoucherCode(null);
          },
          onPending: () => {
            setIsPaying(false);
            queryClient.invalidateQueries({ queryKey: ["orders"] });
            setDiscountInfo(res.discountAmount > 0 ? { discountAmount: res.discountAmount, finalPrice: res.finalPrice, price: res.price } : null);
            setSuccessMessage({
              title: "Pembayaran Sedang Diproses",
              description:
                "Pembayaran Anda sedang diproses. Silakan selesaikan pembayaran Anda. Status transaksi dapat dilihat di halaman Riwayat Order.",
            });
            setShowSuccessDialog(true);
          },
          onError: () => {
            setIsPaying(false);
            toast.error("Pembayaran gagal. Silakan coba lagi.");
          },
          onClose: () => {
            setIsPaying(false);
            toast.warning("Pembayaran belum selesai. Anda dapat melanjutkan pembayaran melalui riwayat order.");
          },
        });
      } else if (res.snapToken) {
        // snapToken exists but Midtrans script failed to load
        setIsPaying(false);
        toast.error("Gagal memuat sistem pembayaran. Silakan refresh halaman dan coba lagi.");
      } else {
        // Fallback: no snapToken (should not happen if backend is configured correctly)
        setIsPaying(false);
        queryClient.invalidateQueries({ queryKey: ["orders"] });
        setDiscountInfo(res.discountAmount > 0 ? { discountAmount: res.discountAmount, finalPrice: res.finalPrice, price: res.price } : null);
        setSuccessMessage({
          title: "Pesanan Berhasil Dibuat",
          description:
            "Pesanan Anda sedang diproses. Anda dapat melihat status transaksi pada halaman Riwayat Order kapan saja.",
        });
        setShowSuccessDialog(true);
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
      voucherCode: voucherCode,
    });
  };

  const total = previewResult ? previewResult.finalPrice : (selectedProduct?.price ?? 0);
  const originalPrice = previewResult?.originalPrice ?? selectedProduct?.price ?? 0;
  const discountAmount = previewResult?.discountAmount ?? 0;
  const isSubmitting = createOrder.isPending || isPaying;

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Banner */}
      

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left - Main content */}
        <div className="lg:col-span-2 space-y-5">
          {/* Game info */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <div className="rounded-2xl overflow-hidden mb-8 w-48 h-48 sm:w-56 sm:h-56">
              {brand.imageUrl ? (
                <img src={brand.imageUrl} alt={brand.brand} className="w-full h-full object-cover" />
              ) : (
                <div className="w-full h-full flex items-center justify-center" style={{ backgroundColor: "var(--color-primary,#0F1419)" }}>
                  <p style={{ color: "#fff", fontWeight: 700, fontSize: "1.5rem" }}>{brand.brand}</p>
                </div>
              )}
            </div>
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
              ].map((item, i) => (
                <div key={i} className="flex justify-between items-start">
                  <span style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>{item.label}</span>
                  <span style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, textAlign: "right", maxWidth: "55%" }}>{item.value}</span>
                </div>
              ))}
            </div>

            {/* Voucher section inside summary */}
            <div className="mb-4">
              <VoucherCard
                appliedCode={voucherCode}
                onApply={handleVoucherApply}
                onRemove={handleVoucherRemove}
                compact
                isLoading={previewMutation.isPending}
                error={previewError}
              />
            </div>

            <div className="pt-4" style={{ borderTop: "1px solid rgba(0,0,0,0.08)" }}>
              {/* Price breakdown */}
              <div className="space-y-2 mb-4">
                <div className="flex justify-between items-center">
                  <span style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>Harga</span>
                  <span style={{ color: "var(--color-primary)", fontSize: "0.85rem", fontWeight: 500 }}>
                    {originalPrice > 0 ? formatRp(originalPrice) : "-"}
                  </span>
                </div>
                {discountAmount > 0 && (
                  <>
                    <div className="flex justify-between items-center">
                      <span style={{ color: "var(--color-success)", fontSize: "0.85rem" }}>Diskon</span>
                      <span style={{ color: "var(--color-success)", fontSize: "0.85rem", fontWeight: 600 }}>
                        -{formatRp(discountAmount)}
                      </span>
                    </div>
                    {previewResult?.voucherCode && (
                      <div className="flex justify-between items-center">
                        <span style={{ color: "var(--color-secondary)", fontSize: "0.85rem" }}>Voucher</span>
                        <span style={{ fontFamily: "monospace", color: "var(--color-success)", fontSize: "0.8rem", fontWeight: 600 }}>
                          {previewResult.voucherCode}
                        </span>
                      </div>
                    )}
                  </>
                )}
              </div>

              <div className="flex justify-between items-center mb-4" style={{ borderTop: "1px solid var(--color-border-lighter)", paddingTop: 12 }}>
                <span style={{ color: "var(--color-primary)", fontWeight: 600 }}>Total</span>
                <span style={{ color: "var(--color-tertiary)", fontWeight: 700, fontSize: "1.1rem" }}>
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

      {/* Payment Success Dialog */}
      <Dialog open={showSuccessDialog} onOpenChange={setShowSuccessDialog}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <div className="flex justify-center mb-2">
              <div
                className="w-14 h-14 rounded-full flex items-center justify-center"
                style={{ backgroundColor: "rgba(34,197,94,0.12)" }}
              >
                <CheckCircle2 size={32} style={{ color: "#22c55e" }} />
              </div>
            </div>
            <DialogTitle className="text-center" style={{ color: "var(--color-primary,#0F1419)" }}>
              {successMessage?.title ?? "Pembayaran Berhasil"}
            </DialogTitle>
            <DialogDescription className="text-center" style={{ color: "var(--color-secondary,#4A5568)" }}>
              {successMessage?.description ??
                "Pesanan Anda sedang diproses. Anda dapat melihat status transaksi pada halaman Riwayat Order kapan saja."}
            </DialogDescription>
          </DialogHeader>

          {/* Discount breakdown */}
          {discountInfo && (
            <div
              className="rounded-xl px-4 py-3 mx-1 mb-2 space-y-2"
              style={{ backgroundColor: "var(--color-success-bg)" }}
            >
              <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                <span style={{ color: "var(--color-secondary)" }}>Harga</span>
                <span style={{ color: "var(--color-primary)", fontWeight: 500 }}>{formatRp(discountInfo.price)}</span>
              </div>
              <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                <span style={{ color: "var(--color-success)" }}>Diskon Voucher</span>
                <span style={{ color: "var(--color-success)", fontWeight: 600 }}>-{formatRp(discountInfo.discountAmount)}</span>
              </div>
              <div className="flex justify-between pt-1" style={{ fontSize: "0.9rem", borderTop: "1px solid var(--color-border-lighter)" }}>
                <span style={{ color: "var(--color-primary)", fontWeight: 600 }}>Total Dibayar</span>
                <span style={{ color: "var(--color-tertiary)", fontWeight: 700 }}>{formatRp(discountInfo.finalPrice)}</span>
              </div>
            </div>
          )}

          <DialogFooter className="flex flex-col sm:flex-row gap-2 sm:gap-0">
            <DialogClose asChild>
              <Button variant="outline" className="w-full sm:flex-1">
                Tutup
              </Button>
            </DialogClose>
            <Button
              className="w-full sm:flex-1"
              onClick={() => {
                setShowSuccessDialog(false);
                navigate("/history");
              }}
            >
              Lihat Riwayat
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
