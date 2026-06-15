import { useState } from "react";
import { useForm } from "react-hook-form";
import { useMutation } from "@tanstack/react-query";
import { ArrowRight, Ticket, ShoppingBag, Loader2 } from "lucide-react";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import toast from "react-hot-toast";
import { cn, formatRupiah } from "@/lib/utils";
import { createOrder } from "@/services/public";
import type { BrandDetailResponse, ProductItem, InputType } from "@/types";

// ─── Midtrans Snap global type ───────────────────────────────────────────────

declare global {
  interface Window {
    snap?: {
      pay: (token: string, options?: Record<string, unknown>) => void;
    };
  }
}

// ─── Zod Schema ──────────────────────────────────────────────────────────────

const topUpSchema = z.object({
  targetId: z.string().min(1, "User ID wajib diisi."),
  zoneId: z.string().optional(),
  voucherCode: z.string().optional(),
});

type TopUpFormData = z.infer<typeof topUpSchema>;

// ─── Props ───────────────────────────────────────────────────────────────────

export interface TopUpFormProps {
  game: BrandDetailResponse;
}

// ─── Helper: Load Midtrans Snap script ───────────────────────────────────────

function loadSnapScript(): Promise<void> {
  return new Promise((resolve, reject) => {
    if (window.snap) {
      resolve();
      return;
    }
    const script = document.createElement("script");
    script.src = "https://app.sandbox.midtrans.com/snap/snap.js";
    script.dataset.clientKey = import.meta.env.VITE_MIDTRANS_CLIENT_KEY ?? "";
    script.onload = () => resolve();
    script.onerror = () => reject(new Error("Gagal memuat Midtrans Snap."));
    document.body.appendChild(script);
  });
}

// ─── Component ───────────────────────────────────────────────────────────────

export function TopUpForm({ game }: TopUpFormProps) {
  const { inputType, products } = game;
  const isZoneRequired = inputType === "USER_ID_AND_ZONE_ID";

  const [selectedProduct, setSelectedProduct] = useState<ProductItem | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<TopUpFormData>({
    resolver: zodResolver(topUpSchema),
    defaultValues: {
      targetId: "",
      zoneId: "",
      voucherCode: "",
    },
  });

  // ── Mutation: Create Order ──

  const mutation = useMutation({
    mutationFn: createOrder,
    onSuccess: async (response) => {
      if (response.snapToken) {
        try {
          await loadSnapScript();
          window.snap?.pay(response.snapToken);
        } catch {
          // Fallback: redirect to Midtrans URL
          if (response.redirectUrl) {
            window.location.href = response.redirectUrl;
          } else {
            toast.error("Gagal memuat popup pembayaran.");
          }
        }
      } else if (response.redirectUrl) {
        window.location.href = response.redirectUrl;
      }
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const apiMessage = err?.response?.data?.message;
      toast.error(apiMessage ?? "Gagal membuat pesanan. Silakan coba lagi.");
    },
  });

  // ── Submit Handler ──

  const onSubmit = (formData: TopUpFormData) => {
    // Validate product selection
    if (!selectedProduct) {
      toast.error("Silakan pilih nominal top-up terlebih dahulu.");
      return;
    }

    // Validate targetId
    if (!formData.targetId.trim()) {
      toast.error("User ID wajib diisi.");
      return;
    }

    // Validate zoneId when required
    if (isZoneRequired && !formData.zoneId?.trim()) {
      toast.error("Zone ID wajib diisi untuk game ini.");
      return;
    }

    mutation.mutate({
      productId: selectedProduct.id,
      targetId: formData.targetId.trim(),
      zoneId: isZoneRequired ? (formData.zoneId?.trim() ?? null) : null,
      voucherCode: formData.voucherCode?.trim() || null,
    });
  };

  const isPending = mutation.isPending;

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-8">
      {/* ── Step 1: Masukkan ID ── */}
      <section className="glass rounded-lg p-6">
        <h3 className="mb-4 text-base font-semibold text-primary">
          <span className="text-label mr-2 text-tertiary">STEP 1</span>
          Masukkan ID
        </h3>

        <div className="space-y-4">
          {/* User ID */}
          <div>
            <label htmlFor="targetId" className="mb-1.5 block text-sm font-medium text-secondary">
              User ID
            </label>
            <input
              id="targetId"
              type="text"
              placeholder="Masukkan User ID"
              className={cn(
                "w-full rounded-md border border-secondary/20 bg-surface px-3 py-2.5",
                "text-sm text-primary placeholder:text-secondary/50",
                "outline-none transition-all",
                "focus:border-tertiary focus:ring-2 focus:ring-tertiary/20",
                errors.targetId && "border-red-400 focus:border-red-400 focus:ring-red-200",
              )}
              {...register("targetId")}
            />
            {errors.targetId && (
              <p className="mt-1 text-xs text-red-500">{errors.targetId.message}</p>
            )}
          </div>

          {/* Zone ID (conditional) */}
          {isZoneRequired && (
            <div>
              <label htmlFor="zoneId" className="mb-1.5 block text-sm font-medium text-secondary">
                Zone / Server ID
              </label>
              <input
                id="zoneId"
                type="text"
                placeholder="Masukkan Zone ID (contoh: 1234)"
                className={cn(
                  "w-full rounded-md border border-secondary/20 bg-surface px-3 py-2.5",
                  "text-sm text-primary placeholder:text-secondary/50",
                  "outline-none transition-all",
                  "focus:border-tertiary focus:ring-2 focus:ring-tertiary/20",
                  errors.zoneId && "border-red-400 focus:border-red-400 focus:ring-red-200",
                )}
                {...register("zoneId")}
              />
              {errors.zoneId && (
                <p className="mt-1 text-xs text-red-500">{errors.zoneId.message}</p>
              )}
            </div>
          )}
        </div>
      </section>

      {/* ── Step 2: Pilih Nominal ── */}
      <section className="glass rounded-lg p-6">
        <h3 className="mb-4 text-base font-semibold text-primary">
          <span className="text-label mr-2 text-tertiary">STEP 2</span>
          Pilih Nominal
        </h3>

        {products.length === 0 ? (
          <p className="text-sm text-secondary">Belum ada produk tersedia untuk game ini.</p>
        ) : (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {products.map((product) => {
              const isSelected = selectedProduct?.id === product.id;
              return (
                <button
                  key={product.id}
                  type="button"
                  onClick={() => setSelectedProduct(product)}
                  disabled={isPending}
                  className={cn(
                    "relative flex flex-col items-start rounded-md border p-3 text-left transition-all",
                    "disabled:opacity-50",
                    isSelected
                      ? "border-tertiary bg-tertiary/5 ring-2 ring-tertiary/30"
                      : "border-secondary/15 bg-surface hover:border-secondary/30 hover:bg-surface/80",
                  )}
                >
                  <span className="text-sm font-medium text-primary leading-snug">
                    {product.name}
                  </span>
                  <span className={cn(
                    "mt-1 text-xs font-semibold",
                    isSelected ? "text-tertiary" : "text-secondary",
                  )}>
                    {formatRupiah(product.price)}
                  </span>
                </button>
              );
            })}
          </div>
        )}
      </section>

      {/* ── Order Summary (visible on mobile, also in sidebar on desktop) ── */}
      <OrderSummary
        game={game}
        selectedProduct={selectedProduct}
        register={register}
        errors={errors}
        isPending={isPending}
        inputType={inputType}
      />
    </form>
  );
}

