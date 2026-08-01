import { useState, useEffect, useCallback } from "react";
import { useLocation, Link } from "react-router";
import { ChevronLeft, ChevronRight, HelpCircle, ArrowRight,
  Zap,
  ShieldCheck,
  CreditCard,
  Headphones,
 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { bannerService, brandService } from "../../../services/api";
import type { BannerItem } from "../../../api/index";
import { getFeaturedFaqs } from "../../../data/faqs";
import FaqAccordion from "../../../components/common/FaqAccordion";
import CategorySection from "../../../components/categori/CategorySection";

function HeroCarousel({ banners }: { banners: BannerItem[] }) {
  const [current, setCurrent] = useState(0);

  const next = useCallback(() => setCurrent((c) => (c + 1) % banners.length), [banners.length]);
  const prev = () => setCurrent((c) => (c - 1 + banners.length) % banners.length);

  useEffect(() => {
    const timer = setInterval(next, 4000);
    return () => clearInterval(timer);
  }, [next]);

  if (!banners.length) return null;

  return (
    <div className="relative overflow-hidden rounded-2xl" style={{ aspectRatio: "22/9" }}>
      {/* Semua slide di-mount lalu di-crossfade lewat opacity — tidak perlu
          AnimatePresence, dan gambarnya tidak di-load ulang tiap ganti slide. */}
      {banners.map((banner, i) => (
        <div
          key={banner.id}
          className="absolute inset-0 transition-opacity duration-500 motion-reduce:transition-none"
          style={{ opacity: i === current ? 1 : 0 }}
          aria-hidden={i !== current}
        >
          <img
            src={banner.imageUrl}
            alt={banner.title}
            className="w-full h-full object-cover"
            loading={i === 0 ? "eager" : "lazy"}
            decoding="async"
          />
          <div className="absolute inset-0" style={{ background: "linear-gradient(to right, rgba(15,20,25,0.7) 0%, transparent 60%)" }} />
          <div className="absolute bottom-6 left-6">
            <h2 style={{ color: "#fff", fontWeight: 700, fontSize: "1.2rem", textShadow: "0 1px 4px rgba(0,0,0,0.4)" }}>
              {banner.title}
            </h2>
          </div>
        </div>
      ))}

      {/* Controls */}
      <button onClick={prev} className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-full flex items-center justify-center" style={{ backgroundColor: "rgba(255,255,255,0.2)", color: "#fff", backdropFilter: "blur(4px)" }}>
        <ChevronLeft size={16} />
      </button>
      <button onClick={next} className="absolute right-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-full flex items-center justify-center" style={{ backgroundColor: "rgba(255,255,255,0.2)", color: "#fff", backdropFilter: "blur(4px)" }}>
        <ChevronRight size={16} />
      </button>

      {/* Dots */}
      <div className="absolute bottom-4 right-4 flex gap-2">
        {banners.map((_, i) => (
          <button
            key={i}
            onClick={() => setCurrent(i)}
            className="rounded-full transition-all duration-300"
            style={{
              width: i === current ? 32 : 12,
              height: 12,
              backgroundColor: i === current ? "var(--color-brand-tertiary, #2C5EF5)" : "rgba(255,255,255,0.5)",
            }}
          />
        ))}
      </div>
    </div>
  );
}

export default function HomePage() {
  const location = useLocation();

  // Scroll to game list when navigated with #game-list hash
  useEffect(() => {
    if (location.hash === "#game-list") {
      const timer = setTimeout(() => {
        document.getElementById("game-list")?.scrollIntoView({ behavior: "smooth" });
      }, 200);
      return () => clearTimeout(timer);
    }
  }, [location.hash]);

  const { data: banners = [], isLoading: bannersLoading } = useQuery({
    queryKey: ["banners"],
    queryFn: bannerService.getAll,
  });

  const { data: brands = [], isLoading: brandsLoading } = useQuery({
    queryKey: ["brands"],
    queryFn: brandService.getAll,
  });

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Hero */}
      <section className="mb-8">
        {bannersLoading ? (
          <div className="rounded-2xl animate-pulse" style={{ aspectRatio: "22/9", backgroundColor: "var(--color-surface, #fff)" }} />
        ) : (
          <HeroCarousel banners={banners} />
        )}
      </section>

      {/* Game Categories */}
      <CategorySection brands={brands} isLoading={brandsLoading} />

      {/* Trust badges */}
      <section className="mt-16">
  <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
    {[
      {
        icon: Zap,
        title: "Proses Instan",
        desc: "Top up langsung masuk",
      },
      {
        icon: ShieldCheck,
        title: "Aman & Terpercaya",
        desc: "Transaksi terenkripsi",
      },
      {
        icon: CreditCard,
        title: "Banyak Metode",
        desc: "GoPay, OVO, Transfer, dll",
      },
      {
        icon: Headphones,
        title: "CS 24 Jam",
        desc: "Siap membantu kapanpun",
      },
    ].map((item) => {
      const Icon = item.icon;

      return (
        <div
          key={item.title}
          className="flex flex-col items-center rounded-2xl p-5 text-center"
          style={{
            backgroundColor: "var(--color-surface)",
            border: "1px solid var(--color-border)",
          }}
        >
          <Icon
            size={28}
            strokeWidth={2}
            style={{
              color: "var(--color-brand-primary)",
              marginBottom: 8,
            }}
          />

          <p
            style={{
              color: "var(--color-primary)",
              fontWeight: 600,
              fontSize: "0.9rem",
            }}
          >
            {item.title}
          </p>

          <p
            style={{
              color: "var(--color-brand-secondary)",
              fontSize: "0.8rem",
              marginTop: 4,
            }}
          >
            {item.desc}
          </p>
        </div>
      );
    })}
  </div>
</section>

      {/* FAQ Preview */}
      <section className="mt-16">
        <div className="flex items-center gap-3 mb-2">
          <HelpCircle size={20} style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />
          <h2 style={{ color: "var(--color-primary, #0F1419)", fontWeight: 700, fontSize: "1.1rem" }}>
            Pertanyaan yang Sering Ditanyakan
          </h2>
        </div>
        <p style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem", marginBottom: 16 }}>
          Berikut beberapa pertanyaan yang paling sering diajukan oleh pengguna Digitalin.
        </p>
        <div
          className="rounded-2xl overflow-hidden"
          style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.07)" }}
        >
          <div className="px-5">
            <FaqAccordion items={getFeaturedFaqs().slice(0, 6)} />
          </div>
        </div>
        <div className="text-center mt-5">
          <Link
            to="/faq"
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl no-underline transition-colors"
            style={{
              backgroundColor: "var(--color-brand-tertiary, #2C5EF5)",
              color: "#fff",
              fontWeight: 600,
              fontSize: "0.875rem",
            }}
          >
            Lihat Semua FAQ
            <ArrowRight size={15} />
          </Link>
        </div>
      </section>
    </div>
  );
}
