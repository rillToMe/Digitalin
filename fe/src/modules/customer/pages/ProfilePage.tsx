import { useState } from "react";
import { User, Mail, Calendar, Edit2, Loader2, Check } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import { authService } from "../../../services/api";
import type { UpdateProfilePayload } from "../../../api/index";

export default function ProfilePage() {
  const { setUser } = useAuthStore();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ fullName: "", newPassword: "" });

  const { data: profile, isLoading: loading } = useQuery({
    queryKey: ["profile"],
    queryFn: authService.getProfile,
  });

  const updateMutation = useMutation({
    mutationFn: (payload: UpdateProfilePayload) => authService.updateProfile(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["profile"] });
      toast.success("Profil berhasil diperbarui");
      setEditing(false);
    },
    onError: (err: { response?: { data?: { message?: string } } }) => {
      const msg = err?.response?.data?.message ?? "Gagal memperbarui profil";
      toast.error(msg);
    },
  });

  // Sync form when profile loads
  if (profile && !form.fullName && !editing) {
    setForm({ fullName: profile.fullName, newPassword: "" });
    setUser(profile);
  }

  const handleSave = () => {
    updateMutation.mutate({
      fullName: form.fullName,
      newPassword: form.newPassword || null,
    });
  };

  if (loading) {
    return (
      <div className="max-w-2xl mx-auto px-4 py-12">
        <div className="animate-pulse space-y-4">
          <div className="h-32 rounded-2xl" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
          <div className="h-48 rounded-2xl" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
        </div>
      </div>
    );
  }

  if (!profile) return null;

  return (
    <div className="max-w-2xl mx-auto px-4 py-10">
      <h1 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem", marginBottom: 24 }}>Profil Saya</h1>

      {/* Avatar card */}
      <div className="rounded-2xl p-6 mb-5 flex items-center gap-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
        <div className="w-16 h-16 rounded-full flex items-center justify-center" style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)" }}>
          <User size={28} color="#fff" />
        </div>
        <div>
          <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.1rem" }}>{profile.fullName}</p>
          <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.85rem", marginTop: 2 }}>{profile.email}</p>
        </div>
      </div>

      {/* Info card */}
      <div className="rounded-2xl p-6 mb-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
        <div className="flex justify-between items-center mb-5">
          <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "1rem" }}>Informasi Akun</h3>
          {!editing && (
            <button onClick={() => { setForm({ fullName: profile.fullName, newPassword: "" }); setEditing(true); }} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-primary,#0F1419)", fontSize: "0.85rem" }}>
              <Edit2 size={13} /> Edit
            </button>
          )}
        </div>

        {editing ? (
          <div className="space-y-4">
            <div>
              <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Nama Lengkap</label>
              <input
                value={form.fullName}
                onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                className="w-full px-4 py-3 rounded-xl outline-none"
                style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
              />
            </div>
            <div>
              <label style={{ color: "var(--color-primary,#0F1419)", fontSize: "0.85rem", fontWeight: 500, display: "block", marginBottom: 6 }}>Password Baru (opsional)</label>
              <input
                type="password"
                value={form.newPassword}
                onChange={(e) => setForm({ ...form, newPassword: e.target.value })}
                placeholder="Biarkan kosong jika tidak ingin ganti"
                className="w-full px-4 py-3 rounded-xl outline-none"
                style={{ border: "1.5px solid rgba(0,0,0,0.12)", fontSize: "0.9rem", color: "var(--color-primary,#0F1419)" }}
                onFocus={(e) => { e.target.style.borderColor = "var(--color-tertiary,#2C5EF5)"; }}
                onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
              />
            </div>
            <div className="flex gap-3 pt-2">
              <button
                onClick={handleSave}
                disabled={updateMutation.isPending}
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                style={{ backgroundColor: "var(--color-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem" }}
              >
                {updateMutation.isPending ? <Loader2 size={16} className="animate-spin" /> : <Check size={16} />}
                Simpan
              </button>
              <button onClick={() => setEditing(false)} className="px-6 py-3 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-secondary,#4A5568)", fontSize: "0.9rem" }}>
                Batal
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            {[
              { icon: <User size={16} />, label: "Nama Lengkap", value: profile.fullName },
              { icon: <Mail size={16} />, label: "Email", value: profile.email },
              { icon: <Calendar size={16} />, label: "Bergabung", value: new Date(profile.createtAt).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" }) },
            ].map((item, i) => (
              <div key={i} className="flex items-center gap-4">
                <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)", color: "var(--color-secondary,#4A5568)" }}>
                  {item.icon}
                </div>
                <div>
                  <p style={{ color: "var(--color-secondary,#4A5568)", fontSize: "0.75rem" }}>{item.label}</p>
                  <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 500, fontSize: "0.9rem" }}>{item.value}</p>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
