import React from "react";
import { Wrench, RefreshCw, Zap } from "lucide-react";

interface MaintenanceCardProps {
  title: string;
  message: string;
  estimatedFinish: string | null;
  onRefresh: () => void;
  isChecking: boolean;
  countdown: number;
}

function formatEstimatedFinish(isoString: string | null) {
  if (!isoString) return null;
  try {
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return null;
    const day = date.getDate();
    const months = [
      "Januari", "Februari", "Maret", "April", "Mei", "Juni",
      "Juli", "Agustus", "September", "Oktober", "November", "Desember"
    ];
    const month = months[date.getMonth()];
    const year = date.getFullYear();
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');

    return {
      dateStr: `${day} ${month} ${year}`,
      timeStr: `${hours}:${minutes} WIB`
    };
  } catch (e) {
    return null;
  }
}

export default function MaintenanceCard({
  title,
  message,
  estimatedFinish,
  onRefresh,
  isChecking,
  countdown,
}: MaintenanceCardProps) {
  const est = formatEstimatedFinish(estimatedFinish);
  // If the estimated finish date has already passed, treat it as no estimate
  // (avoid showing negative/stale countdowns or past timestamps)
  const isEstPast = estimatedFinish
    ? new Date(estimatedFinish).getTime() <= Date.now()
    : false;

  return (
    <div
      className="w-full max-w-md rounded-2xl p-8 shadow-sm text-center space-y-6"
      style={{
        backgroundColor: "var(--color-surface)",
        border: "1px solid var(--color-border-light)",
      }}
    >
      {/* Icon & Logo */}
      <div className="space-y-4">
        <div className="flex justify-center items-center gap-2">
          <div
            style={{ backgroundColor: "var(--color-brand-tertiary)", borderRadius: 8 }}
            className="w-8 h-8 flex items-center justify-center"
          >
            <Zap size={16} color="#fff" fill="#fff" />
          </div>
          <span style={{ color: "var(--color-brand-primary)", fontWeight: 800, fontSize: "1.2rem" }}>
            Digitalin
          </span>
        </div>

        <div className="flex justify-center">
          <div
            className="w-16 h-16 rounded-full flex items-center justify-center animate-bounce"
            style={{ backgroundColor: "var(--color-warning-bg)" }}
          >
            <Wrench size={32} style={{ color: "var(--color-warning)" }} />
          </div>
        </div>
      </div>

      {/* Title & Description */}
      <div className="space-y-2">
        <h2 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.25rem" }}>
          {title || "Website Sedang Maintenance"}
        </h2>
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.88rem", lineHeight: 1.5 }}>
          {message || "Kami sedang melakukan peningkatan sistem untuk memberikan layanan yang lebih baik."}
        </p>
      </div>

      {/* Estimated Finish Date */}
      {est && !isEstPast && (
        <div className="p-4 rounded-xl space-y-1" style={{ backgroundColor: "var(--color-neutral)" }}>
          <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.78rem", fontWeight: 500 }}>
            Estimasi Selesai
          </p>
          <p style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "0.9rem" }}>
            {est.dateStr}
          </p>
          <p style={{ color: "var(--color-brand-tertiary)", fontWeight: 600, fontSize: "0.82rem" }}>
            {est.timeStr}
          </p>
        </div>
      )}

      {/* Fallback when estimated finish is stale / in the past */}
      {est && isEstPast && (
        <div className="p-4 rounded-xl space-y-1" style={{ backgroundColor: "var(--color-neutral)" }}>
          <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.78rem", fontWeight: 500 }}>
            Estimasi Selesai
          </p>
          <p style={{ color: "var(--color-brand-secondary)", fontWeight: 600, fontSize: "0.85rem" }}>
            Maintenance masih berlangsung.
          </p>
        </div>
      )}

      {/* {countdown > 0 && (
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.78rem" }}>
          Mencoba kembali dalam <span className="font-semibold" style={{ color: "var(--color-brand-tertiary)" }}>{countdown} detik</span>
        </p>
      )}

      <button
        onClick={onRefresh}
        disabled={isChecking || countdown > 0}
        className="w-full py-3 rounded-xl flex items-center justify-center gap-2 font-semibold text-white transition-opacity hover:opacity-90 disabled:opacity-50"
        style={{
          backgroundColor: "var(--color-brand-tertiary)",
          cursor: isChecking || countdown > 0 ? "not-allowed" : "pointer",
        }}
      >
        {isChecking ? (
          <RefreshCw size={16} className="animate-spin" />
        ) : (
          <RefreshCw size={16} />
        )}
        Coba Lagi
      </button> */}
    </div>
  );
}
