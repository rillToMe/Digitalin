import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { adminService } from "../../services/api";
import type { UpdatePaymentMethodPayload, UpdatePaymentBankPayload } from "../../api/index";

export const ADMIN_PAYMENT_METHODS_KEY = ["admin-payment-methods"] as const;

/**
 * Kelola konfigurasi metode pembayaran (admin): daftar semua metode/bank
 * (termasuk nonaktif), toggle aktif, dan upload ikon (file masuk Cloudflare
 * R2 via backend — URL publik otomatis tersimpan).
 */
export function usePaymentMethodAdmin() {
  const queryClient = useQueryClient();

  const {
    data: methods,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: ADMIN_PAYMENT_METHODS_KEY,
    queryFn: () => adminService.getAllPaymentMethods(),
    retry: 1,
  });

  const invalidate = () =>
    queryClient.invalidateQueries({ queryKey: ADMIN_PAYMENT_METHODS_KEY });

  const errMsg = (err: unknown) =>
    (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
    "Gagal menyimpan perubahan.";

  const toggleMethod = useMutation({
    mutationFn: ({ code, enabled }: { code: string; enabled: boolean }) =>
      adminService.updatePaymentMethod(code, { enabled }),
    onSuccess: () => invalidate(),
    onError: (err) => toast.error(errMsg(err)),
  });

  const toggleBank = useMutation({
    mutationFn: ({
      code,
      bankCode,
      enabled,
    }: {
      code: string;
      bankCode: string;
      enabled: boolean;
    }) => adminService.updatePaymentBank(code, bankCode, { enabled }),
    onSuccess: () => invalidate(),
    onError: (err) => toast.error(errMsg(err)),
  });

  const uploadMethodIcon = useMutation({
    mutationFn: ({ code, file }: { code: string; file: File }) => {
      const formData = new FormData();
      formData.append("file", file);
      return adminService.uploadPaymentMethodIcon(code, formData);
    },
    onSuccess: (res) => {
      toast.success(res.message || "Ikon berhasil diperbarui!");
      invalidate();
    },
    onError: (err) => toast.error(errMsg(err)),
  });

  const uploadBankIcon = useMutation({
    mutationFn: ({
      code,
      bankCode,
      file,
    }: {
      code: string;
      bankCode: string;
      file: File;
    }) => {
      const formData = new FormData();
      formData.append("file", file);
      return adminService.uploadPaymentBankIcon(code, bankCode, formData);
    },
    onSuccess: (res) => {
      toast.success(res.message || "Ikon berhasil diperbarui!");
      invalidate();
    },
    onError: (err) => toast.error(errMsg(err)),
  });

  const renameMethod = useMutation({
    mutationFn: ({ code, displayName }: { code: string; displayName: string }) =>
      adminService.updatePaymentMethod(code, { displayName }),
    onSuccess: () => {
      toast.success("Nama metode diperbarui");
      invalidate();
    },
    onError: (err) => toast.error(errMsg(err)),
  });

  const renameBank = useMutation({
    mutationFn: ({
      code,
      bankCode,
      displayName,
    }: {
      code: string;
      bankCode: string;
      displayName: string;
    }) => adminService.updatePaymentBank(code, bankCode, { displayName }),
    onSuccess: () => {
      toast.success("Nama bank diperbarui");
      invalidate();
    },
    onError: (err) => toast.error(errMsg(err)),
  });

  return {
    methods,
    isLoading,
    isError,
    refetch,
    toggleMethod,
    toggleBank,
    uploadMethodIcon,
    uploadBankIcon,
    renameMethod,
    renameBank,
  };
}

export type { UpdatePaymentMethodPayload, UpdatePaymentBankPayload };
