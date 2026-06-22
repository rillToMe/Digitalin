import { create } from "zustand";
import type { ProductItem, BrandDetailResponse } from "../api/index";

interface PaymentMethod {
  id: string;
  name: string;
  logo: string;
  fee: number;
}

interface CheckoutState {
  brand: BrandDetailResponse | null;
  selectedProduct: ProductItem | null;
  playerId: string;
  zoneId: string;
  selectedServer: string;
  selectedPayment: PaymentMethod | null;
  voucherCode: string | null;
  setBrand: (brand: BrandDetailResponse) => void;
  setSelectedProduct: (product: ProductItem) => void;
  setPlayerId: (id: string) => void;
  setZoneId: (id: string) => void;
  setSelectedServer: (server: string) => void;
  setSelectedPayment: (method: PaymentMethod) => void;
  setVoucherCode: (code: string | null) => void;
  reset: () => void;
}

export const useCheckoutStore = create<CheckoutState>((set) => ({
  brand: null,
  selectedProduct: null,
  playerId: "",
  zoneId: "",
  selectedServer: "",
  selectedPayment: null,
  voucherCode: null,
  setBrand: (brand) => set({ brand }),
  setSelectedProduct: (product) => set({ selectedProduct: product }),
  setPlayerId: (id) => set({ playerId: id }),
  setZoneId: (id) => set({ zoneId: id }),
  setSelectedServer: (server) => set({ selectedServer: server }),
  setSelectedPayment: (method) => set({ selectedPayment: method }),
  setVoucherCode: (code) => set({ voucherCode: code }),
  reset: () =>
    set({
      brand: null,
      selectedProduct: null,
      playerId: "",
      zoneId: "",
      selectedServer: "",
      selectedPayment: null,
      voucherCode: null,
    }),
}));

export type { PaymentMethod };
