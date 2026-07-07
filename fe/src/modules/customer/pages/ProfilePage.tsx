import { useState, useMemo, useEffect } from "react";
import { Link } from "react-router";
import {
  User, Mail, Calendar, Edit2, Loader2, Check,
  History, Repeat, Lock, ChevronRight,
  ShoppingBag, TrendingUp, Gamepad2, Package, //ShieldCheck
} from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuthStore } from "../../../store/authStore";
import { authService, orderService } from "../../../services/api";
import type { UpdateProfilePayload, OrderResponse, OrderStatus } from "../../../api/index";

// --- Helpers ---

function formatFullRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

const STATUS_BADGE: Record<OrderStatus, { label: string; color: string; bg: string }> = {
  PENDING:      { label: "Menunggu",   color: "var(--color-warning)",   bg: "var(--color-warning-bg)" },
  PAID:         { label: "Dibayar",    color: "var(--color-info)",      bg: "var(--color-info-bg)" },
  PROCESSING:   { label: "Diproses",   color: "var(--color-info)",      bg: "var(--color-info-bg)" },
  COMPLETED:    { label: "Sukses",     color: "var(--color-success)",   bg: "var(--color-success-bg)" },
  NEEDS_ACTION: { label: "Perlu Aksi", color: "var(--color-warning)",   bg: "var(--color-warning-bg)" },
  FAILED:       { label: "Gagal",      color: "var(--color-danger)",    bg: "var(--color-danger-bg)" },
  EXPIRED:      { label: "Expired",    color: "var(--color-brand-secondary)", bg: "var(--color-neutral)" },
  REFUNDED:     { label: "Refunded",   color: "var(--color-info)",      bg: "var(--color-info-bg)" },
};

function StatusBadge({ status }: { status: OrderStatus }) {
  const cfg = STATUS_BADGE[status] ?? { label: status, color: "var(--color-brand-secondary)", bg: "var(--color-neutral)" };
  return (
    <span className="px-2 py-0.5 rounded-full" style={{ backgroundColor: cfg.bg, color: cfg.color, fontSize: "0.7rem", fontWeight: 600 }}>
      {cfg.label}
    </span>
  );
}

// --- Main Component ---

