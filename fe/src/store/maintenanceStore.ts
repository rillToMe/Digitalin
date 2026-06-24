import { create } from "zustand";

interface MaintenanceState {
  isMaintenance: boolean;
  title: string;
  message: string;
  estimatedFinish: string | null;
  setMaintenance: (isMaint: boolean, title: string, message: string, estFinish: string | null) => void;
  clearMaintenance: () => void;
}

export const useMaintenanceStore = create<MaintenanceState>((set) => ({
  isMaintenance: false,
  title: "",
  message: "",
  estimatedFinish: null,
  setMaintenance: (isMaint, title, message, estFinish) =>
    set({ isMaintenance: isMaint, title, message, estimatedFinish: estFinish }),
  clearMaintenance: () =>
    set({ isMaintenance: false, title: "", message: "", estimatedFinish: null }),
}));
