import { useState, useMemo } from "react";
import { Search, HelpCircle, MessageCircle } from "lucide-react";
import { Link } from "react-router";
import FaqAccordion from "../../../components/common/FaqAccordion";
import { FAQ_CATEGORIES, searchFaqs } from "../../../data/faqs";
import type { FaqCategory } from "../../../data/faqs";

type TabKey = "Semua" | FaqCategory;
const TABS: TabKey[] = ["Semua", ...FAQ_CATEGORIES];

export default function FaqPage() {
  const [activeTab, setActiveTab] = useState<TabKey>("Semua");
  const [searchQuery, setSearchQuery] = useState("");

  const filteredFaqs = useMemo(
    () => searchFaqs({ category: activeTab, query: searchQuery }),
    [activeTab, searchQuery]
  );

  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-10">
      {/* Header */}
      <div className="text-center mb-8">
        <div className="flex justify-center mb-4">
          <div
            className="w-14 h-14 rounded-2xl flex items-center justify-center"
            style={{ backgroundColor: "rgba(44,94,245,0.1)" }}
          >
            <HelpCircle size={28} style={{ color: "var(--color-tertiary, #2C5EF5)" }} />
          </div>
        </div>
        <h1 style={{ color: "var(--color-primary, #0F1419)", fontWeight: 700, fontSize: "1.5rem", marginBottom: 8 }}>
          Pusat Bantuan Digitalin
        </h1>
        <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.9rem", maxWidth: 520, margin: "0 auto" }}>
          Temukan jawaban atas pertanyaan yang paling sering ditanyakan mengenai Top Up, pembayaran, akun, dan transaksi.
        </p>
      </div>

      {/* Search */}
      <div className="mb-6">
        <div
          className="flex items-center gap-2 rounded-xl px-4"
          style={{
            backgroundColor: "var(--color-surface, #fff)",
            border: "1.5px solid rgba(0,0,0,0.1)",
            height: 44,
          }}
        >
          <Search size={16} style={{ color: "var(--color-secondary, #4A5568)", flexShrink: 0 }} />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Cari pertanyaan..."
            aria-label="Cari FAQ"
            className="flex-1 outline-none bg-transparent"
            style={{ color: "var(--color-primary, #0F1419)", fontSize: "0.9rem" }}
          />
        </div>
      </div>

      {/* Category Tabs */}
      <div className="mb-8 overflow-x-auto" style={{ scrollbarWidth: "none" }}>
        <div className="flex gap-2 pb-1">
          {TABS.map((tab) => {
            const isActive = activeTab === tab;
            return (
              <button
                key={String(tab)}
                onClick={() => setActiveTab(tab)}
                className="whitespace-nowrap px-4 py-2 rounded-full transition-all"
                style={{
                  backgroundColor: isActive ? "var(--color-tertiary, #2C5EF5)" : "var(--color-surface, #fff)",
                  color: isActive ? "#fff" : "var(--color-primary, #0F1419)",
                  border: isActive ? "none" : "1px solid rgba(0,0,0,0.1)",
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
        className="rounded-2xl overflow-hidden"
        style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.07)" }}
      >
        <div className="px-5">
          {filteredFaqs.length > 0 ? (
            <FaqAccordion items={filteredFaqs} />
          ) : (
            <div className="py-12 text-center">
              <HelpCircle size={32} style={{ color: "var(--color-secondary, #4A5568)", opacity: 0.4, margin: "0 auto 12px" }} />
              <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.9rem" }}>
                Tidak ada pertanyaan yang sesuai.
              </p>
              <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.82rem", marginTop: 4 }}>
                Coba kata kunci lain atau{" "}
                <Link to="/contact" style={{ color: "var(--color-tertiary, #2C5EF5)" }}>
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
        <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.9rem", marginBottom: 12 }}>
          Tidak menemukan jawaban yang Anda cari?
        </p>
        <Link
          to="/contact"
          className="inline-flex items-center gap-2 px-6 py-3 rounded-xl no-underline transition-colors"
          style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", color: "#fff", fontWeight: 600, fontSize: "0.9rem" }}
        >
          <MessageCircle size={16} />
          Hubungi Kami
        </Link>
      </div>
    </div>
  );
}
