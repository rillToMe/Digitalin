import { useState } from "react";
import { Upload, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { adminService } from "../../../services/api";

export function SyncButton() {
  const [syncing, setSyncing] = useState(false);

  const handleSync = async () => {
    setSyncing(true);
    try {
      const res = await adminService.syncProducts();
      toast.success(res.message);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: { message?: string } } })?.response?.data?.message ?? "Gagal sinkronisasi produk";
      toast.error(msg);
    } finally {
      setSyncing(false);
    }
  };

  return (
    <button onClick={handleSync} disabled={syncing} className="flex items-center gap-2 px-4 py-2 rounded-xl" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.85rem", fontWeight: 600, opacity: syncing ? 0.7 : 1 }}>
      {syncing ? <Loader2 size={14} className="animate-spin" /> : <Upload size={14} />}
      {syncing ? "Menyinkronkan..." : "Sync Digiflazz"}
    </button>
  );
}
