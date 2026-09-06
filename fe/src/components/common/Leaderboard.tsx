import { Medal } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { leaderboardService } from "../../services/api";
import type { LeaderboardPeriod } from "../../api/index";
import UserAvatar from "./UserAvatar";

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

const PERIODS: { label: string; value: LeaderboardPeriod }[] = [
  { label: "Hari Ini", value: "today" },
  { label: "Minggu Ini", value: "week" },
  { label: "Bulan Ini", value: "month" },
];

/* Tiga periode = tiga query paralel — semua papan tampil sekaligus,
   tanpa tab. Periode sama tidak polling. */
const STALE_TIME_MS = 30_000;

/* Warna angka 1-3: emas/silver/bronze halus — warna teks angka saja. */
const PODIUM_COLORS = [
  "var(--color-warning)",
  "var(--color-silver)",
  "var(--color-bronze)",
] as const;

/**
 * Privasi nama: 3 huruf pertama tampil, sisanya diganti bintang satu-per-satu
 * mengikuti panjang nama — "Rahmat Aditya" → "Rah**********", "budiman" →
 * "bud****". Nama ≤ 3 karakter tampil apa adanya.
 */
function maskDisplayName(name: string): string {
  if (name.length <= 3) return name;
  return name.slice(0, 3) + "*".repeat(name.length - 3);
}

/**
 * Indikator peringkat — slot lebar-tetap supaya avatar dan nama sejajar
 * sempurna antar baris. 1-3: ikon medali (emas/silver/bronze) berikut angka
 * kecil; 4+: angka tabular biasa.
 */
function RankNumber({ rank }: { rank: number }) {
  if (rank <= 3) {
    return (
      <span className="flex items-center justify-end gap-1 flex-shrink-0" style={{ width: 38 }} aria-label={`Peringkat ${rank}`}>
        <Medal size={14} style={{ color: PODIUM_COLORS[rank - 1] }} aria-hidden="true" />
        <span
          className="tabular-price"
          style={{ color: PODIUM_COLORS[rank - 1], fontWeight: 700, fontSize: "0.8rem" }}
        >
          {rank}
        </span>
      </span>
    );
  }

  return (
    <span
      className="tabular-price flex-shrink-0 text-right"
      style={{ width: 38, color: "var(--color-brand-secondary)", fontWeight: 700, fontSize: "0.8rem" }}
      aria-label={`Peringkat ${rank}`}
    >
      {rank}
    </span>
  );
}

/** Satu bagian editorial per periode: label kecil + garis rambut + baris. */
function PeriodSection({ label, value }: { label: string; value: LeaderboardPeriod }) {
  const { data: entries, isLoading, isError } = useQuery({
    queryKey: ["leaderboard", value],
    queryFn: () => leaderboardService.get(value),
    staleTime: STALE_TIME_MS,
  });

  return (
    <section aria-label={`Leaderboard ${label}`}>
      {/* Label periode + garis rambut — pemisah utama antar bagian adalah
          aturan tipis ini, bukan kartu. */}
      <h3
        style={{
          color: "var(--color-brand-secondary)",
          fontSize: "0.68rem",
          fontWeight: 600,
          letterSpacing: "0.08em",
          textTransform: "uppercase",
          paddingBottom: 10,
          borderBottom: "1px solid var(--color-border-light)",
        }}
      >
        {label}
      </h3>

      {isLoading ? (
        <div className="mt-4 space-y-4">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="flex items-center gap-3">
              <div className="w-[38px] h-4 rounded animate-pulse flex-shrink-0" style={{ backgroundColor: "var(--color-skeleton)" }} />
              <div className="w-9 h-9 rounded-full animate-pulse flex-shrink-0" style={{ backgroundColor: "var(--color-skeleton)" }} />
              <div className="flex-1 h-4 rounded animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
            </div>
          ))}
        </div>
      ) : isError ? (
        <p className="mt-4" style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem" }}>
          Gagal memuat papan peringkat. Coba lagi nanti.
        </p>
      ) : (entries?.length ?? 0) === 0 ? (
        <p className="mt-4" style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", lineHeight: 1.5 }}>
          Belum ada pembelian pada periode ini.
        </p>
      ) : (
        <ol className="mt-2">
          {entries!.map((entry) => (
            <li
              key={entry.rank}
              className="flex items-center gap-3"
              style={{ paddingTop: 9, paddingBottom: 9 }}
            >
              <RankNumber rank={entry.rank} />
              <UserAvatar src={entry.avatarUrl} name={entry.displayName} size={36} />
              <span
                className="flex-1 min-w-0 truncate"
                style={{
                  color: "var(--color-brand-primary)",
                  fontWeight: 600,
                  fontSize: "0.85rem",
                  lineHeight: 1.3,
                }}
              >
                {maskDisplayName(entry.displayName)}
              </span>
              <span
                className="tabular-price flex-shrink-0"
                style={{
                  color: "var(--color-brand-tertiary)",
                  fontWeight: 600,
                  fontSize: "0.8rem",
                }}
              >
                {formatRp(entry.totalSpent)}
              </span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

/**
 * Papan peringkat pembeli — komposisi editorial TANPA kartu: tidak ada
 * wadah besar berbingkai, tidak ada kartu per kolom. Tiga periode dipisah
 * oleh whitespace (desktop: tiga kolom sejajar ber-`gap` lebar; mobile:
 * bertumpuk) dan tiap bagian membawa garis rambutnya sendiri di bawah
 * label periode. Data selalu dari backend (ranking/agregasi tidak dihitung
 * FE); avatar URL OAuth atau fallback ikon default — tidak peduli provider.
 */
export default function Leaderboard() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 md:gap-x-12 md:gap-y-10">
      {PERIODS.map((p) => (
        <PeriodSection key={p.value} label={p.label} value={p.value} />
      ))}
    </div>
  );
}
