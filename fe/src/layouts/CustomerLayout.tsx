import { useState, useRef, useEffect } from "react";
import { Link, useNavigate, useLocation } from "react-router";
import { User, ShoppingBag, LogOut, ChevronDown, Zap, Menu, X, History, Search } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { useAuthStore } from "../store/authStore";
import SearchBar from "../components/common/SearchBar";

interface CustomerLayoutProps {
  children: React.ReactNode;
}

export function CustomerLayout({ children }: CustomerLayoutProps) {
  const { isAuthenticated, user, logout } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [mobileSearchOpen, setMobileSearchOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const mobileSearchRef = useRef<HTMLDivElement>(null);
  const mobileToggleRef = useRef(false);

  useEffect(() => {
    function handleClick(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClick);
    return () => document.removeEventListener("mousedown", handleClick);
  }, []);

  const handleLogout = () => {
    logout();
    setDropdownOpen(false);
    navigate("/");
  };

  // Close mobile search overlay when route changes
  useEffect(() => {
    setMobileSearchOpen(false);
  }, [location.pathname]);

  // Click outside handler for mobile search overlay
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      // Skip if the click was on the toggle button (handled by its own onClick)
      if (mobileToggleRef.current) {
        mobileToggleRef.current = false;
        return;
      }
      if (mobileSearchRef.current && !mobileSearchRef.current.contains(e.target as Node)) {
        setMobileSearchOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  return (
    <div style={{ fontFamily: "Inter, sans-serif", backgroundColor: "var(--color-neutral, #F1F3F5)", minHeight: "100vh" }}>
      {/* Navbar */}
      <nav style={{ backgroundColor: "var(--color-surface, #fff)", borderBottom: "1px solid rgba(0,0,0,0.08)" }} className="sticky top-0 z-50 relative">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex items-center justify-between h-16">
            {/* Logo */}
            <Link to="/" className="flex items-center gap-2 no-underline">
              <div style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", borderRadius: 8 }} className="w-8 h-8 flex items-center justify-center">
                <Zap size={16} color="#fff" fill="#fff" />
              </div>
              <span style={{ color: "var(--color-primary, #0F1419)", fontWeight: 700, fontSize: "1.1rem" }}>
                Digitalin
              </span>
            </Link>

            {/* Desktop Nav + Search */}
            <div className="hidden md:flex items-center gap-3" style={{ flexShrink: 0 }}>
              {/* Expandable Search */}
              <SearchBar variant="expandable" />

              {/* Auth area */}
              {isAuthenticated() ? (
                <div className="relative" ref={dropdownRef}>
                  <button
                    onClick={() => setDropdownOpen(!dropdownOpen)}
                    className="flex items-center gap-2 px-3 py-2 rounded-lg transition-colors"
                    style={{ color: "var(--color-primary, #0F1419)" }}
                  >
                    <div style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", borderRadius: "50%" }} className="w-8 h-8 flex items-center justify-center">
                      <User size={14} color="#fff" />
                    </div>
                    <span style={{ fontSize: "0.9rem", fontWeight: 500 }}>{user?.fullName ?? "Akun"}</span>
                    <ChevronDown size={14} />
                  </button>

                  <AnimatePresence>
                    {dropdownOpen && (
                      <motion.div
                        initial={{ opacity: 0, y: -8, scale: 0.97 }}
                        animate={{ opacity: 1, y: 0, scale: 1 }}
                        exit={{ opacity: 0, y: -8, scale: 0.97 }}
                        transition={{ duration: 0.15 }}
                        className="absolute right-0 mt-2 w-48 rounded-xl shadow-lg overflow-hidden"
                        style={{ backgroundColor: "var(--color-surface, #fff)", border: "1px solid rgba(0,0,0,0.08)" }}
                      >
                        <Link to="/profile" onClick={() => setDropdownOpen(false)} className="flex items-center gap-3 px-4 py-3 no-underline transition-colors hover:bg-gray-50" style={{ color: "var(--color-primary, #0F1419)" }}>
                          <User size={16} />
                          <span style={{ fontSize: "0.9rem" }}>Profile</span>
                        </Link>
                        <Link to="/history" onClick={() => setDropdownOpen(false)} className="flex items-center gap-3 px-4 py-3 no-underline transition-colors hover:bg-gray-50" style={{ color: "var(--color-primary, #0F1419)" }}>
                          <History size={16} />
                          <span style={{ fontSize: "0.9rem" }}>Riwayat Order</span>
                        </Link>
                        <hr style={{ borderColor: "rgba(0,0,0,0.06)", margin: 0 }} />
                        <button onClick={handleLogout} className="w-full flex items-center gap-3 px-4 py-3 transition-colors hover:bg-red-50" style={{ color: "#ef4444", fontSize: "0.9rem" }}>
                          <LogOut size={16} />
                          <span>Logout</span>
                        </button>
                      </motion.div>
                    )}
                  </AnimatePresence>
                </div>
              ) : (
                <div className="flex items-center gap-3">
                  <Link to="/login" className="px-4 py-2 rounded-lg no-underline transition-colors" style={{ color: "var(--color-primary, #0F1419)", fontSize: "0.9rem", fontWeight: 500 }}>
                    Masuk
                  </Link>
                  <Link to="/register" className="px-4 py-2 rounded-lg no-underline" style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", color: "#fff", fontSize: "0.9rem", fontWeight: 500 }}>
                    Daftar
                  </Link>
                </div>
              )}
            </div>

            {/* Mobile buttons */}
            <div className="flex items-center gap-1 md:hidden">
              <button
                className="p-2 rounded-lg transition-colors"
                style={{ color: "var(--color-primary, #0F1419)" }}
                onMouseDown={() => { mobileToggleRef.current = true; }}
                onClick={() => setMobileSearchOpen((prev) => !prev)}
                aria-label={mobileSearchOpen ? "Tutup pencarian" : "Buka pencarian"}
              >
                <Search size={20} />
              </button>
              <button className="p-2" onClick={() => setMobileMenuOpen(!mobileMenuOpen)} style={{ color: "var(--color-primary, #0F1419)" }}>
                {mobileMenuOpen ? <X size={20} /> : <Menu size={20} />}
              </button>
            </div>
          </div>
        </div>

        {/* Mobile search overlay */}
        <AnimatePresence>
          {mobileSearchOpen && (
            <motion.div
              ref={mobileSearchRef}
              initial={{ opacity: 0, y: -12 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -12 }}
              transition={{ duration: 0.15 }}
              className="md:hidden"
              style={{
                position: "absolute",
                top: "100%",
                left: 0,
                right: 0,
                backgroundColor: "var(--color-surface, #fff)",
                borderBottom: "1px solid rgba(0,0,0,0.08)",
                padding: "12px 16px",
                zIndex: 60,
                boxShadow: "0 4px 16px rgba(0,0,0,0.1)",
              }}
            >
              <SearchBar
                variant="overlay"
                onClose={() => setMobileSearchOpen(false)}
              />
            </motion.div>
          )}
        </AnimatePresence>

        {/* Mobile menu */}
        <AnimatePresence>
          {mobileMenuOpen && (
            <motion.div
              initial={{ opacity: 0, height: 0 }}
              animate={{ opacity: 1, height: "auto" }}
              exit={{ opacity: 0, height: 0 }}
              className="md:hidden overflow-hidden"
              style={{ borderTop: "1px solid rgba(0,0,0,0.06)" }}
            >
              <div className="px-4 py-3 space-y-1" style={{ backgroundColor: "var(--color-surface, #fff)" }}>
                {isAuthenticated() ? (
                  <>
                    <Link to="/profile" onClick={() => setMobileMenuOpen(false)} className="flex items-center gap-3 px-3 py-2 rounded-lg no-underline" style={{ color: "var(--color-primary, #0F1419)" }}>
                      <User size={16} /><span>Profile</span>
                    </Link>
                    <Link to="/history" onClick={() => setMobileMenuOpen(false)} className="flex items-center gap-3 px-3 py-2 rounded-lg no-underline" style={{ color: "var(--color-primary, #0F1419)" }}>
                      <ShoppingBag size={16} /><span>Riwayat Order</span>
                    </Link>
                    <button onClick={handleLogout} className="w-full flex items-center gap-3 px-3 py-2 rounded-lg" style={{ color: "#ef4444" }}>
                      <LogOut size={16} /><span>Logout</span>
                    </button>
                  </>
                ) : (
                  <>
                    <Link to="/login" onClick={() => setMobileMenuOpen(false)} className="block px-3 py-2 rounded-lg no-underline text-center" style={{ color: "var(--color-primary, #0F1419)", fontWeight: 500 }}>Masuk</Link>
                    <Link to="/register" onClick={() => setMobileMenuOpen(false)} className="block px-3 py-2 rounded-lg no-underline text-center" style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", color: "#fff", fontWeight: 500 }}>Daftar</Link>
                  </>
                )}
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </nav>

      {/* Main content */}
      <main>{children}</main>

      {/* Footer */}
      <footer style={{ backgroundColor: "var(--color-primary, #0F1419)", color: "rgba(255,255,255,0.7)" }} className="mt-16 py-10">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex flex-col md:flex-row justify-between items-start gap-8">
            <div>
              <div className="flex items-center gap-2 mb-3">
                <div style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", borderRadius: 6 }} className="w-7 h-7 flex items-center justify-center">
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
