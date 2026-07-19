import { create } from "zustand";
import type {
  ProductItem,
  BrandDetailResponse,
  TargetValues,
} from "../api/index";

interface PaymentMethod {
  id: string;
  name: string;
  logo: string;
  fee: number;
}

interface CheckoutState {
  brand: BrandDetailResponse | null;
  selectedProduct: ProductItem | null;
  targetValues: TargetValues;
  selectedPayment: PaymentMethod | null;
  voucherCode: string | null;

  setBrand: (brand: BrandDetailResponse) => void;
  setSelectedProduct: (product: ProductItem | null) => void;
  setTargetValue: (key: string, value: string) => void;
  setSelectedPayment: (method: PaymentMethod | null) => void;
  setVoucherCode: (code: string | null) => void;
  reset: () => void;
}

const initialCheckoutState = {
  brand: null,
  selectedProduct: null,
  targetValues: {},
  selectedPayment: null,
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

  setVoucherCode: (code) =>
    set({ voucherCode: code }),

  reset: () =>
    set({
      ...initialCheckoutState,
      targetValues: {},
    }),
}));

export type { PaymentMethod };