import { useQuery } from "@tanstack/react-query";
import { paymentMethodService } from "../services/api";

/**
 * Metode pembayaran aktif untuk checkout — SSOT backend (ikon, urutan,
 * aktif; bank VA di bawah bank_transfer). Query key tunggal "payment-methods".
 */
export function usePaymentMethods() {
  return useQuery({
    queryKey: ["payment-methods"],
    queryFn: () => paymentMethodService.getEnabled(),
    staleTime: 5 * 60 * 1000, // konfigurasi jarang berubah
    retry: 1,
  });
}
