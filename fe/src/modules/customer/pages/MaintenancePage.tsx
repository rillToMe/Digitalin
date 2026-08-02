import { useState, useEffect } from "react";
import { useNavigate } from "react-router";
import { toast } from "sonner";
import { useMutation } from "@tanstack/react-query";
import { brandService } from "../../../services/api";
import { useMaintenanceStore } from "../../../store/maintenanceStore";
import MaintenanceCard from "../../../components/common/MaintenanceCard";

export default function MaintenancePage() {
  const navigate = useNavigate();
  const { isMaintenance, title, message, estimatedFinish, clearMaintenance, setMaintenance } = useMaintenanceStore();
  const [countdown, setCountdown] = useState(30);

  // Mutation to check if backend is still in maintenance mode
  const checkStatusMutation = useMutation({
    // limit:1 — hanya perlu tahu API menjawab 200 atau 503, bukan isi katalognya.
    mutationFn: () => brandService.getAll({ limit: 1 }),
    onSuccess: () => {
      // If success, it means maintenance mode is OFF!
      clearMaintenance();
      toast.success("Website kembali online!");
      navigate("/");
    },
    onError: (err: any) => {
      if (err.response?.status === 503) {
        const data = err.response.data;
        if (data && data.maintenance === true) {
          setMaintenance(true, data.title, data.message, data.estimatedFinish);
        }
        // Reset countdown timer
        setCountdown(30);
      } else {
        // Any other response status means API is back online
        clearMaintenance();
        toast.success("Website kembali online!");
        navigate("/");
      }
    },
  });

  // If page was loaded directly/refreshed, check status immediately
  useEffect(() => {
    if (!isMaintenance) {
      checkStatusMutation.mutate();
    }
  }, [isMaintenance]);

  // Countdown timer logic
  useEffect(() => {
    if (countdown === 0) {
      // Automatically check status when countdown hits 0
      checkStatusMutation.mutate();
      return;
    }
    const timer = setInterval(() => {
      setCountdown((prev) => prev - 1);
    }, 1000);
    return () => clearInterval(timer);
  }, [countdown]);

  const handleRefreshClick = () => {
    if (checkStatusMutation.isPending) return;
    checkStatusMutation.mutate();
  };

  return (
    <div
      className="min-h-screen flex items-center justify-center p-4"
      style={{ backgroundColor: "var(--color-neutral)" }}
    >
      <MaintenanceCard
        title={title}
        message={message}
        estimatedFinish={estimatedFinish}
        onRefresh={handleRefreshClick}
        isChecking={checkStatusMutation.isPending}
        countdown={countdown}
      />
    </div>
  );
}
