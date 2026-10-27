import { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import { Check, AlertCircle, Loader2, ShieldCheck, Gamepad2 } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { brandService, orderService } from "../../../services/api";
import { useCheckoutStore } from "../../../store/checkoutStore";
import { usePaymentMethods } from "../../../hooks/usePaymentMethods";
import type { CreateOrderPayload, CheckoutPreviewResponse, UsernameCheckResponse } from "../../../api/index";
import { ImageWithFallback } from "../../../utils/ImageWithFallback";
import VoucherCard from "../../../components/common/VoucherCard";
import BackButton from "../../../components/common/BackButton";

/** Ikon metode tanpa iconUrl — monogram netral (bukan logo palsu). */
function MethodIconFallback({ name }: { name: string }) {
  return (
    <span
      className="flex h-[22px] min-w-[22px] items-center justify-center rounded-md px-1.5 label-mono"
      style={{
        backgroundColor: "var(--color-surface-raised)",
        border: "1px solid var(--color-border-default)",
        color: "var(--color-brand-secondary)",
        fontSize: "0.62rem",
      }}
    >
      {name.slice(0, 4).toUpperCase()}
    </span>
  );
}

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

  const [previewResult, setPreviewResult] = useState<CheckoutPreviewResponse | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [usernameCheck, setUsernameCheck] = useState<UsernameCheckResponse | null>(null);

  const {
    selectedProduct,
    setSelectedProduct,
    targetValues,
    setTargetValue,
    setBrand,
    reset,
    voucherCode,
    setVoucherCode,
    selectedPayment,
    setSelectedPayment,
    selectedChannel,
    setSelectedChannel,
  } = useCheckoutStore();

  const { data: brand, isLoading: loading, isError } = useQuery({
    queryKey: ["brand", slug],
    queryFn: () => brandService.getBySlug(slug!),
    enabled: !!slug,
  });

  // Katalog metode/bank dari backend (SSOT: ikon, urutan, aktif).
  const {
    data: paymentMethods,
    isLoading: methodsLoading,
    isError: methodsError,
    refetch: refetchMethods,
  } = usePaymentMethods();

  useEffect(() => {
    reset();
  }, [slug]);

  // Default metode pertama bila belum ada pilihan (mis. setelah reset ganti
  // game) — pilihan lama yang sudah tidak aktif/valid juga diganti.
  useEffect(() => {
    if (!paymentMethods || paymentMethods.length === 0) return;
    const stillValid = paymentMethods.some((m) => m.code === selectedPayment?.code);
    if (!stillValid) setSelectedPayment(paymentMethods[0]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [paymentMethods]);

  // Reset voucher and preview when product or player data changes
  useEffect(() => {
    setVoucherCode(null);
    setPreviewResult(null);
    setPreviewError(null);
    setUsernameCheck(null);
  }, [selectedProduct?.id, targetValues, setVoucherCode]);

  const usernameCheckMutation = useMutation({
    mutationFn: () => brandService.checkUsername(slug!, targetValues),
    onSuccess: setUsernameCheck,
    onError: (error: { response?: { data?: { message?: string } } }) => {
      setUsernameCheck(null);
      toast.error(error.response?.data?.message ?? "Gagal mengecek nama akun.");
    },
  });

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
      // Charge Core API sukses: instruksi bayar (QR/VA/deeplink) dirender
      // di halaman status — di sana juga ada polling dan countdown batas bayar.
      queryClient.invalidateQueries({ queryKey: ["orders"] });
      // URL status SELALU PublicHash (identifier publik satu-satunya);
      // order baru selalu punya hash (dibuat atomik bersama OrderNo).
      navigate(`/order/status/${res.publicHash ?? ""}`);
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

    // Sub-kanal (bank VA / toko cstore): wajib bila metode punya daftar.
    if (selectedPayment && selectedPayment.banks.length > 0) {
      const active = selectedPayment.banks.some(
        (b) => b.code === selectedChannel,
      );
      if (!active) {
        toast.error(
          selectedPayment.code === "bank_transfer"
            ? "Pilih bank Virtual Account terlebih dahulu"
            : "Pilih toko terlebih dahulu",
        );
        return;
      }
    }

    createOrder.mutate({
      productId: selectedProduct.id,
      targetValues,
      voucherCode,
      paymentMethod: selectedPayment!.code,
      paymentBank:
        selectedPayment!.code === "bank_transfer" ? selectedChannel : null,
      paymentStore:
        selectedPayment!.code === "cstore" ? selectedChannel : null,
    });
  };

  const total = previewResult ? previewResult.finalPrice : (selectedProduct?.price ?? 0);
  const originalPrice = previewResult?.originalPrice ?? selectedProduct?.price ?? 0;
  const discountAmount = previewResult?.discountAmount ?? 0;
  const isSubmitting = createOrder.isPending;
  const paymentGroups = paymentMethods
    ? [
        {
          label: "Rekomendasi",
          methods: paymentMethods.filter((method) => method.code === "qris"),
        },
        {
          label: "E-Wallet",
          methods: paymentMethods.filter((method) =>
            ["gopay", "shopeepay"].includes(method.code),
          ),
        },
        {
          label: "Virtual Account",
          methods: paymentMethods.filter((method) => method.code === "bank_transfer"),
        },
        {
          label: "Toko Retail",
          methods: paymentMethods.filter((method) => method.code === "cstore"),
        },
        {
          label: "Metode Lainnya",
          methods: paymentMethods.filter(
            (method) =>
              !["qris", "gopay", "shopeepay", "bank_transfer", "cstore"].includes(method.code),
          ),
        },
      ].filter((group) => group.methods.length > 0)
    : [];

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
            {brand.canCheckUsername && (
              <div className="mt-4 space-y-2">
                <button
                  type="button"
                  onClick={() => usernameCheckMutation.mutate()}
                  disabled={usernameCheckMutation.isPending}
                  className="flex items-center gap-2 rounded-[3px] border px-4 py-2.5 label-mono transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 disabled:cursor-not-allowed disabled:opacity-60"
                  style={{
                    borderColor: "var(--color-brand-tertiary)",
                    backgroundColor: "transparent",
                    color: "var(--color-brand-tertiary)",
                    fontSize: "0.78rem",
                    fontWeight: 700,
                    letterSpacing: "0.03em",
                  }}
                  onMouseEnter={(event) => {
                    event.currentTarget.style.backgroundColor = "var(--color-hover-info)";
                  }}
                  onMouseLeave={(event) => {
                    event.currentTarget.style.backgroundColor = "transparent";
                  }}
                >
                  {usernameCheckMutation.isPending && <Loader2 size={16} className="animate-spin" />}
                  {usernameCheckMutation.isPending ? "Mengecek..." : "Cek Nama Akun"}
                </button>
                {usernameCheck && (
                  <p className="label-mono" style={{ color: usernameCheck.isValid ? "var(--color-success)" : "var(--color-error)", fontSize: "0.75rem", fontWeight: 600 }}>
                    {usernameCheck.isValid ? `Nama akun: ${usernameCheck.username}` : usernameCheck.message}
                  </p>
                )}
              </div>
            )}
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
                    <div className="flex items-center gap-2 pr-6">
                      {product.imageUrl && (
                        <img src={product.imageUrl} alt="" aria-hidden="true" className="h6 w-10 shrink-0 object-contain" />
                      )}
                      <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.85rem" }}>{product.name}</p>
                    </div>
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

          {/* Payment Method Selection — katalog dari backend (ikon + urutan + aktif) */}
          <div className="hud-checkout-panel p-5" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-default)" }}>
            <StepHeading step="03">Metode Pembayaran</StepHeading>

            {methodsLoading ? (
              <div className="py-6 flex flex-col items-center gap-2">
                <Loader2 size={22} className="animate-spin" style={{ color: "var(--color-brand-tertiary)" }} aria-hidden="true" />
                <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem" }}>
                  Memuat metode pembayaran...
                </span>
              </div>
            ) : methodsError || !paymentMethods || paymentMethods.length === 0 ? (
              <div className="py-5 text-center space-y-3">
                <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
                  Gagal memuat metode pembayaran.
                </p>
                <button
                  type="button"
                  onClick={() => refetchMethods()}
                  className="px-4 py-2 rounded-xl text-xs font-semibold"
                  style={{
                    border: "1px solid var(--color-border-default)",
                    color: "var(--color-brand-primary)",
                  }}
                >
                  Coba Lagi
                </button>
              </div>
            ) : (
              <>
                <div className="space-y-5">
                  {paymentGroups.map((group) => (
                    <section key={group.label} aria-label={group.label}>
                      <h3
                        className="mb-2"
                        style={{
                          color: "var(--color-brand-secondary)",
                          fontSize: "0.72rem",
                          fontWeight: 700,
                          letterSpacing: "0.08em",
                          textTransform: "uppercase",
                        }}
                      >
                        {group.label}
                      </h3>
                      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                        {group.methods.map((method) => {
                          const active = selectedPayment?.code === method.code;
                          const methodName = method.code === "bank_transfer" ? "Virtual Account" : method.name;
                          return (
                            <button
                              key={method.code}
                              type="button"
                              onClick={() => setSelectedPayment(method)}
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
                              <div className="flex items-center gap-2 pr-6">
                                {method.iconUrl ? (
                                  <ImageWithFallback
                                    src={method.iconUrl}
                                    alt={methodName}
                                    className="h-[22px] w-auto object-contain"
                                  />
                                ) : (
                                  <MethodIconFallback name={methodName} />
                                )}
                                <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.85rem" }}>{methodName}</p>
                              </div>
                              {method.description && (
                                <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.72rem", marginTop: 4 }}>
                                  {method.description}
                                </p>
                              )}
                            </button>
                          );
                        })}
                      </div>
                    </section>
                  ))}
                </div>

                {/* Sub-kanal (bank VA / toko cstore): kartu grid (bukan
                    dropdown) — urut SortOrder backend, dari katalog API. */}
                {selectedPayment && selectedPayment.banks.length > 0 && (
                  <div className="mt-4">
                    <label
                      style={{
                        color: "var(--color-brand-primary)",
                        fontSize: "0.85rem",
                        fontWeight: 500,
                        display: "block",
                        marginBottom: 8,
                      }}
                    >
                      {selectedPayment.code === "bank_transfer"
                        ? "Bank Virtual Account *"
                        : "Pilih Toko *"}
                    </label>
                    <div className="grid grid-cols-2 sm:grid-cols-3 gap-2.5">
                      {selectedPayment.banks.map((bank) => {
                        const activeBank = selectedChannel === bank.code;
                        return (
                          <button
                            key={bank.code}
                            type="button"
                            onClick={() => setSelectedChannel(bank.code)}
                            aria-pressed={activeBank}
                            className="hud-checkout-control hud-product-option relative flex items-center gap-2.5 p-3 text-left transition-colors active:bg-(--color-accent-tint-strong) focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-(--color-brand-tertiary)"
                            style={{
                              border: activeBank
                                ? "1.5px solid var(--color-brand-tertiary)"
                                : "1.5px solid var(--color-border-default)",
                              backgroundColor: activeBank ? "var(--color-accent-tint)" : "var(--color-surface)",
                            }}
                          >
                            {activeBank && (
                              <div className="hud-checkout-control absolute right-1.5 top-1.5 flex h-4 w-4 items-center justify-center" style={{ backgroundColor: "var(--color-brand-tertiary)" }}>
                                <Check size={9} style={{ color: "var(--color-on-primary)" }} aria-hidden="true" />
                              </div>
                            )}
                            {bank.iconUrl ? (
                              <ImageWithFallback
                                src={bank.iconUrl}
                                alt={bank.name}
                                className="h-6 w-auto max-w-[54px] object-contain"
                              />
                            ) : (
                              <MethodIconFallback name={bank.name} />
                            )}
                            <span className="truncate pr-4" style={{ color: "var(--color-brand-primary)", fontWeight: 500, fontSize: "0.8rem" }}>
                              {bank.name}
                            </span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </>
            )}
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
    </div>
  );
}
