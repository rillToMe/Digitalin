import { create } from "zustand";
import type {
  ProductItem,
  BrandDetailResponse,
  TargetValues,
  PaymentMethodInfo,
} from "../api/index";

/**
 * State checkout. Katalog metode pembayaran TIDAK ada di sini — SSOT-nya
 * backend (GET /payment-methods, ikon + urutan + aktif); halaman checkout
 * mengambilnya lewat usePaymentMethods dan memilihnya ke sini.
 */
interface CheckoutState {
  brand: BrandDetailResponse | null;
  selectedProduct: ProductItem | null;
  targetValues: TargetValues;
  selectedPayment: PaymentMethodInfo | null;
  selectedChannel: string | null;
  voucherCode: string | null;

  setBrand: (brand: BrandDetailResponse) => void;
  setSelectedProduct: (product: ProductItem | null) => void;
  setTargetValue: (key: string, value: string) => void;
  setSelectedPayment: (method: PaymentMethodInfo | null) => void;
  setSelectedChannel: (channel: string | null) => void;
  setVoucherCode: (code: string | null) => void;
  reset: () => void;
}

const initialCheckoutState = {
  brand: null,
  selectedProduct: null,
  targetValues: {},
  selectedPayment: null,
  selectedChannel: null,
  voucherCode: null,
};

export const useCheckoutStore = create<CheckoutState>((set) => ({
  ...initialCheckoutState,

  setBrand: (brand) => set({ brand }),

  setSelectedProduct: (product) =>
    set({ selectedProduct: product }),

  setTargetValue: (key, value) =>
    set((state) => ({
      targetValues: {
        ...state.targetValues,
        [key]: value,
      },
    })),

  setSelectedPayment: (method) =>
    set({ selectedPayment: method }),

  setSelectedChannel: (channel) => set({ selectedChannel: channel }),

  setVoucherCode: (code) =>
    set({ voucherCode: code }),

  reset: () =>
    set({
      ...initialCheckoutState,
      targetValues: {},
    }),
}));
