import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router";
import { ChevronLeft, ChevronRight, Gamepad2, Monitor, Smartphone, MoreHorizontal } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { useQuery } from "@tanstack/react-query";
import { bannerService, brandService } from "../../../services/api";
import type { BannerItem, BrandItem } from "../../../api/index";

const CATEGORIES = [
  { id: "all", label: "Semua", icon: <Gamepad2 size={15} /> },
  { id: "mobile", label: "Mobile Game", icon: <Smartphone size={15} /> },
  { id: "pc", label: "PC Game", icon: <Monitor size={15} /> },
  { id: "other", label: "Lainnya", icon: <MoreHorizontal size={15} /> },
];

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
      <AnimatePresence mode="wait">
        <motion.div
          key={current}
          initial={{ opacity: 0, x: 60 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -60 }}
          transition={{ duration: 0.4, ease: "easeInOut" }}
          className="absolute inset-0"
        >
          <img
            src={banners[current].imageUrl}
            alt={banners[current].title}
            className="w-full h-full object-cover"
          />
          <div className="absolute inset-0" style={{ background: "linear-gradient(to right, rgba(15,20,25,0.7) 0%, transparent 60%)" }} />
          <div className="absolute bottom-6 left-6">
            <h2 style={{ color: "#fff", fontWeight: 700, fontSize: "1.2rem", textShadow: "0 1px 4px rgba(0,0,0,0.4)" }}>
              {banners[current].title}
            </h2>
          </div>
        </motion.div>
      </AnimatePresence>

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
              backgroundColor: i === current ? "var(--color-tertiary, #2C5EF5)" : "rgba(255,255,255,0.5)",
            }}
          />
        ))}
      </div>
    </div>
  );
}

function GameCard({ brand }: { brand: BrandItem }) {
  const navigate = useNavigate();
  const minPrice = 14000;

  return (
    <motion.div
      whileHover={{ y: -4, scale: 1.02 }}
      whileTap={{ scale: 0.98 }}
      onClick={() => navigate(`/game/${brand.slug}`)}
      className="cursor-pointer rounded-2xl overflow-hidden"
      style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.07)", transition: "box-shadow 0.2s" }}
    >
      <div style={{ height: 120, overflow: "hidden", position: "relative" }}>
        {brand.imageUrl ? (
          <img src={brand.imageUrl} alt={brand.brand} className="w-full h-full object-cover" />
        ) : (
          <div className="w-full h-full flex items-center justify-center" style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }}>
            <Gamepad2 size={32} style={{ color: "var(--color-secondary, #4A5568)" }} />
          </div>
        )}
        <div className="absolute inset-0" style={{ background: "linear-gradient(to top, rgba(15,20,25,0.6) 0%, transparent 50%)" }} />
      </div>
      <div className="px-3 py-3">
        <p style={{ color: "var(--color-primary, #0F1419)", fontWeight: 600, fontSize: "0.9rem" }} className="truncate">{brand.brand}</p>
        <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.75rem", marginTop: 2 }}>
          Mulai dari <span style={{ color: "var(--color-tertiary, #2C5EF5)", fontWeight: 600 }}>Rp {minPrice.toLocaleString("id-ID")}</span>
        </p>
      </div>
    </motion.div>
  );
}

// Skeleton Card
function SkeletonCard() {
  return (
    <div className="rounded-2xl overflow-hidden" style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
      <div className="w-full h-32 animate-pulse" style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }} />
      <div className="p-3">
        <div className="h-4 w-3/4 rounded animate-pulse mb-2" style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }} />
        <div className="h-3 w-1/2 rounded animate-pulse" style={{ backgroundColor: "var(--color-neutral, #F1F3F5)" }} />
      </div>
    </div>
  );
}

export default function HomePage() {
  const [category, setCategory] = useState("all");

  const { data: banners = [], isLoading: bannersLoading } = useQuery({
    queryKey: ["banners"],
    queryFn: bannerService.getAll,
  });

  const { data: brands = [], isLoading: brandsLoading } = useQuery({
    queryKey: ["brands"],
    queryFn: brandService.getAll,
  });

  const loading = bannersLoading || brandsLoading;

  const filteredBrands = brands.filter((b) => {
    if (category === "all") return true;
    if (category === "mobile") return ["Mobile Legends", "Free Fire", "PUBG Mobile", "Honkai Star Rail", "Genshin Impact"].includes(b.brand);
    if (category === "pc") return ["Valorant", "Steam Wallet"].includes(b.brand);
    return true;
  });

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Hero */}
      <section className="mb-8">
        {loading ? (
          <div className="rounded-2xl animate-pulse" style={{ aspectRatio: "22/9", backgroundColor: "var(--color-surface, #fff)" }} />
        ) : (
          <HeroCarousel banners={banners} />
        )}
      </section>

      {/* Categories */}
      <section className="mb-8">
        <div className="flex gap-2 overflow-x-auto pb-2" style={{ scrollbarWidth: "none" }}>
          {CATEGORIES.map((cat) => {
            const active = category === cat.id;
            return (
              <button
                key={cat.id}
                onClick={() => setCategory(cat.id)}
                className="flex items-center gap-2 px-4 py-2 rounded-full whitespace-nowrap transition-all"
                style={{
                  backgroundColor: active ? "var(--color-tertiary, #2C5EF5)" : "var(--color-surface, #fff)",
                  color: active ? "#fff" : "var(--color-primary, #0F1419)",
                  border: active ? "none" : "1px solid rgba(0,0,0,0.1)",
                  fontWeight: active ? 600 : 400,
                  fontSize: "0.85rem",
                }}
              >
                {cat.icon}
                {cat.label}
              </button>
            );
          })}
        </div>
      </section>

      {/* Game Grid */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 style={{ color: "var(--color-primary, #0F1419)", fontWeight: 700, fontSize: "1.1rem" }}>
            {category === "all" ? "Semua Game" : CATEGORIES.find((c) => c.id === category)?.label}
          </h2>
          {!loading && (
            <span style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.85rem" }}>
              {filteredBrands.length} game
            </span>
          )}
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-4">
          {loading
            ? Array.from({ length: 8 }).map((_, i) => <SkeletonCard key={i} />)
            : filteredBrands.map((brand) => <GameCard key={brand.id} brand={brand} />)}
        </div>

        {!loading && filteredBrands.length === 0 && (
          <div className="text-center py-16">
            <Gamepad2 size={40} style={{ color: "var(--color-secondary, #4A5568)", margin: "0 auto 12px" }} />
            <p style={{ color: "var(--color-secondary, #4A5568)" }}>Tidak ada game ditemukan</p>
          </div>
        )}
      </section>

      {/* Trust badges */}
      <section className="mt-16">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          {[
            { icon: "⚡", title: "Proses Instan", desc: "Top up langsung masuk" },
            { icon: "🔒", title: "Aman & Terpercaya", desc: "Transaksi terenkripsi" },
            { icon: "💳", title: "Banyak Metode", desc: "GoPay, OVO, Transfer, dll" },
            { icon: "🎧", title: "CS 24 Jam", desc: "Siap membantu kapanpun" },
          ].map((item, i) => (
            <div key={i} className="rounded-2xl p-5 flex flex-col items-center text-center" style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.07)" }}>
              <span style={{ fontSize: "1.8rem", marginBottom: 8 }}>{item.icon}</span>
              <p style={{ color: "var(--color-primary, #0F1419)", fontWeight: 600, fontSize: "0.9rem" }}>{item.title}</p>
              <p style={{ color: "var(--color-secondary, #4A5568)", fontSize: "0.8rem", marginTop: 4 }}>{item.desc}</p>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
