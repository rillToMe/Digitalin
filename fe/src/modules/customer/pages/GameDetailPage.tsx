import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import { Check, AlertCircle, Loader2, ShieldCheck, CheckCircle2, Gamepad2 } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { brandService, orderService } from "../../../services/api";
import { paySnap, preloadSnap, SnapError } from "../../../services/midtransSnap";
import { useCheckoutStore } from "../../../store/checkoutStore";
import type { CreateOrderPayload, CheckoutPreviewResponse } from "../../../api/index";
import VoucherCard from "../../../components/common/VoucherCard";
import BackButton from "../../../components/common/BackButton";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogClose,
} from "../../../components/common/dialog";
import { Button } from "../../../components/common/button";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

/**
 * Judul langkah dengan penanda mono ("01", "02") — urutan checkout dibaca
 * sebagai langkah, bukan sebagai dua kartu yang kebetulan bertumpuk.
 */
function StepHeading({ step, children }: { step: string; children: React.ReactNode }) {
  return (
    <h3
      className="flex items-center gap-2.5"
      style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.05rem", marginBottom: 16 }}
    >
      <span
        className="hud-checkout-control label-mono flex h-7 w-7 items-center justify-center border"
        style={{
          backgroundColor: "var(--color-accent-tint)",
          color: "var(--color-brand-tertiary)",
          borderColor: "var(--color-brand-tertiary)",
        }}
      >
        {step}
      </span>
      {children}
    </h3>
  );
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
    selectedProduct,
    setSelectedProduct,
    targetValues,
    setTargetValue,
    setBrand,
    reset,
    voucherCode,
    setVoucherCode,
  } = useCheckoutStore();

  const { data: brand, isLoading: loading, isError } = useQuery({
    queryKey: ["brand", slug],
    queryFn: () => brandService.getBySlug(slug!),
    enabled: !!slug,
  });

  useEffect(() => {
    reset();
  }, [slug]);

  /* Halaman ini tujuannya memang bayar, jadi snap.js ditarik sekarang — kalau
     baru dimulai setelah order dibuat, popup-nya menunggu ~726 kB dulu. */
  useEffect(() => {
    preloadSnap();
  }, []);

  // Reset voucher and preview when product or player data changes
  useEffect(() => {
    setVoucherCode(null);
    setPreviewResult(null);
    setPreviewError(null);
  }, [selectedProduct?.id, targetValues, setVoucherCode]);

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
      if (res.snapToken) {
        // Popup Snap — script-nya sudah dipramuat saat halaman dibuka.
        setIsPaying(true);
        void paySnap(res.snapToken, {
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
            setSelectedProduct(null);
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
        }).catch((err: unknown) => {
          setIsPaying(false);
          toast.error(
            err instanceof SnapError
              ? err.message
              : "Gagal memuat sistem pembayaran. Silakan refresh halaman dan coba lagi.",
          );
        });
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
          <div className="rounded-2xl h-32" style={{ backgroundColor: "var(--color-skeleton)" }} />
          <div className="grid md:grid-cols-3 gap-6">
            <div className="md:col-span-2 space-y-4">
              <div className="rounded-2xl h-40" style={{ backgroundColor: "var(--color-skeleton)" }} />
              <div className="rounded-2xl h-64" style={{ backgroundColor: "var(--color-skeleton)" }} />
            </div>
            <div className="rounded-2xl h-64" style={{ backgroundColor: "var(--color-skeleton)" }} />
          </div>
        </div>
      </div>
    );
  }

  if (isError || !brand) {
    return (
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <BackButton />
        <div className="py-16 text-center">
          <AlertCircle size={40} style={{ color: "var(--color-brand-secondary)", margin: "0 auto 12px" }} />
          <p style={{ color: "var(--color-brand-secondary)" }}>Game tidak ditemukan</p>
        </div>
      </div>
    );
  }

  const handleSubmit = () => {
    if (!selectedProduct) {
      toast.error("Pilih nominal terlebih dahulu");
      return;
    }

    const orderedFields = [...brand.inputFields].sort(
      (a, b) => a.order - b.order
    );

    for (const field of orderedFields) {
      const value = targetValues[field.key]?.trim() ?? "";

      if (field.required && !value) {
        toast.error(`${field.label} wajib diisi`);
        return;
      }

      if (
        field.type === "select" &&
        value &&
        !field.options.some(
          (option) =>
            option.toLowerCase() === value.toLowerCase()
        )
      ) {
        toast.error(`Pilihan ${field.label} tidak valid`);
        return;
      }
    }

    createOrder.mutate({
      productId: selectedProduct.id,
      targetValues,
      voucherCode,
    });
  };

  const total = previewResult ? previewResult.finalPrice : (selectedProduct?.price ?? 0);
  const originalPrice = previewResult?.originalPrice ?? selectedProduct?.price ?? 0;
  const discountAmount = previewResult?.discountAmount ?? 0;
  const isSubmitting = createOrder.isPending || isPaying;

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <BackButton />

      {/* Header game: artwork-nya sendiri jadi latar (di-blur + scrim solid)
          dengan poster potret di depannya. Halaman ini jadi terbaca sebagai
          halaman game, bukan formulir — dan warna jenuhnya tetap hanya datang
          dari artwork. Scrim selalu dipasang, bukan hanya saat ada gambar:
          teksnya putih di kedua tema, jadi latarnya harus tetap gelap. */}
      <div
        className="relative mb-5 overflow-hidden rounded-2xl"
        style={{ boxShadow: "var(--shadow-card)" }}
      >
        {brand.imageUrl && (
          <img
            src={brand.imageUrl}
            alt=""
            aria-hidden="true"
            className="absolute inset-0 h-full w-full scale-110 object-cover blur-xl"
          />
        )}
        <div className="absolute inset-0" style={{ backgroundColor: "var(--color-poster-scrim)" }} />

        <div className="relative flex items-end gap-4 p-4 sm:gap-5 sm:p-5">
          <div
            className="aspect-[3/4] w-20 shrink-0 overflow-hidden rounded-xl sm:w-24"
            style={{
              backgroundColor: "var(--color-surface-raised)",
              boxShadow: "var(--shadow-raised)",
            }}
          >
            {brand.imageUrl ? (
              <img src={brand.imageUrl} alt={brand.brand} className="h-full w-full object-cover" />
            ) : (
              <div className="flex h-full w-full items-center justify-center">
                <Gamepad2 size={28} style={{ color: "var(--color-brand-secondary)" }} aria-hidden="true" />
              </div>
            )}
          </div>

          <div className="min-w-0 pb-1">
            <p className="label-mono" style={{ color: "var(--color-on-media)" }}>
              Top up instan
            </p>
            <h1
              className="truncate"
              style={{ color: "var(--color-on-primary)", fontWeight: 700, fontSize: "1.5rem", lineHeight: 1.2 }}
            >
              {brand.brand}
            </h1>
            <p className="label-mono mt-1.5" style={{ color: "var(--color-on-media-muted)" }}>
              {brand.gameCode}
            </p>
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left - Main content */}
        <div className="lg:col-span-2 space-y-5">
          {/* Player Info */}
          <div className="hud-checkout-panel p-5" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-default)" }}>
            <StepHeading step="01">Informasi Akun</StepHeading>
            <div className="space-y-3">
              {[...brand.inputFields]
                .sort((a, b) => a.order - b.order)
                .map((field) => (
                  <div key={field.key}>
                    <label
                      style={{
                        color: "var(--color-brand-primary)",
                        fontSize: "0.85rem",
                        fontWeight: 500,
                        display: "block",
                        marginBottom: 6,
                      }}
                    >
                      {field.label}
                      {field.required ? " *" : ""}
                    </label>

                    {field.type === "select" ? (
                      <select
                        value={targetValues[field.key] ?? ""}
                        onChange={(event) =>
                          setTargetValue(field.key, event.target.value)
                        }
                        className="hud-checkout-field w-full rounded-[3px] px-4 py-3 outline-none transition-colors"
                        style={{
                          border: "1.5px solid var(--color-border-input)",
                          fontSize: "0.9rem",
                          backgroundColor: "var(--color-surface-raised)",
                          color: "var(--color-brand-primary)",
                        }}
                      >
                        <option value="">
                          {field.placeholder || `Pilih ${field.label}`}
                        </option>

                        {field.options.map((option) => (
                          <option key={option} value={option}>
                            {option}
                          </option>
                        ))}
                      </select>
                    ) : (
                      <input
                        type="text"
                        inputMode={field.type === "number" ? "numeric" : undefined}
                        pattern={field.type === "number" ? "[0-9]*" : undefined}
                        value={targetValues[field.key] ?? ""}
                        onChange={(event) => {
                          const value =
                            field.type === "number"
                              ? event.target.value.replace(/\D/g, "")
                              : event.target.value;

                          setTargetValue(field.key, value);
                        }}
                        placeholder={field.placeholder}
                        className="hud-checkout-field w-full rounded-[3px] px-4 py-3 outline-none transition-colors"
                        style={{
                          border: "1.5px solid var(--color-border-input)",
                          fontSize: "0.9rem",
                          backgroundColor: "var(--color-surface-raised)",
                          color: "var(--color-brand-primary)",
                        }}
                      />
                    )}
                  </div>
                ))}
            </div>
          </div>

          {/* Product Selection */}
          <div className="hud-checkout-panel p-5" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-default)" }}>
            <StepHeading step="02">Pilih Nominal</StepHeading>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {brand.products.map((product) => {
                const active = selectedProduct?.id === product.id;
                return (
                  <button
                    key={product.id}
                    type="button"
                    onClick={() => setSelectedProduct(product)}
                    aria-pressed={active}
                    className="hud-checkout-control hud-product-option relative p-3 text-left transition-colors active:bg-(--color-accent-tint-strong) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
                    style={{
                      border: active
                        ? "1.5px solid var(--color-brand-tertiary)"
                        : "1.5px solid var(--color-border-default)",
                      backgroundColor: active ? "var(--color-accent-tint)" : "var(--color-surface)",
                    }}
                  >
                    {active && (
                      <div className="hud-checkout-control absolute right-2 top-2 flex h-5 w-5 items-center justify-center" style={{ backgroundColor: "var(--color-brand-tertiary)" }}>
                        <Check size={11} style={{ color: "var(--color-on-primary)" }} aria-hidden="true" />
                      </div>
                    )}
                    <p className="pr-6" style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.85rem" }}>{product.name}</p>
                    {/* Kobalt hanya untuk nominal yang dipilih — kalau semua harga
                        biru, aksennya berhenti berarti (design.md: satu aksi). */}
                    <p
                      className="tabular-price"
                      style={{
                        color: active ? "var(--color-brand-tertiary)" : "var(--color-brand-secondary)",
                        fontSize: "0.8rem",
                        marginTop: 3,
                        fontWeight: 600,
                      }}
                    >
                      {formatRp(product.price)}
                    </p>
                  </button>
                );
              })}
            </div>
          </div>
        </div>

        {/* Right - Summary (sticky on desktop) */}
        <div className="lg:sticky lg:top-24 h-fit">
          <div className="hud-checkout-panel p-5" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-default)" }}>
            <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.05rem", marginBottom: 16 }}>
              Ringkasan Order
            </h3>
            <div className="space-y-3 mb-5">
              {[
                { label: "Game", value: brand.brand },
                ...[...brand.inputFields]
                  .sort((a, b) => a.order - b.order)
                  .map((field) => ({
                    label: field.label,
                    value: targetValues[field.key]?.trim() || "-",
                  })),
                {
                  label: "Produk",
                  value: selectedProduct?.name ?? "-",
                },
              ].map((item, i) => (
                <div key={i} className="flex justify-between items-start gap-3">
                  <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>{item.label}</span>
                  <span
                    style={{
                      color: item.value === "-" ? "var(--color-brand-secondary)" : "var(--color-brand-primary)",
                      fontSize: "0.85rem",
                      fontWeight: item.value === "-" ? 400 : 500,
                      fontStyle: item.value === "-" ? "italic" : "normal",
                      textAlign: "right",
                      maxWidth: "55%",
                    }}
                  >
                    {item.value}
                  </span>
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

            <div className="pt-4" style={{ borderTop: "1px solid var(--color-border-default)" }}>
              {/* Price breakdown */}
              <div className="space-y-2 mb-4">
                <div className="flex justify-between items-center">
                  <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>Harga</span>
                  <span className="tabular-price" style={{ color: "var(--color-brand-primary)", fontSize: "0.85rem", fontWeight: 500 }}>
                    {originalPrice > 0 ? formatRp(originalPrice) : "-"}
                  </span>
                </div>
                {discountAmount > 0 && (
                  <>
                    <div className="flex justify-between items-center">
                      <span style={{ color: "var(--color-success)", fontSize: "0.85rem" }}>Diskon</span>
                      <span className="tabular-price" style={{ color: "var(--color-success)", fontSize: "0.85rem", fontWeight: 600 }}>
                        -{formatRp(discountAmount)}
                      </span>
                    </div>
                    {previewResult?.voucherCode && (
                      <div className="flex justify-between items-center">
                        <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>Voucher</span>
                        <span className="label-mono" style={{ color: "var(--color-success)" }}>
                          {previewResult.voucherCode}
                        </span>
                      </div>
                    )}
                  </>
                )}
              </div>

              <div className="flex justify-between items-center mb-4" style={{ borderTop: "1px solid var(--color-border-default)", paddingTop: 12 }}>
                <span style={{ color: "var(--color-brand-primary)", fontWeight: 600 }}>Total</span>
                <span className="tabular-price" style={{ color: "var(--color-brand-tertiary)", fontWeight: 700, fontSize: "1.15rem" }}>
                  {total > 0 ? formatRp(total) : "-"}
                </span>
              </div>

              <button
                onClick={handleSubmit}
                disabled={isSubmitting}
                className="hud-checkout-control flex w-full items-center justify-center gap-2 py-3.5 uppercase tracking-[0.12em] transition-colors hover:bg-(--color-brand-tertiary-hover) disabled:opacity-60 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
                style={{
                  backgroundColor: "var(--color-brand-tertiary)",
                  color: "var(--color-on-primary)",
                  fontWeight: 700,
                  fontSize: "0.82rem",
                }}
              >
                {isSubmitting && <Loader2 size={16} className="animate-spin" aria-hidden="true" />}
                {isSubmitting ? "Memproses..." : "Bayar Sekarang"}
              </button>

              <div className="mt-3 flex items-center justify-center gap-1.5">
                <ShieldCheck size={12} strokeWidth={1.5} style={{ color: "var(--color-brand-secondary)" }} aria-hidden="true" />
                <span className="label-mono" style={{ color: "var(--color-brand-secondary)", fontSize: "0.62rem" }}>Transaksi aman &amp; terenkripsi</span>
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
                style={{ backgroundColor: "var(--color-success-bg)" }}
              >
                <CheckCircle2 size={32} style={{ color: "var(--color-success)" }} aria-hidden="true" />
              </div>
            </div>
            <DialogTitle className="text-center" style={{ color: "var(--color-brand-primary)" }}>
              {successMessage?.title ?? "Pembayaran Berhasil"}
            </DialogTitle>
            <DialogDescription className="text-center" style={{ color: "var(--color-brand-secondary)" }}>
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
                <span style={{ color: "var(--color-brand-secondary)" }}>Harga</span>
                <span className="tabular-price" style={{ color: "var(--color-brand-primary)", fontWeight: 500 }}>{formatRp(discountInfo.price)}</span>
              </div>
              <div className="flex justify-between" style={{ fontSize: "0.85rem" }}>
                <span style={{ color: "var(--color-success)" }}>Diskon Voucher</span>
                <span className="tabular-price" style={{ color: "var(--color-success)", fontWeight: 600 }}>-{formatRp(discountInfo.discountAmount)}</span>
              </div>
              <div className="flex justify-between pt-1" style={{ fontSize: "0.9rem", borderTop: "1px solid var(--color-border-lighter)" }}>
                <span style={{ color: "var(--color-brand-primary)", fontWeight: 600 }}>Total Dibayar</span>
                <span className="tabular-price" style={{ color: "var(--color-brand-tertiary)", fontWeight: 700 }}>{formatRp(discountInfo.finalPrice)}</span>
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
