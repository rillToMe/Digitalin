import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { adminService } from "../../services/api";

export function usePaymentSettings() {
  const queryClient = useQueryClient();
  const [expiryValue, setExpiryValue] = useState<number>(15);

  const {
    data: expiryData,
    isLoading: expiryLoading,
    isError: expiryError,
    refetch: refetchExpiry,
  } = useQuery({
    queryKey: ["midtrans-expiry"],
    queryFn: adminService.getMidtransExpiry,
    retry: 1,
  });

  useEffect(() => {
    if (expiryData?.expiry !== undefined) {
      setExpiryValue(expiryData.expiry);
    }
  }, [expiryData]);

  const updateExpiryMutation = useMutation({
    mutationFn: (expiry: number) =>
      adminService.updateMidtransExpiry({ expiry }),
    onSuccess: (res) => {
      toast.success(res.message || "Konfigurasi berhasil disimpan!");
      queryClient.invalidateQueries({ queryKey: ["midtrans-expiry"] });
    },
    onError: (err: any) => {
      toast.error(
        err?.response?.data?.message ?? "Gagal memperbarui konfigurasi."
      );
    },
  });

  const handleSaveExpiry = (e: React.FormEvent) => {
    e.preventDefault();
    if (expiryValue < 1) {
      toast.error("Waktu kedaluwarsa pembayaran minimal 1 menit.");
      return;
    }
    updateExpiryMutation.mutate(expiryValue);
  };

  return {
    expiryValue,
    setExpiryValue,
    expiryLoading,
    expiryError,
    refetchExpiry,
    isSavingExpiry: updateExpiryMutation.isPending,
    handleSaveExpiry,
  };
}
