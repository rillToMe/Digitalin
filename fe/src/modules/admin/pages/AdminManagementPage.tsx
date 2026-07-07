import { useState } from "react";
import { Plus, Pencil, Key, Trash2, X, Loader2, ShieldCheck, Shield, Eye, EyeOff } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { adminManagementService } from "../../../services/api";
import type { AdminListItem, AdminRole, CreateAdminPayload } from "../../../api/index";

// ── Helpers ────────────────────────────────────────────────────────────────────

function formatDateTime(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("id-ID", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

const ROLE_STYLES: Record<AdminRole, { bg: string; text: string; icon: typeof Shield }> = {
  SUPER_ADMIN: {
    bg: "rgba(44,94,245,0.12)",
    text: "var(--color-brand-tertiary, #2C5EF5)",
    icon: ShieldCheck,
  },
  ADMIN: {
    bg: "rgba(0,0,0,0.06)",
    text: "var(--color-brand-secondary, #4A5568)",
    icon: Shield,
  },
};

// ── Error helper ───────────────────────────────────────────────────────────────
type ApiError = { response?: { data?: { message?: string } } };
function errMsg(err: ApiError, fallback: string): string {
  return err?.response?.data?.message ?? fallback;
}

// ── Sub-components (modals) ────────────────────────────────────────────────────

interface ModalShellProps {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
}
function ModalShell({ title, onClose, children }: ModalShellProps) {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      style={{ backgroundColor: "rgba(0,0,0,0.4)" }}
    >
      <div
        className="w-full max-w-md rounded-2xl p-6"
        style={{ backgroundColor: "var(--color-surface, #fff)", maxHeight: "90vh", overflowY: "auto" }}
      >
        <div className="flex items-center justify-between mb-5">
          <h3 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1rem" }}>
            {title}
          </h3>
          <button onClick={onClose} style={{ color: "var(--color-brand-secondary)" }}>
            <X size={20} />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

interface FieldProps {
  label: string;
  children: React.ReactNode;
}
function Field({ label, children }: FieldProps) {
  return (
    <div>
      <label
        style={{
          color: "var(--color-brand-primary)",
          fontSize: "0.85rem",
          fontWeight: 500,
          display: "block",
          marginBottom: 5,
        }}
      >
        {label}
      </label>
      {children}
    </div>
  );
}

const inputStyle: React.CSSProperties = {
  border: "1.5px solid var(--color-border-input, rgba(0,0,0,0.15))",
  fontSize: "0.9rem",
  color: "var(--color-brand-primary)",
  backgroundColor: "var(--color-surface)",
};

// ── Create Admin Modal ─────────────────────────────────────────────────────────

interface CreateAdminModalProps {
  onClose: () => void;
}
function CreateAdminModal({ onClose }: CreateAdminModalProps) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState<CreateAdminPayload>({
    username: "",
    password: "",
    role: "ADMIN",
  });
  const [showPass, setShowPass] = useState(false);

  const mutation = useMutation({
    mutationFn: (payload: CreateAdminPayload) => adminManagementService.create(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-management-list"] });
      toast.success("Admin berhasil dibuat.");
      onClose();
    },
    onError: (err: ApiError) => toast.error(errMsg(err, "Gagal membuat admin.")),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.username.trim()) { toast.error("Username wajib diisi."); return; }
    if (!form.password.trim()) { toast.error("Password wajib diisi."); return; }
    mutation.mutate(form);
  };

  return (
    <ModalShell title="Tambah Admin" onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label="Username *">
          <input
            value={form.username}
            onChange={(e) => setForm({ ...form, username: e.target.value })}
            placeholder="admin1"
            className="w-full px-4 py-2.5 rounded-xl outline-none"
            style={inputStyle}
            autoFocus
          />
        </Field>

        <Field label="Password *">
          <div className="relative">
            <input
              type={showPass ? "text" : "password"}
              value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })}
              placeholder="••••••••"
              className="w-full px-4 py-2.5 pr-11 rounded-xl outline-none"
              style={inputStyle}
            />
            <button
              type="button"
              onClick={() => setShowPass((v) => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2"
              style={{ color: "var(--color-brand-secondary)" }}
            >
              {showPass ? <EyeOff size={17} /> : <Eye size={17} />}
            </button>
          </div>
        </Field>

        <Field label="Role *">
          <div className="flex gap-2">
            {(["ADMIN", "SUPER_ADMIN"] as AdminRole[]).map((r) => {
              const active = form.role === r;
              return (
                <button
                  key={r}
                  type="button"
                  onClick={() => setForm({ ...form, role: r })}
                  className="flex-1 py-2.5 rounded-xl transition-all"
                  style={{
                    backgroundColor: active ? "var(--color-brand-tertiary)" : "var(--color-surface)",
                    color: active ? "#fff" : "var(--color-brand-primary)",
                    border: active ? "none" : "1.5px solid var(--color-border-input, rgba(0,0,0,0.15))",
                    fontWeight: 500,
                    fontSize: "0.85rem",
                  }}
                >
                  {r}
                </button>
              );
            })}
          </div>
        </Field>

        <button
          type="submit"
          disabled={mutation.isPending}
          className="w-full py-3 rounded-xl flex items-center justify-center gap-2 mt-2"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "#fff",
            fontWeight: 600,
            fontSize: "0.9rem",
            opacity: mutation.isPending ? 0.7 : 1,
          }}
        >
          {mutation.isPending && <Loader2 size={15} className="animate-spin" />}
          {mutation.isPending ? "Menyimpan..." : "Tambah Admin"}
        </button>
      </form>
    </ModalShell>
  );
}

