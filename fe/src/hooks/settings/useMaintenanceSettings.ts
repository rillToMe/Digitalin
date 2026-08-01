import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { adminService } from "../../services/api";

export function useMaintenanceSettings() {
  const queryClient = useQueryClient();

  const [maintEnabled, setMaintEnabled] = useState(false);
  const [maintTitle, setMaintTitle] = useState("Website Sedang Maintenance");
  const [maintMessage, setMaintMessage] = useState(
    "Kami sedang melakukan peningkatan sistem. Mohon tunggu sebentar."
  );
  const [maintEstimated, setMaintEstimated] = useState("");
  const [maintAllowAdmin, setMaintAllowAdmin] = useState(true);

  const getNowLocalString = (): string => {
    const now = new Date();
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(
      now.getDate()
    )}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
  };

  const isEstimatedInPast =
    maintEstimated.trim() !== "" &&
    new Date(maintEstimated).getTime() <= Date.now();

  const {
    data: maintData,
    isLoading: maintLoading,
    isError: maintError,
    refetch: refetchMaint,
  } = useQuery({
    queryKey: ["maintenance-status"],
    queryFn: adminService.getMaintenance,
    retry: 1,
  });

  useEffect(() => {
    if (maintData) {
      setMaintEnabled(maintData.enabled);
      setMaintTitle(maintData.title);
      setMaintMessage(maintData.message);
      setMaintEstimated(maintData.estimatedFinish ?? "");
      setMaintAllowAdmin(maintData.allowAdmin);
    }
  }, [maintData]);

  const updateMaintMutation = useMutation({
    mutationFn: adminService.updateMaintenance,
    onSuccess: (res) => {
      toast.success(
        res.enabled
          ? "Maintenance Mode AKTIF. User tidak dapat mengakses website."
          : "Maintenance Mode NONAKTIF. Website kembali online."
      );
      queryClient.invalidateQueries({ queryKey: ["maintenance-status"] });
    },
    onError: (err: any) => {
      toast.error(
        err?.response?.data?.message ?? "Gagal memperbarui maintenance mode."
      );
    },
  });

  const handleSaveMaintenance = (e: React.FormEvent) => {
    e.preventDefault();
    if (!maintTitle.trim()) {
      toast.error("Title maintenance wajib diisi.");
      return;
    }
    if (!maintMessage.trim()) {
      toast.error("Pesan maintenance wajib diisi.");
      return;
    }
    if (maintEstimated.trim() && new Date(maintEstimated).getTime() <= Date.now()) {
      toast.error("Estimasi selesai harus berada di masa depan.");
      return;
    }
    updateMaintMutation.mutate({
      enabled: maintEnabled,
      title: maintTitle.trim(),
      message: maintMessage.trim(),
      estimatedFinish: maintEstimated.trim() || null,
      allowAdmin: maintAllowAdmin,
    });
  };

  const handleToggleMaintenance = () => {
    const newEnabled = !maintEnabled;
    setMaintEnabled(newEnabled);
    const safeEstimated =
      maintEstimated.trim() && new Date(maintEstimated).getTime() > Date.now()
        ? maintEstimated.trim()
        : null;
    updateMaintMutation.mutate({
      enabled: newEnabled,
      title: maintTitle.trim() || "Website Sedang Maintenance",
      message:
        maintMessage.trim() ||
        "Kami sedang melakukan peningkatan sistem. Mohon tunggu sebentar.",
      estimatedFinish: safeEstimated,
      allowAdmin: maintAllowAdmin,
    });
  };

  return {
    maintEnabled,
    setMaintEnabled,
    maintTitle,
    setMaintTitle,
    maintMessage,
    setMaintMessage,
    maintEstimated,
    setMaintEstimated,
    maintAllowAdmin,
    setMaintAllowAdmin,
    maintLoading,
    maintError,
    refetchMaint,
    isSavingMaint: updateMaintMutation.isPending,
    isEstimatedInPast,
    getNowLocalString,
    handleSaveMaintenance,
    handleToggleMaintenance,
  };
}
