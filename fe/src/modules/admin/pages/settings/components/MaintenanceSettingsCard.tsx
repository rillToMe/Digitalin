import { Save, Loader2, RefreshCw, AlertTriangle, WrenchIcon } from "lucide-react";
import { useMaintenanceSettings } from "../hooks/useMaintenanceSettings";
import { SettingSectionHeader } from "./SettingSectionHeader";
import { SettingToggle } from "./SettingToggle";

export function MaintenanceSettingsCard() {
  const {
    maintEnabled,
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
    isSavingMaint,
    isEstimatedInPast,
    getNowLocalString,
    handleSaveMaintenance,
    handleToggleMaintenance,
  } = useMaintenanceSettings();

  const isSubmitDisabled = isSavingMaint || isEstimatedInPast;

  const headerRightElement = (
    <div className="flex items-center gap-3">
      <span
        className="px-2.5 py-1 rounded-full text-[0.72rem] font-bold"
        style={
          maintEnabled
            ? {
              backgroundColor: "rgba(245,158,11,0.12)",
              color: "var(--color-warning, #f59e0b)",
            }
            : {
              backgroundColor: "rgba(16,185,129,0.1)",
              color: "var(--color-success, #10b981)",
            }
        }
      >
        {maintEnabled ? "AKTIF" : "NONAKTIF"}
      </span>

      <SettingToggle
        checked={maintEnabled}
        onChange={handleToggleMaintenance}
        disabled={maintLoading}
        isLoading={isSavingMaint}
        activeColor="var(--color-warning, #f59e0b)"
        size="md"
        title={maintEnabled ? "Nonaktifkan Maintenance" : "Aktifkan Maintenance"}
      />
    </div>
  );

  return (
    <div
      className="rounded-2xl p-6"
      style={{
        backgroundColor: "var(--color-surface)",
        border: maintEnabled
          ? "1.5px solid var(--color-warning, #f59e0b)"
          : "1px solid var(--color-border-light)",
      }}
    >
      <SettingSectionHeader
        icon={WrenchIcon}
        title="Maintenance Mode"
        iconColor={maintEnabled ? "var(--color-warning, #f59e0b)" : "var(--color-brand-secondary)"}
        rightElement={headerRightElement}
      />

      {maintEnabled && (
        <div
          className="flex items-start gap-3 p-3 rounded-xl mb-4"
          style={{
            backgroundColor: "rgba(245,158,11,0.08)",
            border: "1px solid rgba(245,158,11,0.3)",
          }}
        >
          <AlertTriangle
            size={16}
            style={{
              color: "var(--color-warning, #f59e0b)",
              flexShrink: 0,
              marginTop: 1,
            }}
          />
          <p
            style={{
              color: "var(--color-warning, #f59e0b)",
              fontSize: "0.8rem",
              fontWeight: 500,
            }}
          >
            Maintenance Mode sedang <strong>AKTIF</strong>. Semua user diarahkan ke halaman maintenance. Admin masih dapat mengakses panel ini.
          </p>
        </div>
      )}

      {maintError ? (
        <div
          className="p-5 rounded-xl text-center space-y-3"
          style={{ backgroundColor: "var(--color-danger-bg)" }}
        >
          <AlertTriangle
            size={32}
            style={{ color: "var(--color-danger)", margin: "0 auto" }}
          />
          <p
            style={{
              color: "var(--color-danger)",
              fontSize: "0.85rem",
              fontWeight: 500,
            }}
          >
            Gagal memuat konfigurasi maintenance.
          </p>
          <button
            onClick={() => refetchMaint()}
            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-xl text-xs font-semibold"
            style={{
              backgroundColor: "var(--color-surface)",
              border: "1px solid var(--color-border)",
              color: "var(--color-brand-primary)",
            }}
          >
            <RefreshCw size={12} /> Coba Lagi
          </button>
        </div>
      ) : maintLoading ? (
        <div className="py-8 flex flex-col items-center justify-center gap-2">
          <Loader2
            size={24}
            className="animate-spin"
            style={{ color: "var(--color-brand-tertiary)" }}
          />
          <span style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem" }}>
            Memuat konfigurasi...
          </span>
        </div>
      ) : (
        <form onSubmit={handleSaveMaintenance} className="space-y-4">
          <div>
            <label
              style={{
                color: "var(--color-brand-primary)",
                fontSize: "0.85rem",
                fontWeight: 500,
                display: "block",
                marginBottom: 6,
              }}
            >
              Judul Maintenance
            </label>
            <input
              type="text"
              value={maintTitle}
              onChange={(e) => setMaintTitle(e.target.value)}
              disabled={isSavingMaint}
              maxLength={200}
              placeholder="Website Sedang Maintenance"
              className="w-full px-4 py-2.5 rounded-xl outline-none transition-all"
              style={{
                border: "1.5px solid var(--color-border-input)",
                fontSize: "0.88rem",
                color: "var(--color-brand-primary)",
                backgroundColor: "var(--color-surface)",
              }}
              onFocus={(e) => (e.target.style.borderColor = "var(--color-brand-tertiary)")}
              onBlur={(e) => (e.target.style.borderColor = "var(--color-border-input)")}
            />
          </div>

          <div>
            <label
              style={{
                color: "var(--color-brand-primary)",
                fontSize: "0.85rem",
                fontWeight: 500,
                display: "block",
                marginBottom: 6,
              }}
            >
              Pesan untuk User
            </label>
            <textarea
              value={maintMessage}
              onChange={(e) => setMaintMessage(e.target.value)}
              disabled={isSavingMaint}
              maxLength={1000}
              rows={3}
              placeholder="Kami sedang melakukan peningkatan sistem..."
              className="w-full px-4 py-2.5 rounded-xl outline-none transition-all resize-none"
              style={{
                border: "1.5px solid var(--color-border-input)",
                fontSize: "0.88rem",
                color: "var(--color-brand-primary)",
                backgroundColor: "var(--color-surface)",
              }}
              onFocus={(e) => (e.target.style.borderColor = "var(--color-brand-tertiary)")}
              onBlur={(e) => (e.target.style.borderColor = "var(--color-border-input)")}
            />
          </div>

          <div>
            <label
              style={{
                color: isEstimatedInPast ? "var(--color-danger)" : "var(--color-brand-primary)",
                fontSize: "0.85rem",
                fontWeight: 500,
                display: "block",
                marginBottom: 6,
              }}
            >
              Estimasi Selesai <span style={{ color: "var(--color-brand-secondary)", fontWeight: 400 }}>(opsional)</span>
            </label>
            <input
              type="datetime-local"
              value={maintEstimated}
              min={getNowLocalString()}
              onChange={(e) => setMaintEstimated(e.target.value)}
              disabled={isSavingMaint}
              className="px-4 py-2.5 rounded-xl outline-none transition-all"
              style={{
                border: isEstimatedInPast ? "1.5px solid var(--color-danger)" : "1.5px solid var(--color-border-input)",
                fontSize: "0.88rem",
                color: "var(--color-brand-primary)",
                backgroundColor: "var(--color-surface)",
              }}
              onFocus={(e) => (e.target.style.borderColor = isEstimatedInPast ? "var(--color-danger)" : "var(--color-brand-tertiary)")}
              onBlur={(e) => (e.target.style.borderColor = isEstimatedInPast ? "var(--color-danger)" : "var(--color-border-input)")}
            />
            {isEstimatedInPast ? (
              <p
                style={{
                  color: "var(--color-danger)",
                  fontSize: "0.78rem",
                  marginTop: 4,
                  fontWeight: 500,
                }}
              >
                Estimasi selesai harus berada di masa depan.
              </p>
            ) : (
              <p
                style={{
                  color: "var(--color-brand-secondary)",
                  fontSize: "0.78rem",
                  marginTop: 4,
                }}
              >
                Masukkan estimasi kapan maintenance akan selesai. Tanggal dan waktu harus berada setelah waktu saat ini. Kosongkan jika belum diketahui.
              </p>
            )}
          </div>

          <div className="flex items-center gap-3">
            <SettingToggle
              checked={maintAllowAdmin}
              onChange={() => setMaintAllowAdmin(!maintAllowAdmin)}
              disabled={isSavingMaint}
            />
            <div>
              <span
                style={{
                  color: "var(--color-brand-primary)",
                  fontSize: "0.85rem",
                  fontWeight: 500,
                }}
              >
                Admin tetap dapat mengakses website
              </span>
              <p
                style={{
                  color: "var(--color-brand-secondary)",
                  fontSize: "0.78rem",
                  marginTop: 2,
                }}
              >
                Jika aktif, maintenance mode tidak berlaku untuk request dari admin.
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3 pt-2 flex-wrap">
            <button
              type="submit"
              disabled={isSubmitDisabled}
              title={isEstimatedInPast ? "Estimasi selesai harus berada di masa depan" : undefined}
              className="px-5 py-2.5 rounded-xl flex items-center justify-center gap-2 text-white font-semibold transition-opacity hover:opacity-90 disabled:opacity-50"
              style={{ backgroundColor: "var(--color-brand-tertiary)" }}
            >
              {isSavingMaint ? <Loader2 size={15} className="animate-spin" /> : <Save size={15} />}
              {isSavingMaint ? "Menyimpan..." : "Simpan Konfigurasi"}
            </button>

            {/* <button
              type="button"
              onClick={handleToggleMaintenance}
              disabled={isSavingMaint}
              className="px-5 py-2.5 rounded-xl flex items-center justify-center gap-2 font-semibold transition-opacity hover:opacity-90 disabled:opacity-50"
              style={
                maintEnabled
                  ? {
                      backgroundColor: "rgba(16,185,129,0.1)",
                      color: "var(--color-success, #10b981)",
                      border: "1px solid var(--color-success, #10b981)",
                    }
                  : {
                      backgroundColor: "rgba(245,158,11,0.1)",
                      color: "var(--color-warning, #f59e0b)",
                      border: "1px solid var(--color-warning, #f59e0b)",
                    }
              }
            >
              {isSavingMaint ? (
                <Loader2 size={15} className="animate-spin" />
              ) : maintEnabled ? (
                <PowerOff size={15} />
              ) : (
                <Power size={15} />
              )}
              {maintEnabled ? "Nonaktifkan" : "Aktifkan"} Maintenance
            </button> */}
          </div>
        </form>
      )}
    </div>
  );
}
