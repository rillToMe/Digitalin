import { Ticket, ArrowLeft } from "lucide-react";
import { Link } from "react-router";

export default function VouchersPage() {
  return (
    <div className="max-w-2xl mx-auto px-4 sm:px-6 lg:px-8 py-20 text-center">
      {/* Icon */}
      <div className="flex justify-center mb-6">
        <div
          className="w-20 h-20 rounded-2xl flex items-center justify-center"
          style={{ backgroundColor: "rgba(44,94,245,0.1)" }}
        >
          <Ticket size={40} style={{ color: "var(--color-tertiary, #2C5EF5)" }} />
        </div>
      </div>

      {/* Title */}
      <h1 style={{ color: "var(--color-primary, #0F1419)", fontWeight: 700, fontSize: "1.5rem", marginBottom: 12 }}>
        Voucher Digital
      </h1>

      {/* Badge */}
      <div
        className="inline-flex items-center gap-2 px-4 py-2 rounded-full mb-6"
        style={{ backgroundColor: "rgba(44,94,245,0.08)" }}
      >
        <span
          className="w-2 h-2 rounded-full"
          style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)" }}
        />
        <span style={{ color: "var(--color-tertiary, #2C5EF5)", fontSize: "0.82rem", fontWeight: 600 }}>
          Coming Soon
        </span>
      </div>

      {/* Description */}
      <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.9rem", lineHeight: 1.7, maxWidth: 400, margin: "0 auto 8px" }}>
        Fitur Voucher Digital sedang dalam tahap pengembangan.
        Segera hadir: voucher game, gift card, dan promo eksklusif untuk Anda.
      </p>

      {/* What's coming */}
      <div className="mt-8 mb-10">
        <p style={{ color: "var(--color-primary, #0F1419)", fontWeight: 600, fontSize: "0.9rem", marginBottom: 16 }}>
          Yang akan datang:
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          {[
            { label: "Voucher Game", desc: "Steam, Google Play, dll" },
            { label: "Gift Card", desc: "iTunes, Netflix, dll" },
            { label: "Promo Voucher", desc: "Diskon khusus member" },
          ].map((item) => (
            <div
              key={item.label}
              className="rounded-xl p-4"
              style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }}
            >
              <p style={{ color: "var(--color-primary, #0F1419)", fontWeight: 600, fontSize: "0.85rem", marginBottom: 4 }}>
                {item.label}
              </p>
              <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.78rem" }}>{item.desc}</p>
            </div>
          ))}
        </div>
      </div>

      {/* Back button */}
      <Link
        to="/"
        className="inline-flex items-center gap-2 px-6 py-3 rounded-xl no-underline transition-colors"
        style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem" }}
      >
        <ArrowLeft size={16} />
        Kembali ke Beranda
      </Link>
    </div>
  );
}