// ── Edit Role Modal ────────────────────────────────────────────────────────────

interface EditRoleModalProps {
  admin: AdminListItem;
  onClose: () => void;
}
function EditRoleModal({ admin, onClose }: EditRoleModalProps) {
  const queryClient = useQueryClient();
  const [role, setRole] = useState<AdminRole>(admin.role);

  const mutation = useMutation({
    mutationFn: (r: AdminRole) => adminManagementService.updateRole(admin.id, { role: r }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-management-list"] });
      toast.success("Role berhasil diperbarui.");
      onClose();
    },
    onError: (err: ApiError) => toast.error(errMsg(err, "Gagal mengubah role.")),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    mutation.mutate(role);
  };

  return (
    <ModalShell title={`Ubah Role — ${admin.username}`} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label="Role *">
          <div className="flex gap-2">
            {(["ADMIN", "SUPER_ADMIN"] as AdminRole[]).map((r) => {
              const active = role === r;
              return (
                <button
                  key={r}
                  type="button"
                  onClick={() => setRole(r)}
                  className="flex-1 py-2.5 rounded-xl transition-all"
                  style={{
                    backgroundColor: active ? "var(--color-brand-tertiary)" : "var(--color-surface)",
                    color: active ? "#fff" : "var(--color-brand-primary)",
                    border: active ? "none" : "1.5px solid var(--color-border-input, rgba(0,0,0,0.15))",
                    fontWeight: 500,
                    fontSize: "0.85rem",
                  }}
                >
                  {r}
                </button>
              );
            })}
          </div>
        </Field>

        <button
          type="submit"
          disabled={mutation.isPending}
          className="w-full py-3 rounded-xl flex items-center justify-center gap-2 mt-2"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "#fff",
            fontWeight: 600,
            fontSize: "0.9rem",
            opacity: mutation.isPending ? 0.7 : 1,
          }}
        >
          {mutation.isPending && <Loader2 size={15} className="animate-spin" />}
          {mutation.isPending ? "Menyimpan..." : "Simpan Role"}
        </button>
      </form>
    </ModalShell>
  );
}

// ── Reset Password Modal ───────────────────────────────────────────────────────

interface ResetPasswordModalProps {
  admin: AdminListItem;
  onClose: () => void;
}
function ResetPasswordModal({ admin, onClose }: ResetPasswordModalProps) {
  const queryClient = useQueryClient();
  const [newPassword, setNewPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);

  const mutation = useMutation({
    mutationFn: (pwd: string) =>
      adminManagementService.resetPassword(admin.id, { newPassword: pwd }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-management-list"] });
      toast.success("Password berhasil diperbarui.");
      onClose();
    },
    onError: (err: ApiError) => toast.error(errMsg(err, "Gagal mereset password.")),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newPassword.trim()) { toast.error("Password baru wajib diisi."); return; }
    if (newPassword !== confirm) { toast.error("Konfirmasi password tidak cocok."); return; }
    mutation.mutate(newPassword);
  };

  return (
    <ModalShell title={`Reset Password — ${admin.username}`} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label="Password Baru *">
          <div className="relative">
            <input
              type={showNew ? "text" : "password"}
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full px-4 py-2.5 pr-11 rounded-xl outline-none"
              style={inputStyle}
              autoFocus
            />
            <button
              type="button"
              onClick={() => setShowNew((v) => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2"
              style={{ color: "var(--color-brand-secondary)" }}
            >
              {showNew ? <EyeOff size={17} /> : <Eye size={17} />}
            </button>
          </div>
        </Field>

        <Field label="Konfirmasi Password *">
          <div className="relative">
            <input
              type={showConfirm ? "text" : "password"}
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              placeholder="••••••••"
              className="w-full px-4 py-2.5 pr-11 rounded-xl outline-none"
              style={inputStyle}
            />
            <button
              type="button"
              onClick={() => setShowConfirm((v) => !v)}
              className="absolute right-3 top-1/2 -translate-y-1/2"
              style={{ color: "var(--color-brand-secondary)" }}
            >
              {showConfirm ? <EyeOff size={17} /> : <Eye size={17} />}
            </button>
          </div>
        </Field>

        <button
          type="submit"
          disabled={mutation.isPending}
          className="w-full py-3 rounded-xl flex items-center justify-center gap-2 mt-2"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "#fff",
            fontWeight: 600,
            fontSize: "0.9rem",
            opacity: mutation.isPending ? 0.7 : 1,
          }}
        >
          {mutation.isPending && <Loader2 size={15} className="animate-spin" />}
          {mutation.isPending ? "Menyimpan..." : "Reset Password"}
        </button>
      </form>
    </ModalShell>
  );
}

