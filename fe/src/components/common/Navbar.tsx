import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { useQuery } from "@tanstack/react-query";
import {
  ChevronDown,
  History,
  LogOut,
  Menu,
  Search,
  ShoppingBag,
  User,
  X,
  Zap,
} from "lucide-react";
import { AnimatePresence, motion } from "motion/react";

import { useAuthStore } from "../../store/authStore";
import { authService } from "../../services/api";
import SearchBar from "./SearchBar";

export default function Navbar() {
  const token = useAuthStore((state) => state.token);
  const user = useAuthStore((state) => state.user);
  const setUser = useAuthStore((state) => state.setUser);
  const logout = useAuthStore((state) => state.logout);

  const navigate = useNavigate();
  const location = useLocation();

  const isAuthenticated = Boolean(token);

  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [mobileSearchOpen, setMobileSearchOpen] = useState(false);

  const dropdownRef = useRef<HTMLDivElement>(null);
  const mobileSearchRef = useRef<HTMLDivElement>(null);
  const mobileToggleRef = useRef(false);

  const { data: profile } = useQuery({
    queryKey: ["profile"],
    queryFn: authService.getProfile,
    enabled: isAuthenticated,
  });

  useEffect(() => {
    if (profile) {
      setUser(profile);
    }
  }, [profile, setUser]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        dropdownRef.current &&
        !dropdownRef.current.contains(event.target as Node)
      ) {
        setDropdownOpen(false);
      }
    }

    document.addEventListener("mousedown", handleClickOutside);

    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  useEffect(() => {
    setDropdownOpen(false);
    setMobileMenuOpen(false);
    setMobileSearchOpen(false);
  }, [location.pathname]);

  useEffect(() => {
    function handleMobileSearchClickOutside(event: MouseEvent) {
      if (mobileToggleRef.current) {
        mobileToggleRef.current = false;
        return;
      }

      if (
        mobileSearchRef.current &&
        !mobileSearchRef.current.contains(event.target as Node)
      ) {
        setMobileSearchOpen(false);
      }
    }

    document.addEventListener("mousedown", handleMobileSearchClickOutside);

    return () => {
      document.removeEventListener(
        "mousedown",
        handleMobileSearchClickOutside,
      );
    };
  }, []);

  const handleLogout = () => {
    logout();
    setDropdownOpen(false);
    setMobileMenuOpen(false);
    navigate("/");
  };

  const displayName = profile?.fullName ?? user?.fullName ?? "Akun";

  return (
    <nav
      className="sticky top-0 z-50 relative"
      style={{
        backgroundColor: "var(--color-surface)",
        borderBottom: "1px solid var(--color-border)",
      }}
    >
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          <Link to="/" className="flex items-center gap-2 no-underline">
            <div
              className="w-8 h-8 flex items-center justify-center"
              style={{
                backgroundColor: "var(--color-brand-tertiary)",
                borderRadius: 8,
              }}
            >
              <Zap
                size={16}
                color="var(--background)"
                fill="var(--background)"
              />
            </div>

            <span
              style={{
                color: "var(--color-brand-primary)",
                fontWeight: 700,
                fontSize: "1.1rem",
              }}
            >
              Digitalin
            </span>
          </Link>

          <div className="hidden md:flex items-center gap-3 flex-shrink-0">
            <SearchBar variant="expandable" />

            {isAuthenticated ? (
              <div className="relative" ref={dropdownRef}>
                <button
                  type="button"
                  onClick={() => setDropdownOpen((prev) => !prev)}
                  className="flex items-center gap-2 px-3 py-2 rounded-lg transition-colors"
                  style={{ color: "var(--color-brand-primary)" }}
                >
                  <div
                    className="w-8 h-8 flex items-center justify-center"
                    style={{
                      backgroundColor: "var(--color-brand-tertiary)",
                      borderRadius: "50%",
                    }}
                  >
                    <User size={14} color="var(--background)" />
                  </div>

                  <span
                    style={{
                      fontSize: "0.9rem",
                      fontWeight: 500,
                    }}
                  >
                    {displayName}
                  </span>

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
                      style={{
                        backgroundColor: "var(--color-surface)",
                        border: "1px solid var(--color-border)",
                      }}
                    >
                      <Link
                        to="/profile"
                        onClick={() => setDropdownOpen(false)}
                        className="flex items-center gap-3 px-4 py-3 no-underline transition-colors hover:bg-gray-50"
                        style={{ color: "var(--color-brand-primary)" }}
                      >
                        <User size={16} />
                        <span style={{ fontSize: "0.9rem" }}>Profile</span>
                      </Link>

                      <Link
                        to="/history"
                        onClick={() => setDropdownOpen(false)}
                        className="flex items-center gap-3 px-4 py-3 no-underline transition-colors hover:bg-gray-50"
                        style={{ color: "var(--color-brand-primary)" }}
                      >
                        <History size={16} />
                        <span style={{ fontSize: "0.9rem" }}>
                          Riwayat Order
                        </span>
                      </Link>

                      <hr
                        style={{
                          borderColor: "var(--color-border)",
                          margin: 0,
                        }}
                      />

                      <button
                        type="button"
                        onClick={handleLogout}
                        className="w-full flex items-center gap-3 px-4 py-3 transition-colors"
                        style={{
                          color: "var(--color-danger)",
                          fontSize: "0.9rem",
                        }}
                      >
                        <LogOut size={16} />
                        <span>Logout</span>
                      </button>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            ) : (
              <div className="flex items-center gap-3">
                <Link
                  to="/login"
                  className="px-4 py-2 rounded-lg no-underline transition-colors"
                  style={{
                    color: "var(--color-brand-primary)",
                    fontSize: "0.9rem",
                    fontWeight: 500,
                  }}
                >
                  Masuk
                </Link>

                <Link
                  to="/register"
                  className="px-4 py-2 rounded-lg no-underline"
                  style={{
                    backgroundColor: "var(--color-brand-tertiary)",
                    color: "var(--background)",
                    fontSize: "0.9rem",
                    fontWeight: 500,
                  }}
                >
                  Daftar
                </Link>
              </div>
            )}
          </div>

          <div className="flex items-center gap-1 md:hidden">
            <button
              type="button"
              className="p-2 rounded-lg transition-colors"
              style={{ color: "var(--color-brand-primary)" }}
              onMouseDown={() => {
                mobileToggleRef.current = true;
              }}
              onClick={() => setMobileSearchOpen((prev) => !prev)}
              aria-label={
                mobileSearchOpen ? "Tutup pencarian" : "Buka pencarian"
              }
            >
              <Search size={20} />
            </button>

            <button
              type="button"
              className="p-2"
              onClick={() => setMobileMenuOpen((prev) => !prev)}
              style={{ color: "var(--color-brand-primary)" }}
              aria-label={mobileMenuOpen ? "Tutup menu" : "Buka menu"}
            >
              {mobileMenuOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
          </div>
        </div>
      </div>

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
              backgroundColor: "var(--color-surface)",
              borderBottom: "1px solid var(--color-border)",
              padding: "12px 16px",
              zIndex: 60,
              boxShadow: "var(--shadow-navbar)",
            }}
          >
            <SearchBar
              variant="overlay"
              onClose={() => setMobileSearchOpen(false)}
            />
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {mobileMenuOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: "auto" }}
            exit={{ opacity: 0, height: 0 }}
            className="md:hidden overflow-hidden"
            style={{
              borderTop: "1px solid var(--color-border)",
            }}
          >
            <div
              className="px-4 py-3 space-y-1"
              style={{
                backgroundColor: "var(--color-surface)",
              }}
            >
              {isAuthenticated ? (
                <>
                  <Link
                    to="/profile"
                    onClick={() => setMobileMenuOpen(false)}
                    className="flex items-center gap-3 px-3 py-2 rounded-lg no-underline"
                    style={{ color: "var(--color-brand-primary)" }}
                  >
                    <User size={16} />
                    <span>{displayName}</span>
                  </Link>

                  <Link
                    to="/history"
                    onClick={() => setMobileMenuOpen(false)}
                    className="flex items-center gap-3 px-3 py-2 rounded-lg no-underline"
                    style={{ color: "var(--color-brand-primary)" }}
                  >
                    <ShoppingBag size={16} />
                    <span>Riwayat Order</span>
                  </Link>

                  <button
                    type="button"
                    onClick={handleLogout}
                    className="w-full flex items-center gap-3 px-3 py-2 rounded-lg"
                    style={{ color: "var(--color-danger)" }}
                  >
                    <LogOut size={16} />
                    <span>Logout</span>
                  </button>
                </>
              ) : (
                <>
                  <Link
                    to="/login"
                    onClick={() => setMobileMenuOpen(false)}
                    className="block px-3 py-2 rounded-lg no-underline text-center"
                    style={{
                      color: "var(--color-brand-primary)",
                      fontWeight: 500,
                    }}
                  >
                    Masuk
                  </Link>

                  <Link
                    to="/register"
                    onClick={() => setMobileMenuOpen(false)}
                    className="block px-3 py-2 rounded-lg no-underline text-center"
                    style={{
                      backgroundColor: "var(--color-brand-tertiary)",
                      color: "var(--background)",
                      fontWeight: 500,
                    }}
                  >
                    Daftar
                  </Link>
                </>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </nav>
  );
}