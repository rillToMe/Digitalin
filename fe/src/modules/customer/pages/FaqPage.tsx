import { useState, useEffect } from "react";
import { Search, HelpCircle, MessageCircle } from "lucide-react";
import { Link } from "react-router";
import { useQuery } from "@tanstack/react-query";
import FaqAccordion from "../../../components/common/FaqAccordion";
import BackButton from "../../../components/common/BackButton";
import { faqService } from "../../../services/api";

/* Filter/search dijalankan server-side (GET /faqs?category=&search=);
   kategori juga dari API — FE tidak lagi hardcode daftarnya. */
const DEBOUNCE_MS = 300;

export default function FaqPage() {
  const [activeTab, setActiveTab] = useState<string>("Semua");
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");

  // Debounce search (pola yang sama dengan useSearchGames / VouchersPage)
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [search]);

  const { data: categories } = useQuery({
    queryKey: ["faq-categories"],
    queryFn: faqService.getCategories,
  });

  const category = activeTab === "Semua" ? undefined : activeTab;

  const { data: faqs, isLoading, isError } = useQuery({
    queryKey: ["faqs", category, debouncedSearch],
    queryFn: () =>
      faqService.getAll({
        category,
        search: debouncedSearch.trim() || undefined,
      }),
  });

  const tabs = ["Semua", ...(categories ?? [])];

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      <BackButton />
      {/* Header */}
      <div className="text-center mb-8">
        <div className="flex justify-center mb-4">
          <div
            className="hud-panel flex h-14 w-14 items-center justify-center"
            style={{ backgroundColor: "rgba(44,94,245,0.1)" }}
          >
            <HelpCircle size={28} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />
          </div>
        </div>
        <h1 style={{ color: "var(--color-brand-primary, #0F1419)", fontWeight: 700, fontSize: "1.5rem", marginBottom: 8 }}>
          Pusat Bantuan Digitalin
        </h1>
        <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.9rem", maxWidth: 520, margin: "0 auto" }}>
          Temukan jawaban atas pertanyaan yang paling sering ditanyakan mengenai Top Up, pembayaran, akun, dan transaksi.
        </p>
      </div>

      {/* Search */}
      <div className="mb-6">
        <div
          className="hud-panel flex items-center gap-2 px-4"
          style={{
            backgroundColor: "var(--color-surface, #fff)",
            border: "1.5px solid rgba(0,0,0,0.1)",
            height: 44,
          }}
        >
          <Search size={16} style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }} />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Cari pertanyaan..."
            aria-label="Cari FAQ"
            className="flex-1 outline-none bg-transparent"
            style={{ color: "var(--color-brand-primary, #0F1419)", fontSize: "0.9rem" }}
          />
        </div>
      </div>

      {/* Category Tabs — kategori dari API */}
      <div className="mb-8 overflow-x-auto" style={{ scrollbarWidth: "none" }}>
        <div className="flex gap-2 pb-1">
          {tabs.map((tab) => {
            const isActive = activeTab === tab;
            return (
              <button
                key={String(tab)}
                onClick={() => setActiveTab(tab)}
                className="whitespace-nowrap border-b-2 border-transparent px-1 py-2 transition-colors"
                style={{
                  backgroundColor: "transparent",
                  color: isActive ? "var(--color-brand-primary, #0F1419)" : "var(--color-brand-secondary, #4A5568)",
                  borderBottomColor: isActive ? "var(--color-brand-tertiary, #2C5EF5)" : "transparent",
                  fontWeight: isActive ? 600 : 400,
                  fontSize: "0.83rem",
                  cursor: "pointer",
                }}
              >
                {tab}
              </button>
            );
          })}
        </div>
      </div>

      {/* FAQ List */}
      <div
        className="hud-panel overflow-hidden"
        style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.07)" }}
      >
        <div className="px-5">
          {isLoading ? (
            <div className="py-10 space-y-4">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="h-6 rounded-lg animate-pulse" style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }} />
              ))}
            </div>
          ) : isError ? (
            <div className="py-12 text-center">
              <HelpCircle size={32} style={{ color: "var(--color-brand-secondary, #4A5568)", opacity: 0.4, margin: "0 auto 12px" }} />
              <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.9rem" }}>
                Gagal memuat FAQ. Coba muat ulang halaman.
              </p>
            </div>
          ) : (faqs?.length ?? 0) > 0 ? (
            <FaqAccordion items={faqs!} />
          ) : (
            <div className="py-12 text-center">
              <HelpCircle size={32} style={{ color: "var(--color-brand-secondary, #4A5568)", opacity: 0.4, margin: "0 auto 12px" }} />
              <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.9rem" }}>
                Tidak ada pertanyaan yang sesuai.
              </p>
              <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.82rem", marginTop: 4 }}>
                Coba kata kunci lain atau{" "}
                <Link to="/contact" style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }}>
                  hubungi kami
                </Link>
                .
              </p>
            </div>
          )}
        </div>
      </div>

      {/* CTA */}
      <div className="text-center mt-10">
        <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.9rem", marginBottom: 12 }}>
          Tidak menemukan jawaban yang Anda cari?
        </p>
        <Link
          to="/contact"
          className="inline-flex items-center gap-2 border px-6 py-3 no-underline transition-colors"
          style={{ backgroundColor: "var(--color-brand-tertiary, #2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem" }}
        >
          <MessageCircle size={16} />
          Hubungi Kami
        </Link>
      </div>
    </div>
  );
}