// ── Delete Confirmation Modal ──────────────────────────────────────────────────

interface DeleteConfirmModalProps {
  admin: AdminListItem;
  onClose: () => void;
}
function DeleteConfirmModal({ admin, onClose }: DeleteConfirmModalProps) {
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: () => adminManagementService.deleteAdmin(admin.id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["admin-management-list"] });
      toast.success("Admin berhasil dihapus.");
      onClose();
    },
    onError: (err: ApiError) => toast.error(errMsg(err, "Gagal menghapus admin.")),
  });

  return (
    <ModalShell title="Hapus Admin" onClose={onClose}>
      <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.9rem", marginBottom: 20 }}>
        Apakah Anda yakin ingin menghapus admin{" "}
        <strong style={{ color: "var(--color-brand-primary)" }}>{admin.username}</strong>?
        Tindakan ini tidak dapat dibatalkan.
      </p>
      <div className="flex gap-3">
        <button
          onClick={onClose}
          className="flex-1 py-2.5 rounded-xl"
          style={{
            border: "1.5px solid var(--color-border-input, rgba(0,0,0,0.15))",
            color: "var(--color-brand-primary)",
            fontWeight: 500,
            fontSize: "0.9rem",
          }}
        >
          Batal
        </button>
        <button
          onClick={() => mutation.mutate()}
          disabled={mutation.isPending}
          className="flex-1 py-2.5 rounded-xl flex items-center justify-center gap-2"
          style={{
            backgroundColor: "var(--color-danger, #ef4444)",
            color: "#fff",
            fontWeight: 600,
            fontSize: "0.9rem",
            opacity: mutation.isPending ? 0.7 : 1,
          }}
        >
          {mutation.isPending && <Loader2 size={15} className="animate-spin" />}
          {mutation.isPending ? "Menghapus..." : "Hapus"}
        </button>
      </div>
    </ModalShell>
  );
}

// ── Main Page ──────────────────────────────────────────────────────────────────

type ActiveModal =
  | { type: "create" }
  | { type: "edit-role"; admin: AdminListItem }
  | { type: "reset-password"; admin: AdminListItem }
  | { type: "delete"; admin: AdminListItem }
  | null;

