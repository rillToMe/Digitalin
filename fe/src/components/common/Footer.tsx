import { ArrowUpRight, Headphones } from "lucide-react";
import { Link } from "react-router";

/* Semua tujuan di sini harus benar-benar terdaftar di App.tsx — link footer
   yang mati tidak pernah kelihatan waktu ngoding karena jarang diklik.
   Footer.check.ts mencocokkannya dengan daftar <Route path> di App.tsx. */
const NAV_GROUPS: { title: string; links: { label: string; to: string }[] }[] = [
  {
    title: "Layanan",
    links: [
      // Anchor ke section di halaman utama, bukan route sendiri.
      { label: "Top Up Game", to: "/#game-list" },
      { label: "Voucher Digital", to: "/vouchers" },
    ],
  },
  {
    title: "Akun",
    links: [
      { label: "Masuk", to: "/login" },
      { label: "Daftar", to: "/register" },
      { label: "Riwayat Transaksi", to: "/history" },
    ],
  },
  {
    title: "Bantuan",
    links: [
      { label: "FAQ", to: "/faq" },
      { label: "Kontak", to: "/contact" },
      { label: "Lupa Password", to: "/forgot-password" },
    ],
  },
];

export default function Footer() {
  return (
    <footer className="site-footer mt-16">
      <div className="site-footer-content max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12 lg:py-14">
        <div className="site-footer-main grid grid-cols-2 gap-x-8 gap-y-10 md:grid-cols-[minmax(240px,1.75fr)_1fr_1fr_1fr] lg:gap-12">
          <div className="col-span-2 md:col-span-1">
            <Link to="/" className="site-footer-brand">
                <img src="/favicon.svg" alt="" aria-hidden="true" className="h-8 w-auto" />
              Digitalin
            </Link>

            <p className="site-footer-tagline mt-4">
              Platform top up game terpercaya dengan proses cepat dan aman.
            </p>

            <p className="site-footer-kicker mt-5">Top up game, always on.</p>
          </div>

          {NAV_GROUPS.map((group) => (
            <nav key={group.title} aria-label={group.title}>
              <h2 className="site-footer-heading">{group.title}</h2>

              <ul className="site-footer-links">
                {group.links.map((link) => (
                  <li key={link.to}>
                    {/* Hash tidak bisa lewat <Link>: react-router tidak
                        menggulirkan ke elemennya, jadi biarkan browser. */}
                    {link.to.startsWith("/#") ? (
                      <a href={link.to}>{link.label}</a>
                    ) : (
                      <Link to={link.to}>{link.label}</Link>
                    )}
                  </li>
                ))}
              </ul>
            </nav>
          ))}
        </div>

        {/* Tahunnya dihitung, bukan ditulis: "© 2024" sudah basi sejak Januari. */}
        <div className="site-footer-bottom mt-10 pt-6 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
          <p>© {new Date().getFullYear()} Digitalin. All rights reserved.</p>

          <Link to="/contact" className="site-footer-support pressable">
            <span className="site-footer-support-icon" aria-hidden="true">
              <Headphones size={17} strokeWidth={2} />
            </span>
            <span>
              <small>Butuh bantuan transaksi?</small>
              Hubungi kami
            </span>
            <ArrowUpRight size={17} strokeWidth={2} aria-hidden="true" />
          </Link>
        </div>
      </div>
    </footer>
  );
}
