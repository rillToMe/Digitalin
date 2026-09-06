import BackButton from "../../../components/common/BackButton";
import Leaderboard from "../../../components/common/Leaderboard";

/**
 * Halaman papan peringkat pembeli — publik, data seluruhnya dari backend
 * (GET /leaderboard?period=...). Ranking/agregasi tidak dihitung FE.
 * Komposisi header editorial: judul sebagai jangkar visual utama, tanpa
 * ilustrasi dekoratif.
 */
export default function LeaderboardPage() {
  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      <BackButton />

      {/* Header — hierarchy dua tingkat: judul menonjol, deskripsi senyap. */}
      <header className="mb-10">
        <h1
          style={{
            color: "var(--color-brand-primary, #0F1419)",
            fontWeight: 800,
            fontSize: "1.75rem",
            letterSpacing: "-0.01em",
            lineHeight: 1.2,
          }}
        >
          Top Pembeli Digitalin
        </h1>
        <p
          style={{
            color: "var(--color-brand-secondary, #4A5568)",
            fontSize: "0.875rem",
            marginTop: 6,
            maxWidth: 460,
            lineHeight: 1.55,
          }}
        >
          Peringkat pembeli berdasarkan total belanja aktual hari ini, minggu
          ini, dan bulan ini.
        </p>
      </header>

      <Leaderboard />
    </div>
  );
}
