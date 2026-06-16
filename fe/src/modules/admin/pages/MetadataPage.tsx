import { useState } from "react";
import { Pencil, X, Loader2, Save } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminService } from "../../../services/api";
import type { GameMetadataItem, InputType, UpdateMetadataPayload } from "../../../api/index";

export default function MetadataPage() {
  const queryClient = useQueryClient();
  const [editTarget, setEditTarget] = useState<GameMetadataItem | null>(null);
  const [form, setForm] = useState<{ inputType: InputType; serverList: string }>({ inputType: "USER_ID_ONLY", serverList: "" });

  const { data: items = [], isLoading: loading } = useQuery({
    queryKey: ["admin-metadata"],
    queryFn: adminService.getMetadata,
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, payload }: { id: number; payload: UpdateMetadataPayload }) => adminService.updateMetadata(id, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-metadata"] });
      toast.success("Metadata diperbarui");
      setEditTarget(null);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      toast.error(err?.response?.data?.message ?? "Gagal memperbarui metadata");
    },
  });

  const openEdit = (item: GameMetadataItem) => {
    setEditTarget(item);
    setForm({ inputType: item.inputType, serverList: item.serverList ?? "" });
  };

  const handleSave = () => {
    if (!editTarget) return;
    updateMutation.mutate({
      id: editTarget.id,
      payload: {
        inputType: form.inputType,
        serverList: form.serverList || null,
      },
    });
  };

  return (
    <AdminLayout title="Metadata" breadcrumb={["Admin", "Metadata"]}>
      <div className="rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
        <div className="overflow-x-auto">
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid rgba(0,0,0,0.07)", backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                {["Brand", "Input Type", "Server List", "Aksi"].map((h) => (
                  <th key={h} className="px-5 py-3 text-left" style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.78rem", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.04em" }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                Array.from({ length: 4 }).map((_, i) => (
                  <tr key={i}>
                    {Array.from({ length: 4 }).map((__, j) => (
                      <td key={j} className="px-5 py-3">
                        <div className="h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }} />
                      </td>
                    ))}
                  </tr>
                ))
              ) : items.map((item) => (
                <tr key={item.id} style={{ borderBottom: "1px solid rgba(0,0,0,0.05)" }}>
                  <td className="px-5 py-4" style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.9rem" }}>{item.brand}</td>
                  <td className="px-5 py-4">
                    <span className="px-2.5 py-1 rounded-full" style={{
                      backgroundColor: item.inputType === "USER_ID_ONLY" ? "#dbeafe" : "#ede9fe",
                      color: item.inputType === "USER_ID_ONLY" ? "#1d4ed8" : "#6d28d9",
                      fontSize: "0.75rem", fontWeight: 600,
                    }}>
                      {item.inputType === "USER_ID_ONLY" ? "User ID Only" : "User ID + Zone ID"}
                    </span>
                  </td>
                  <td className="px-5 py-4" style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem" }}>
                    {item.serverList ? (
                      <div className="flex flex-wrap gap-1">
                        {item.serverList.split(",").map((s) => (
                          <span key={s} className="px-2 py-0.5 rounded" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)", fontSize: "0.75rem", color: "var(--color-primary,#0F1419)" }}>{s.trim()}</span>
                        ))}
                      </div>
                    ) : (
                      <span style={{ color: "rgba(0,0,0,0.3)" }}>—</span>
                    )}
                  </td>
                  <td className="px-5 py-4">
                    <button onClick={() => openEdit(item)} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-tertiary,#2C5EF5)", fontSize: "0.82rem" }}>
                      <Pencil size={13} /> Edit
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Edit Modal */}
      {editTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4" style={{ backgroundColor: "rgba(0,0,0,0.4)" }}>
          <div className="w-full max-w-md rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface,#fff)" }}>
            <div className="flex items-center justify-between mb-5">
              <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1rem" }}>Edit Metadata – {editTarget.brand}</h3>
              <button onClick={() => setEditTarget(null)} style={{ color: "var(--color-secondary,#4A5568)" }}><X size={20} /></button>
            </div>
            <div className="space-y-4">
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Input Type</label>
                <div className="flex gap-3">
                  {(["USER_ID_ONLY", "USER_ID_AND_ZONE_ID"] as InputType[]).map((t) => (
                    <button
                      key={t}
                      onClick={() => setForm({ ...form, inputType: t })}
                      className="flex-1 py-2.5 rounded-xl transition-all"
                      style={{
                        border: form.inputType === t ? "2px solid var(--color-tertiary,#2C5EF5)" : "1.5px solid rgba(0,0,0,0.1)",
                        backgroundColor: form.inputType === t ? "rgba(44,94,245,0.06)" : "transparent",
                        color: form.inputType === t ? "var(--color-tertiary,#2C5EF5)" : "var(--color-secondary,#4A5568)",
                        fontSize: "0.8rem", fontWeight: form.inputType === t ? 600 : 400,
                      }}
                    >
                      {t === "USER_ID_ONLY" ? "User ID Only" : "User ID + Zone ID"}
                    </button>
                  ))}
                </div>
              </div>
              <div>
                <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>
                  Server List <span style={{ color: "var(--color-secondary,#4A5568)", fontWeight: 400 }}>(pisahkan dengan koma, kosongkan jika tidak ada)</span>
                </label>
                <input
                  value={form.serverList}
                  onChange={(e) => setForm({ ...form, serverList: e.target.value })}
                  placeholder="Asia,Europe,America"
                  className="w-full px-4 py-2.5 rounded-xl outline-none"
                  style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                />
              </div>
            </div>
            <div className="flex gap-3 mt-5">
              <button onClick={handleSave} disabled={updateMutation.isPending} className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600 }}>
                {updateMutation.isPending ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
                {updateMutation.isPending ? "Menyimpan..." : "Simpan"}
              </button>
              <button onClick={() => setEditTarget(null)} className="px-5 py-3 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-secondary,#4A5568)" }}>Batal</button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
