import { useRef, useState } from "react";
import {
  Check,
  Landmark,
  Loader2,
  Pencil,
  RefreshCw,
  Upload,
  X,
} from "lucide-react";
import { AdminLayout } from "../../../layouts/AdminLayout";
import { usePaymentMethodAdmin } from "../../../hooks/settings/usePaymentMethodAdmin";
import { ImageWithFallback } from "../../../utils/ImageWithFallback";
import type { AdminPaymentBankInfo, AdminPaymentMethodInfo } from "../../../api/index";

/**
 * Halaman khusus kelola metode pembayaran & bank VA: ikon (upload →
 * Cloudflare R2 via backend), nama tampilan, aktif/nonaktif. Urutan tampil
 * mengikuti sortOrder backend. Katalog tetap fixed-seed — halaman ini
 * mengedit, bukan menambah/menghapus.
 */

type EditingTarget =
  | { kind: "method"; code: string; name: string }
  | { kind: "bank"; methodCode: string; code: string; name: string };

/** Kartu satu item (metode/bank): ikon, nama (edit inline), toggle, upload. */
function ConfigCard({
  name,
  iconUrl,
  enabled,
  isUploading,
  isToggling,
  isRenaming,
  onToggle,
  onUploadIcon,
  onRename,
}: {
  name: string;
  iconUrl: string | null;
  enabled: boolean;
  isUploading: boolean;
  isToggling: boolean;
  isRenaming: boolean;
  onToggle: () => void;
  onUploadIcon: (file: File) => void;
  onRename: (newName: string) => void;
}) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(name);

  const startEdit = () => {
    setDraft(name);
    setEditing(true);
  };

  const submitRename = () => {
    const trimmed = draft.trim();
    if (!trimmed || trimmed === name) {
      setEditing(false);
      return;
    }
    onRename(trimmed);
    setEditing(false);
  };

  return (
    <div
      className="rounded-2xl p-4 flex flex-col gap-3"
      style={{
        backgroundColor: "var(--color-surface)",
        border: `1px solid ${enabled ? "var(--color-border-light)" : "var(--color-border-default)"}`,
        opacity: enabled ? 1 : 0.72,
      }}
    >
      {/* Baris ikon + status */}
      <div className="flex items-center gap-3">
        {iconUrl ? (
          <ImageWithFallback
            src={iconUrl}
            alt={name}
            className="h-9 w-auto max-w-[72px] object-contain flex-shrink-0"
          />
        ) : (
          <span
            className="flex h-9 min-w-9 items-center justify-center rounded-lg px-2 label-mono flex-shrink-0"
            style={{
              backgroundColor: "var(--color-surface-raised)",
              border: "1px solid var(--color-border-default)",
              color: "var(--color-brand-secondary)",
              fontSize: "0.65rem",
            }}
          >
            {name.slice(0, 4).toUpperCase()}
          </span>
        )}

        <span
          className="px-2 py-0.5 rounded-full flex-shrink-0"
          style={{
            backgroundColor: enabled ? "var(--color-success-bg)" : "var(--color-neutral)",
            color: enabled ? "var(--color-success)" : "var(--color-brand-secondary)",
            fontSize: "0.68rem",
            fontWeight: 600,
          }}
        >
          {enabled ? "Aktif" : "Nonaktif"}
        </span>
      </div>

      {/* Nama — edit inline */}
      {editing ? (
        <div className="flex items-center gap-2">
          <input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") submitRename();
              if (e.key === "Escape") setEditing(false);
            }}
            autoFocus
            maxLength={100}
            className="flex-1 min-w-0 px-3 py-2 rounded-lg outline-none"
            style={{
              border: "1.5px solid var(--color-brand-tertiary)",
              color: "var(--color-brand-primary)",
              fontSize: "0.88rem",
              backgroundColor: "var(--color-surface)",
            }}
            aria-label="Nama baru"
          />
          <button
            type="button"
            onClick={submitRename}
            disabled={isRenaming}
            aria-label="Simpan nama"
            className="w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{
              backgroundColor: "var(--color-brand-tertiary)",
              color: "var(--color-on-primary)",
            }}
          >
            {isRenaming ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <Check size={14} />
            )}
          </button>
          <button
            type="button"
            onClick={() => setEditing(false)}
            aria-label="Batal"
            className="w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{
              border: "1px solid var(--color-border-default)",
              color: "var(--color-brand-secondary)",
            }}
          >
            <X size={14} />
          </button>
        </div>
      ) : (
        <div className="flex items-center justify-between gap-2">
          <p
            className="truncate"
            style={{
              color: "var(--color-brand-primary)",
              fontWeight: 600,
              fontSize: "0.9rem",
            }}
          >
            {name}
          </p>
          <button
            type="button"
            onClick={startEdit}
            aria-label={`Ubah nama ${name}`}
            className="w-8 h-8 rounded-lg flex items-center justify-center flex-shrink-0"
            style={{
              border: "1px solid var(--color-border-default)",
              color: "var(--color-brand-secondary)",
            }}
          >
            <Pencil size={13} />
          </button>
        </div>
      )}

      {/* Aksi */}
      <div className="flex items-center gap-2">
        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          className="hidden"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) onUploadIcon(file);
            e.target.value = ""; // reset supaya file sama bisa di-upload ulang
          }}
        />
        <button
          type="button"
          onClick={() => fileInputRef.current?.click()}
          disabled={isUploading}
          className="flex-1 flex items-center justify-center gap-1.5 py-2 rounded-lg text-xs font-semibold"
          style={{
            border: "1px solid var(--color-border-default)",
            color: "var(--color-brand-primary)",
          }}
        >
          {isUploading ? (
            <Loader2 size={13} className="animate-spin" />
          ) : (
            <Upload size={13} />
          )}
          Upload Ikon
        </button>
        <button
          type="button"
          onClick={onToggle}
          disabled={isToggling}
          className="flex-1 py-2 rounded-lg text-xs font-semibold"
          style={{
            border: "1px solid var(--color-border-default)",
            color: enabled ? "var(--color-danger)" : "var(--color-success)",
          }}
        >
          {isToggling ? "…" : enabled ? "Nonaktifkan" : "Aktifkan"}
        </button>
      </div>
    </div>
  );
}

