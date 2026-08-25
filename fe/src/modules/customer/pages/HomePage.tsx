import { useState, useEffect, useCallback } from "react";
import { useLocation, Link } from "react-router";
import { ChevronLeft, ChevronRight, HelpCircle, ArrowRight,
  Zap,
  ShieldCheck,
  CreditCard,
  Headphones,
 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { bannerService } from "../../../services/api";
import type { BannerItem } from "../../../api/index";
import { getFeaturedFaqs } from "../../../data/faqs";
import FaqAccordion from "../../../components/common/FaqAccordion";
import CategorySection from "../../../components/categori/CategorySection";

/** Frame responsif dipakai carousel dan skeleton sekaligus agar tidak ada layout shift. */
const HERO_FRAME = "aspect-[16/9] sm:aspect-[21/8] lg:aspect-[24/8]";

function HeroCarousel({ banners }: { banners: BannerItem[] }) {
  const [current, setCurrent] = useState(0);

  const next = useCallback(() => setCurrent((c) => (c + 1) % banners.length), [banners.length]);
  const prev = () => setCurrent((c) => (c - 1 + banners.length) % banners.length);

  useEffect(() => {
    const timer = setInterval(next, 4000);
    return () => clearInterval(timer);
  }, [next]);

  if (!banners.length) return null;

  const previous = (current - 1 + banners.length) % banners.length;
  const following = (current + 1) % banners.length;
  const visibleSlides = banners.length === 1
    ? [{ banner: banners[current], position: "active" }]
    : [
        { banner: banners[previous], position: "previous" },
        { banner: banners[current], position: "active" },
        { banner: banners[following], position: "next" },
      ];

  return (
    <div className={`relative overflow-hidden ${HERO_FRAME}`}>
      {visibleSlides.map(({ banner, position }) => (
        <div
          key={`${position}-${banner.id}`}
          className={`hud-hero-slide hud-panel absolute inset-y-0 overflow-hidden ${position}`}
          aria-hidden={position !== "active"}
        >
          <img
            src={banner.imageUrl}
            alt={banner.title}
            className="w-full h-full object-cover"
            loading={position === "active" ? "eager" : "lazy"}
            decoding="async"
          />
          {/* Scrim solid hanya di belakang judul — bukan gradient menutupi
              seluruh banner (design.md: sistem ini flat) dan bukan
              text-shadow, yang tidak menolong di atas gambar terang. */}
          <div className="absolute bottom-0 left-0 right-0 px-6 py-4 pr-28" style={{ backgroundColor: "var(--color-poster-scrim)" }}>
            <h2 style={{ color: "var(--color-on-primary)", fontWeight: 700, fontSize: "1.2rem" }}>
              {banner.title}
            </h2>
          </div>
        </div>
      ))}

      {/* Controls */}
      {banners.length > 1 && <button onClick={prev} aria-label="Banner sebelumnya" className="absolute left-[5.5vw] top-1/2 z-20 flex h-9 w-9 -translate-y-1/2 items-center justify-center border sm:left-[8vw]" style={{ backgroundColor: "var(--color-media-control)", color: "var(--color-on-primary)", backdropFilter: "blur(4px)" }}>
        <ChevronLeft size={16} aria-hidden="true" />
      </button>}
      {banners.length > 1 && <button onClick={next} aria-label="Banner berikutnya" className="absolute right-[5.5vw] top-1/2 z-20 flex h-9 w-9 -translate-y-1/2 items-center justify-center border sm:right-[8vw]" style={{ backgroundColor: "var(--color-media-control)", color: "var(--color-on-primary)", backdropFilter: "blur(4px)" }}>
        <ChevronRight size={16} aria-hidden="true" />
      </button>}

      {/* Dots */}
      <div className="absolute bottom-4 left-1/2 z-20 flex -translate-x-1/2 gap-2">
        {banners.map((_, i) => (
          <button
            key={i}
            onClick={() => setCurrent(i)}
            aria-label={`Ke banner ${i + 1}`}
            aria-current={i === current}
            className="h-1 border-0 transition-all duration-150 motion-reduce:transition-none"
            style={{
              width: i === current ? 32 : 12,
              height: 12,
              backgroundColor: i === current ? "var(--color-brand-tertiary)" : "var(--color-on-media-muted)",
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

  return (
    <div className="mx-auto max-w-7xl px-4 pb-8 sm:px-6 lg:px-8">
      {/* Hero */}
      <section className="relative left-1/2 mb-8 w-[100dvw] max-w-[100dvw] -translate-x-1/2 overflow-hidden pt-2 sm:pt-3">
        {bannersLoading ? (
          <div className={`mx-[4vw] animate-pulse border sm:mx-[7vw] ${HERO_FRAME}`} style={{ backgroundColor: "var(--color-skeleton)" }} />
        ) : (
          <HeroCarousel banners={banners} />
        )}
      </section>

      {/* Game Categories */}
      <CategorySection />

      {/* Cara top up — urutannya sama dengan langkah di halaman game
          (01 Informasi Akun → 02 Pilih Nominal → bayar), jadi halaman ini
          sudah mengajarkan alurnya sebelum orang masuk ke sana. Ditaruh
          setelah katalog supaya baris kartu pertama tidak turun dari layar. */}
      <section className="mt-16" aria-labelledby="cara-topup">
        <h2 id="cara-topup" className="hud-section-label mb-4">
          Cara top up
        </h2>
        <ol className="grid gap-4 sm:grid-cols-3">
          {[
            { n: "01", title: "Pilih game", desc: "Cari lewat katalog atau kolom pencarian." },
            { n: "02", title: "Isi data & nominal", desc: "Masukkan ID akun, lalu pilih nominal." },
            { n: "03", title: "Bayar", desc: "Selesaikan pembayaran, saldo diproses otomatis." },
          ].map((step) => (
            <li
              key={step.n}
              className="hud-panel p-5"
              style={{
                backgroundColor: "var(--color-surface)",
                border: "1px solid var(--color-border-light)",
              }}
            >
              <p
                style={{
                  fontFamily: "var(--font-label-mono)",
                  fontSize: "1.5rem",
                  fontWeight: 600,
                  lineHeight: 1,
                  /* Bukan kobalt: aksen disimpan untuk aksi (design.md), dan
                     angka mono abu justru lebih terbaca sebagai penanda urutan. */
                  color: "var(--color-brand-secondary)",
                }}
              >
                {step.n}
              </p>
              <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.9rem", marginTop: 10 }}>
                {step.title}
              </p>
              <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem", marginTop: 4 }}>
                {step.desc}
              </p>
            </li>
          ))}
        </ol>
      </section>

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
          className="hud-panel flex flex-col items-center p-5 text-center"
          style={{
            backgroundColor: "var(--color-surface)",
            border: "1px solid var(--color-border-light)",
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
              color: "var(--color-brand-primary)",
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
        <div className="hud-section-label mb-4">
          <HelpCircle size={20} style={{ color: "var(--color-brand-tertiary)" }} />
          <h2 style={{ color: "var(--color-brand-primary)", fontWeight: 700, fontSize: "1.1rem" }}>
            Pertanyaan yang Sering Ditanyakan
          </h2>
        </div>
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.875rem", marginBottom: 16 }}>
          Berikut beberapa pertanyaan yang paling sering diajukan oleh pengguna Digitalin.
        </p>
        <div
          className="hud-panel overflow-hidden"
          style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
        >
          <div className="px-5">
            <FaqAccordion items={getFeaturedFaqs().slice(0, 6)} />
          </div>
        </div>
        <div className="text-center mt-5">
          <Link
            to="/faq"
            className="pressable inline-flex items-center gap-2 border px-5 py-2.5 no-underline transition-colors"
            style={{
              backgroundColor: "var(--color-brand-tertiary)",
              color: "var(--color-on-primary)",
              fontWeight: 600,
              fontSize: "0.875rem",
            }}
          >
            Lihat Semua FAQ
            <ArrowRight size={15} aria-hidden="true" />
          </Link>
        </div>
      </section>
    </div>
  );
}