export default function ProfilePage() {
  const { setUser } = useAuthStore();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ fullName: "", newPassword: "" });

  const { data: profile, isLoading: profileLoading } = useQuery({
    queryKey: ["profile"],
    queryFn: authService.getProfile,
  });

  const { data: historyData, isLoading: historyLoading } = useQuery({
    queryKey: ["orders", "profile-stats"],
    queryFn: () => orderService.getHistory(1, 100),
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
  useEffect(() => {
    if (profile && !form.fullName && !editing) {
      setForm({ fullName: profile.fullName, newPassword: "" });
      setUser(profile);
    }
  }, [profile, form.fullName, editing, setUser]);

  // Compute stats and favorites from order history
  const allOrders = historyData?.data ?? [];
  const totalOrders = historyData?.totalItems ?? 0;

  const stats = useMemo(() => {
    const completedOrders = allOrders.filter((o) => o.status === "COMPLETED");
    const totalSpend = completedOrders.reduce((sum, o) => sum + o.finalPrice, 0);
    return {
      totalOrders,
      totalSpend,
      successCount: completedOrders.length,
    };
  }, [allOrders, totalOrders]);

  const recentOrders = useMemo(() => allOrders.slice(0, 5), [allOrders]);

  const favoriteGames = useMemo(() => {
    const counts: Record<string, number> = {};
    for (const order of allOrders) {
      const name = order.productName.split(" - ")[0].trim();
      counts[name] = (counts[name] ?? 0) + 1;
    }
    return Object.entries(counts)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 6)
      .map(([name, count]) => ({ name, count }));
  }, [allOrders]);

  const memberSince = profile
    ? new Date(profile.createtAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" })
    : "-";

  const handleSave = () => {
    updateMutation.mutate({
      fullName: form.fullName,
      newPassword: form.newPassword || null,
    });
  };

  // --- Loading ---
  if (profileLoading) {
    return (
      <div className="max-w-5xl mx-auto px-4 py-12">
        <div className="animate-pulse space-y-4">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
            <div className="space-y-4">
              <div className="h-36 rounded-2xl" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
              <div className="h-64 rounded-2xl" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
            </div>
            <div className="lg:col-span-2 space-y-4">
              <div className="h-28 rounded-2xl" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
              <div className="h-48 rounded-2xl" style={{ backgroundColor: "var(--color-surface,#fff)" }} />
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (!profile) return null;

  return (
    <div className="max-w-5xl mx-auto px-4 py-10">
      <h1 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem", marginBottom: 24 }}>Profil Saya</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        {/* ─── LEFT COLUMN ─── */}
        <div className="space-y-5">
          {/* Avatar Card */}
          <div className="rounded-2xl p-6 flex items-center gap-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <div className="w-16 h-16 rounded-full flex items-center justify-center flex-shrink-0" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)" }}>
              <User size={28} color="#fff" />
            </div>
            <div className="min-w-0">
              <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.1rem" }} className="truncate">{profile.fullName}</p>
              <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem", marginTop: 2 }} className="truncate">{profile.email}</p>
            </div>
          </div>

          {/* Account Info Card */}
          <div className="rounded-2xl p-6" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
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
                    onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary,#2C5EF5)"; }}
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
                    onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary,#2C5EF5)"; }}
                    onBlur={(e) => { e.target.style.borderColor = "rgba(0,0,0,0.12)"; }}
                  />
                </div>
                <div className="flex gap-3 pt-2">
                  <button
                    onClick={handleSave}
                    disabled={updateMutation.isPending}
                    className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                    style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem" }}
                  >
                    {updateMutation.isPending ? <Loader2 size={16} className="animate-spin" /> : <Check size={16} />}
                    Simpan
                  </button>
                  <button onClick={() => setEditing(false)} className="px-6 py-3 rounded-xl" style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.9rem" }}>
                    Batal
                  </button>
                </div>
              </div>
            ) : (
              <div className="space-y-4">
                {[
                  { icon: <User size={16} />, label: "Nama Lengkap", value: profile.fullName },
                  { icon: <Mail size={16} />, label: "Email", value: profile.email },
                  { icon: <Calendar size={16} />, label: "Bergabung", value: memberSince },
                ].map((item, i) => (
                  <div key={i} className="flex items-center gap-4">
                    <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)", color: "var(--color-brand-secondary,#4A5568)" }}>
                      {item.icon}
                    </div>
                    <div>
                      <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem" }}>{item.label}</p>
                      <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 500, fontSize: "0.9rem" }}>{item.value}</p>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Quick Actions */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginBottom: 12 }}>Aksi Cepat</h3>
            <div className="space-y-2">
              {[
                { icon: <History size={16} />, label: "Riwayat Transaksi", to: "/history" },
                { icon: <Repeat size={16} />, label: "Top Up Terakhir", to: "/history" },
                { icon: <Lock size={16} />, label: "Ubah Password", to: "#", onClick: () => { setForm({ fullName: profile.fullName, newPassword: "" }); setEditing(true); } },
              ].map((action, i) => {
                const content = (
                  <div className="flex items-center gap-3 px-4 py-3 rounded-xl transition-colors hover:bg-gray-50" style={{ color: "var(--color-primary,#0F1419)" }}>
                    <div className="w-8 h-8 rounded-lg flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)", color: "var(--color-brand-tertiary,#2C5EF5)" }}>
                      {action.icon}
                    </div>
                    <span style={{ fontSize: "0.9rem", fontWeight: 500, flex: 1 }}>{action.label}</span>
                    <ChevronRight size={14} style={{ color: "var(--color-brand-secondary,#4A5568)" }} />
                  </div>
                );
                if (action.onClick) {
                  return <button key={i} onClick={action.onClick} className="w-full text-left">{content}</button>;
                }
                return <Link key={i} to={action.to} className="no-underline">{content}</Link>;
              })}
            </div>
          </div>
        </div>

        {/* ─── RIGHT COLUMN ─── */}
        <div className="lg:col-span-2 space-y-5">
          {/* Stats Card */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginBottom: 16 }}>Statistik Akun</h3>
            {historyLoading ? (
              <div className="grid grid-cols-3 gap-3">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="h-20 rounded-xl animate-pulse" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }} />
                ))}
              </div>
            ) : (
              <div className="grid grid-cols-3 gap-3">
                {[
                  { icon: <ShoppingBag size={18} />, label: "Total Order", value: String(stats.totalOrders), color: "#2C5EF5" },
                  { icon: <TrendingUp size={18} />, label: "Total Spend", value: formatFullRp(stats.totalSpend), color: "#15803d" },
                  { icon: <Calendar size={18} />, label: "Member Sejak", value: memberSince, color: "#b45309" },
                ].map((stat, i) => (
                  <div key={i} className="rounded-xl p-4 text-center" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                    <div className="w-9 h-9 rounded-lg flex items-center justify-center mx-auto mb-2" style={{ backgroundColor: `${stat.color}15`, color: stat.color }}>
                      {stat.icon}
                    </div>
                    <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 700, fontSize: "1.1rem" }}>{stat.value}</p>
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 2 }}>{stat.label}</p>
                  </div>
                ))}
              </div>
            )}
            {!historyLoading && stats.successCount > 0 && (
              <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem", marginTop: 12, textAlign: "center" }}>
                {stats.successCount} transaksi sukses dari {stats.totalOrders} total order
              </p>
            )}
          </div>

          {/* Recent Orders */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginBottom: 16 }}>Transaksi Terakhir</h3>
            {historyLoading ? (
              <div className="space-y-3">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="h-16 rounded-xl animate-pulse" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }} />
                ))}
              </div>
            ) : recentOrders.length === 0 ? (
              <div className="text-center py-8">
                <Package size={32} style={{ color: "var(--color-brand-secondary,#4A5568)", margin: "0 auto 8px" }} />
                <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem" }}>Belum ada transaksi</p>
              </div>
            ) : (
              <div className="space-y-2">
                {recentOrders.map((order: OrderResponse) => (
                  <div key={order.id} className="flex items-center gap-4 px-4 py-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                    <div className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0" style={{ backgroundColor: "var(--color-surface,#fff)" }}>
                      <Gamepad2 size={18} style={{ color: "var(--color-brand-tertiary,#2C5EF5)" }} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem" }} className="truncate">{order.productName}</p>
                      <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.75rem", marginTop: 1 }}>
                        {new Date(order.orderDate).toLocaleDateString("id-ID", { day: "numeric", month: "short" })}
                      </p>
                    </div>
                    <div className="text-right flex-shrink-0">
                      <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem" }}>{formatFullRp(order.finalPrice)}</p>
                      <StatusBadge status={order.status} />
                    </div>
                  </div>
                ))}
              </div>
            )}
            {totalOrders > 5 && (
              <Link
                to="/history"
                className="flex items-center justify-center gap-2 mt-4 py-3 rounded-xl no-underline transition-colors hover:bg-gray-50"
                style={{ border: "1px solid rgba(0,0,0,0.08)", color: "var(--color-brand-tertiary,#2C5EF5)", fontSize: "0.85rem", fontWeight: 600 }}
              >
                Lihat Semua Riwayat <ChevronRight size={14} />
              </Link>
            )}
          </div>

          {/* Favorite Games */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginBottom: 16 }}>Game Favorit</h3>
            {historyLoading ? (
              <div className="flex gap-3">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="h-16 w-24 rounded-xl animate-pulse" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }} />
                ))}
              </div>
            ) : favoriteGames.length === 0 ? (
              <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.85rem", textAlign: "center", padding: "16px 0" }}>
                Belum ada data. Mulai top up untuk melihat game favorit kamu.
              </p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {favoriteGames.map((game) => (
                  <div
                    key={game.name}
                    className="flex items-center gap-2 px-4 py-2.5 rounded-xl"
                    style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}
                  >
                    <Gamepad2 size={15} style={{ color: "var(--color-brand-tertiary,#2C5EF5)" }} />
                    <span style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.85rem" }}>{game.name}</span>
                    <span className="px-1.5 py-0.5 rounded-full" style={{ backgroundColor: "var(--color-brand-tertiary,#2C5EF5)", color: "#fff", fontSize: "0.7rem", fontWeight: 700 }}>
                      {game.count}x
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Security Section */}
          <div className="rounded-2xl p-5" style={{ backgroundColor: "var(--color-surface,#fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
            <h3 style={{ color: "var(--color-primary,#0F1419)", fontWeight: 600, fontSize: "0.95rem", marginBottom: 16 }}>Keamanan Akun</h3>
            <div className="space-y-3">
              <div className="flex items-center justify-between px-4 py-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ backgroundColor: "var(--color-surface,#fff)", color: "var(--color-brand-secondary,#4A5568)" }}>
                    <Mail size={16} />
                  </div>
                  <div>
                    <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 500, fontSize: "0.9rem" }}>Email</p>
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem" }}>{profile.email}</p>
                  </div>
                </div>
                <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full" style={{ backgroundColor: "#dcfce7", color: "#15803d" }}>
                  <Check size={12} />
                  <span style={{ fontSize: "0.75rem", fontWeight: 600 }}>Terverifikasi</span>
                </div>
              </div>

              {/* <div className="flex items-center justify-between px-4 py-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ backgroundColor: "var(--color-surface,#fff)", color: "var(--color-brand-secondary,#4A5568)" }}>
                    <Lock size={16} />
                  </div>
                  <div>
                    <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 500, fontSize: "0.9rem" }}>Password</p>
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem" }}>Terakhir diperbarui saat edit profil</p>
                  </div>
                </div>
                <button
                  onClick={() => { setForm({ fullName: profile.fullName, newPassword: "" }); setEditing(true); }}
                  className="px-3 py-1.5 rounded-lg"
                  style={{ border: "1px solid rgba(0,0,0,0.1)", color: "var(--color-primary,#0F1419)", fontSize: "0.8rem", fontWeight: 500 }}
                >
                  Ubah
                </button>
              </div> */}

              {/* <div className="flex items-center justify-between px-4 py-3 rounded-xl" style={{ backgroundColor: "var(--color-neutral,#F1F3F5)" }}>
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg flex items-center justify-center" style={{ backgroundColor: "var(--color-surface,#fff)", color: "var(--color-brand-secondary,#4A5568)" }}>
                    <ShieldCheck size={16} />
                  </div>
                  <div>
                    <p style={{ color: "var(--color-primary,#0F1419)", fontWeight: 500, fontSize: "0.9rem" }}>Sesi Login</p>
                    <p style={{ color: "var(--color-brand-secondary,#4A5568)", fontSize: "0.8rem" }}>JWT aktif, expires dalam 24 jam</p>
                  </div>
                </div>
                <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full" style={{ backgroundColor: "#dcfce7", color: "#15803d" }}>
                  <span style={{ fontSize: "0.75rem", fontWeight: 600 }}>Aktif</span>
                </div>
              </div> */}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
