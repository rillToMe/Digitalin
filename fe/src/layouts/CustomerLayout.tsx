import { Link } from "react-router";
import { Zap } from "lucide-react";
import Navbar from "../components/common/Navbar";

interface CustomerLayoutProps {
  children: React.ReactNode;
}

export function CustomerLayout({ children }: CustomerLayoutProps) {
  return (
    <div style={{ fontFamily: "Inter, sans-serif", backgroundColor: "var(--color-neutral)", minHeight: "100vh" }}>
      <Navbar />

      {/* Main content */}
      <main>{children}</main>

      {/* Footer */}
      <footer style={{ backgroundColor: "var(--color-brand-primary, #0F1419)", color: "rgba(255,255,255,0.7)" }} className="mt-16 py-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex flex-col md:flex-row justify-between items-start gap-8">
            <div>
              <div className="flex items-center gap-2 mb-3">
                <div style={{ backgroundColor: "var(--color-brand-tertiary, #2C5EF5)", borderRadius: 6 }} className="w-7 h-7 flex items-center justify-center">
                  <Zap size={13} color="#fff" fill="#fff" />
                </div>
                <span style={{ color: "#fff", fontWeight: 700 }}>Digitalin</span>
              </div>
              <p style={{ fontSize: "0.85rem", maxWidth: 280 }}>Platform top up game terpercaya dengan proses cepat dan aman.</p>
            </div>
            <div className="flex gap-12">
              <div>
                <p style={{ color: "#fff", fontWeight: 600, fontSize: "0.9rem", marginBottom: 8 }}>Layanan</p>
                <ul className="space-y-2" style={{ fontSize: "0.85rem", listStyle: "none", padding: 0 }}>
                  <li>
                    <a
                      href="/#game-list"
                      className="no-underline transition-opacity hover:opacity-80"
                      style={{ color: "rgba(255,255,255,0.7)" }}
                    >
                      Top Up Game
                    </a>
                  </li>
                  <li>
                    <Link to="/vouchers" className="no-underline transition-opacity hover:opacity-80" style={{ color: "rgba(255,255,255,0.7)" }}>
                      Voucher Digital
                    </Link>
                  </li>
                </ul>
              </div>
              <div>
                <p style={{ color: "#fff", fontWeight: 600, fontSize: "0.9rem", marginBottom: 8 }}>Bantuan</p>
                <ul className="space-y-2" style={{ fontSize: "0.85rem", listStyle: "none", padding: 0 }}>
                  <li>
                    <Link to="/faq" className="no-underline transition-opacity hover:opacity-80" style={{ color: "rgba(255,255,255,0.7)" }}>
                      FAQ
                    </Link>
                  </li>
                  <li>
                    <Link to="/contact" className="no-underline transition-opacity hover:opacity-80" style={{ color: "rgba(255,255,255,0.7)" }}>
                      Kontak
                    </Link>
                  </li>
                </ul>
              </div>
            </div>
          </div>
          <hr style={{ borderColor: "rgba(255,255,255,0.1)", marginTop: 32, marginBottom: 24 }} />
          <p style={{ fontSize: "0.8rem", textAlign: "center" }}>© 2024 Digitalin. All rights reserved.</p>
        </div>
      </footer>
    </div>
  );
}