export default function AdminManagementPage() {
  const [activeModal, setActiveModal] = useState<ActiveModal>(null);

  const { data: admins = [], isLoading } = useQuery({
    queryKey: ["admin-management-list"],
    queryFn: () => adminManagementService.getAll(),
  });

  const closeModal = () => setActiveModal(null);

  return (
    <AdminLayout title="Kelola Admin" breadcrumb={["Admin", "Kelola Admin"]}>
      {/* Toolbar */}
      <div className="flex items-center justify-between mb-5">
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
          {admins.length} admin terdaftar
        </p>
        <button
          onClick={() => setActiveModal({ type: "create" })}
          className="flex items-center gap-2 px-4 py-2.5 rounded-xl"
          style={{
            backgroundColor: "var(--color-brand-tertiary)",
            color: "#fff",
            fontWeight: 600,
            fontSize: "0.9rem",
            whiteSpace: "nowrap",
          }}
        >
          <Plus size={16} />
          Tambah Admin
        </button>
      </div>

      {/* Table */}
      <div
        className="rounded-2xl overflow-hidden"
        style={{
          backgroundColor: "var(--color-surface)",
          border: "1px solid var(--color-border-light, rgba(0,0,0,0.06))",
        }}
      >
        <div className="overflow-x-auto">
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr
                style={{
                  borderBottom: "1px solid var(--color-border-light, rgba(0,0,0,0.06))",
                  backgroundColor: "var(--color-neutral, #F1F3F5)",
                }}
              >
                {["Username", "Role", "Dibuat", "Login Terakhir", "Aksi"].map((h) => (
                  <th
                    key={h}
                    className="px-5 py-3 text-left"
                    style={{
                      color: "var(--color-brand-secondary)",
                      fontSize: "0.75rem",
                      fontWeight: 600,
                      textTransform: "uppercase",
                      letterSpacing: "0.04em",
                    }}
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {isLoading
                ? Array.from({ length: 4 }).map((_, i) => (
                    <tr key={i}>
                      {Array.from({ length: 5 }).map((__, j) => (
                        <td key={j} className="px-5 py-4">
                          <div
                            className="h-4 rounded animate-pulse"
                            style={{ backgroundColor: "var(--color-skeleton, #e5e7eb)" }}
                          />
                        </td>
                      ))}
                    </tr>
                  ))
                : admins.map((a) => {
                    const roleStyle = ROLE_STYLES[a.role];
                    const RoleIcon = roleStyle.icon;
                    return (
                      <tr
                        key={a.id}
                        style={{
                          borderBottom: "1px solid var(--color-border-lighter, rgba(0,0,0,0.04))",
                        }}
                      >
                        {/* Username */}
                        <td className="px-5 py-4">
                          <span
                            style={{
                              fontWeight: 600,
                              fontSize: "0.9rem",
                              color: "var(--color-brand-primary)",
                              fontFamily: "monospace",
                            }}
                          >
                            {a.username}
                          </span>
                        </td>

                        {/* Role badge */}
                        <td className="px-5 py-4">
                          <span
                            className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full"
                            style={{
                              backgroundColor: roleStyle.bg,
                              color: roleStyle.text,
                              fontSize: "0.72rem",
                              fontWeight: 600,
                            }}
                          >
                            <RoleIcon size={11} />
                            {a.role}
                          </span>
                        </td>

                        {/* Created */}
                        <td className="px-5 py-4" style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
                          {formatDateTime(a.createdAt)}
                        </td>

                        {/* Last login */}
                        <td className="px-5 py-4" style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}>
                          {formatDateTime(a.lastLoginAt)}
                        </td>

                        {/* Actions */}
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-1">
                            {/* Edit role */}
                            <button
                              onClick={() => setActiveModal({ type: "edit-role", admin: a })}
                              className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                              style={{ color: "var(--color-brand-tertiary)" }}
                              onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = "var(--color-hover-info, rgba(44,94,245,0.08))";
                              }}
                              onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = "transparent";
                              }}
                              title="Ubah Role"
                            >
                              <Pencil size={14} />
                            </button>

                            {/* Reset password */}
                            <button
                              onClick={() => setActiveModal({ type: "reset-password", admin: a })}
                              className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                              style={{ color: "var(--color-warning, #d97706)" }}
                              onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = "var(--color-warning-bg, rgba(217,119,6,0.08))";
                              }}
                              onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = "transparent";
                              }}
                              title="Reset Password"
                            >
                              <Key size={14} />
                            </button>

                            {/* Delete */}
                            <button
                              onClick={() => setActiveModal({ type: "delete", admin: a })}
                              className="w-8 h-8 rounded-lg flex items-center justify-center transition-colors"
                              style={{ color: "var(--color-danger, #ef4444)" }}
                              onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = "var(--color-hover-danger, rgba(239,68,68,0.08))";
                              }}
                              onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = "transparent";
                              }}
                              title="Hapus"
                            >
                              <Trash2 size={14} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
              {!isLoading && admins.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-5 py-12 text-center" style={{ color: "var(--color-brand-secondary)" }}>
                    Tidak ada admin ditemukan.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modals */}
      {activeModal?.type === "create" && <CreateAdminModal onClose={closeModal} />}
      {activeModal?.type === "edit-role" && (
        <EditRoleModal admin={activeModal.admin} onClose={closeModal} />
      )}
      {activeModal?.type === "reset-password" && (
        <ResetPasswordModal admin={activeModal.admin} onClose={closeModal} />
      )}
      {activeModal?.type === "delete" && (
        <DeleteConfirmModal admin={activeModal.admin} onClose={closeModal} />
      )}
    </AdminLayout>
  );
}