// ─── Order Summary Sub-Component ─────────────────────────────────────────────

interface OrderSummaryProps {
  game: BrandDetailResponse;
  selectedProduct: ProductItem | null;
  register: ReturnType<typeof useForm<TopUpFormData>>["register"];
  errors: ReturnType<typeof useForm<TopUpFormData>>["formState"]["errors"];
  isPending: boolean;
  inputType: InputType;
}

function OrderSummary({
  game,
  selectedProduct,
  register,
  errors,
  isPending,
}: OrderSummaryProps) {
  return (
    <section className="glass rounded-lg p-6">
      <h3 className="mb-4 text-base font-semibold text-primary">
        <ShoppingBag className="mr-2 inline h-4 w-4 text-tertiary" />
        Ringkasan Pesanan
      </h3>

      {/* Selected product info */}
      <div className="mb-4 space-y-2 text-sm">
        <div className="flex justify-between">
          <span className="text-secondary">Game</span>
          <span className="font-medium text-primary">{game.brand}</span>
        </div>
        {selectedProduct ? (
          <>
            <div className="flex justify-between">
              <span className="text-secondary">Produk</span>
              <span className="font-medium text-primary">{selectedProduct.name}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-secondary">Harga</span>
              <span className="font-semibold text-tertiary">
                {formatRupiah(selectedProduct.price)}
              </span>
            </div>
          </>
        ) : (
          <div className="flex justify-between">
            <span className="text-secondary">Produk</span>
            <span className="text-secondary/60 italic">Belum dipilih</span>
          </div>
        )}
      </div>

      <hr className="mb-4 border-secondary/10" />

      {/* Voucher Code */}
      <div className="mb-5">
        <label htmlFor="voucherCode" className="mb-1.5 flex items-center gap-1.5 text-sm font-medium text-secondary">
          <Ticket className="h-3.5 w-3.5" />
          Kode Voucher
          <span className="text-xs text-secondary/60">(opsional)</span>
        </label>
        <input
          id="voucherCode"
          type="text"
          placeholder="Masukkan kode voucher"
          className={cn(
            "w-full rounded-md border border-secondary/20 bg-surface px-3 py-2.5",
            "text-sm text-primary placeholder:text-secondary/50",
            "outline-none transition-all",
            "focus:border-tertiary focus:ring-2 focus:ring-tertiary/20",
            errors.voucherCode && "border-red-400 focus:border-red-400 focus:ring-red-200",
          )}
          {...register("voucherCode")}
        />
        {errors.voucherCode && (
          <p className="mt-1 text-xs text-red-500">{errors.voucherCode.message}</p>
        )}
      </div>

      {/* Submit Button — single primary action per screen (rules.txt) */}
      <button
        type="submit"
        disabled={isPending}
        className={cn(
          "flex w-full items-center justify-center gap-2 rounded-md",
          "bg-tertiary px-5 py-3 text-sm font-semibold text-on-primary",
          "transition-colors hover:bg-tertiary/90",
          "disabled:cursor-not-allowed disabled:opacity-50",
        )}
      >
        {isPending ? (
          <>
            <Loader2 className="h-4 w-4 animate-spin" />
            Memproses...
          </>
        ) : (
          <>
            Beli Sekarang
            <ArrowRight className="h-4 w-4" />
          </>
        )}
      </button>
    </section>
  );
}