export default function PaymentMethodsPage() {
  const {
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
  } = usePaymentMethodAdmin();

  const rename = (target: EditingTarget, newName: string) => {
    if (target.kind === "method") {
      renameMethod.mutate({ code: target.code, displayName: newName });
    } else {
      renameBank.mutate({
        code: target.methodCode,
        bankCode: target.code,
        displayName: newName,
      });
    }
  };

  const renderCard = (
    target: EditingTarget,
    info: { iconUrl: string | null; enabled: boolean },
  ) => (
    <ConfigCard
      key={target.kind === "method" ? target.code : `${target.methodCode}-${target.code}`}
      name={target.name}
      iconUrl={info.iconUrl}
      enabled={info.enabled}
      isUploading={uploadMethodIcon.isPending || uploadBankIcon.isPending}
      isToggling={toggleMethod.isPending || toggleBank.isPending}
      isRenaming={renameMethod.isPending || renameBank.isPending}
      onToggle={() => {
        if (target.kind === "method") {
          toggleMethod.mutate({ code: target.code, enabled: !info.enabled });
        } else {
          toggleBank.mutate({
            code: target.methodCode,
            bankCode: target.code,
            enabled: !info.enabled,
          });
        }
      }}
      onUploadIcon={(file) => {
        if (target.kind === "method") {
          uploadMethodIcon.mutate({ code: target.code, file });
        } else {
          uploadBankIcon.mutate({
            code: target.methodCode,
            bankCode: target.code,
            file,
          });
        }
      }}
      onRename={(newName) => rename(target, newName)}
    />
  );

  return (
    <AdminLayout
      title="Metode Pembayaran"
      breadcrumb={["Admin", "Metode Pembayaran"]}
    >
      <p
        className="mb-5"
        style={{ color: "var(--color-brand-secondary)", fontSize: "0.85rem" }}
      >
        Kelola tampilan metode pembayaran dan bank VA di checkout: ikon
        (tersimpan di Cloudflare R2), nama tampilan, dan status aktif. Item
        nonaktif tidak muncul di checkout dan ordernya ditolak backend.
      </p>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="rounded-2xl h-44 animate-pulse"
              style={{ backgroundColor: "var(--color-surface)" }}
            />
          ))}
        </div>
      ) : isError ? (
        <div className="py-12 text-center space-y-3">
          <p
            style={{
              color: "var(--color-brand-primary)",
              fontWeight: 600,
              fontSize: "0.95rem",
            }}
          >
            Gagal memuat konfigurasi pembayaran.
          </p>
          <button
            onClick={() => refetch()}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold"
            style={{
              border: "1px solid var(--color-border-default)",
              color: "var(--color-brand-primary)",
            }}
          >
            <RefreshCw size={12} /> Coba Lagi
          </button>
        </div>
      ) : (
        <div className="space-y-8">
          {/* Metode pembayaran */}
          <section>
            <h2
              style={{
                color: "var(--color-brand-primary)",
                fontWeight: 700,
                fontSize: "1rem",
                marginBottom: 12,
              }}
            >
              Metode Pembayaran
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {methods
                ?.filter(
                  (m: AdminPaymentMethodInfo) =>
                    m.code !== "bank_transfer" && m.banks.length === 0,
                )
                .map((m) =>
                  renderCard(
                    { kind: "method", code: m.code, name: m.name },
                    { iconUrl: m.iconUrl, enabled: m.enabled },
                  ),
                )}
            </div>
          </section>

          {/* Metode ber-sub-kanal (bank VA / toko cstore) */}
          <section>
            <div className="flex items-center gap-2 mb-3">
              <Landmark
                size={16}
                style={{ color: "var(--color-brand-tertiary)" }}
                aria-hidden="true"
              />
              <h2
                style={{
                  color: "var(--color-brand-primary)",
                  fontWeight: 700,
                  fontSize: "1rem",
                }}
              >
                Sub-Kanal (Bank VA & Toko)
              </h2>
            </div>

            <div className="space-y-8">
              {methods
                ?.filter(
                  (m: AdminPaymentMethodInfo) => m.banks.length > 0,
                )
                .map((m) => (
                  <div key={m.code} className="space-y-4">
                    <div className="flex items-center gap-2">
                      <h3
                        style={{
                          color: "var(--color-brand-primary)",
                          fontWeight: 600,
                          fontSize: "0.9rem",
                        }}
                      >
                        {m.name}
                      </h3>
                      {!m.enabled && (
                        <span
                          className="px-2 py-0.5 rounded-full"
                          style={{
                            backgroundColor: "var(--color-warning-bg)",
                            color: "var(--color-warning)",
                            fontSize: "0.68rem",
                            fontWeight: 600,
                          }}
                        >
                          {m.name} nonaktif — sub-kanal tidak muncul di checkout
                        </span>
                      )}
                    </div>

                    {renderCard(
                      { kind: "method", code: m.code, name: m.name },
                      { iconUrl: m.iconUrl, enabled: m.enabled },
                    )}

                    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                      {m.banks.map((bank: AdminPaymentBankInfo) =>
                        renderCard(
                          {
                            kind: "bank",
                            methodCode: m.code,
                            code: bank.code,
                            name: bank.name,
                          },
                          { iconUrl: bank.iconUrl, enabled: bank.enabled },
                        ),
                      )}
                    </div>
                  </div>
                ))}
            </div>
          </section>
        </div>
      )}
    </AdminLayout>
  );
}
