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
} from "lucide-react";

import { useAuthStore } from "../../store/authStore";
import { authService } from "../../services/api";
import SearchBar from "./SearchBar";
import ThemeToggle from "./ThemeToggle";

const navLinks = [
  { to: "/", label: "Beranda" },
  { to: "/vouchers", label: "Voucher" },
  { to: "/history", label: "Cek Transaksi" },
];

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
      }}
    >
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-x-0 bottom-0 h-px"
        style={{
          background:
            "linear-gradient(90deg, transparent, var(--color-navbar-border) 18%, var(--color-navbar-border) 82%, transparent)",
        }}
      />

      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          <Link to="/" className="flex items-center gap-2 no-underline">
            <div
              className="w-8 h-8 flex items-center justify-center"
            >
              <img
                src="/favicon.svg"
                alt="Digitalin Logo"
                className="h-10 w-auto"
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

          <div className="hidden lg:flex min-w-0 flex-1 items-center justify-end">
            <div
              aria-hidden="true"
              className="mx-5 h-6 w-px flex-shrink-0"
              style={{ backgroundColor: "var(--color-navbar-border)" }}
            />

            <div className="flex items-center gap-5">
              {navLinks.map((item) => {
                const isActive =
                  item.to === "/"
                    ? location.pathname === "/"
                    : location.pathname.startsWith(item.to);

                return (
                  <Link
                    key={item.to}
                    to={item.to}
                    aria-current={isActive ? "page" : undefined}
                    className="group relative whitespace-nowrap py-2 no-underline"
                    style={{
                      color: isActive
                        ? "var(--color-brand-primary)"
                        : "var(--color-brand-secondary)",
                      fontSize: "0.78rem",
                      fontWeight: 600,
                      letterSpacing: "0.04em",
                    }}
                  >
                    {item.label}
                    <span
                      aria-hidden="true"
                      className={`absolute inset-x-0 -bottom-1 h-px origin-left transition-transform duration-150 ${
                        isActive
                          ? "scale-x-100"
                          : "scale-x-0 group-hover:scale-x-100"
                      }`}
                      style={{ backgroundColor: "var(--color-brand-tertiary)" }}
                    />
                  </Link>
                );
              })}
            </div>

            <div
              aria-hidden="true"
              className="mx-5 h-6 w-px flex-shrink-0"
              style={{ backgroundColor: "var(--color-navbar-border)" }}
            />

            <SearchBar variant="expandable" />

            <div
              aria-hidden="true"
              className="mx-4 h-6 w-px flex-shrink-0"
              style={{ backgroundColor: "var(--color-navbar-border)" }}
            />

            <div className="flex items-center gap-3 flex-shrink-0">
              <ThemeToggle />

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
                    <User size={14} color="var(--color-on-primary)" />
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

                {dropdownOpen && (
                  <div
                    className="animate-pop-in absolute right-0 mt-2 w-48 rounded-xl shadow-lg overflow-hidden"
                    style={{
                      backgroundColor: "var(--color-surface)",
                      border: "1px solid var(--color-border)",
                    }}
                  >
                    <Link
                      to="/profile"
                      onClick={() => setDropdownOpen(false)}
                      className="flex items-center gap-3 px-4 py-3 no-underline transition-colors hover:bg-(--color-hover-surface)"
                      style={{ color: "var(--color-brand-primary)" }}
                    >
                      <User size={16} />
                      <span style={{ fontSize: "0.9rem" }}>Profile</span>
                    </Link>

                    <Link
                      to="/history"
                      onClick={() => setDropdownOpen(false)}
                      className="flex items-center gap-3 px-4 py-3 no-underline transition-colors hover:bg-(--color-hover-surface)"
                      style={{ color: "var(--color-brand-primary)" }}
                    >
                      <History size={16} />
                      <span style={{ fontSize: "0.9rem" }}>Riwayat Order</span>
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
                  </div>
                )}
              </div>
              ) : (
              <div className="flex items-center gap-3">
                <Link
                  to="/login"
                  className="pressable px-4 py-2 rounded-lg no-underline transition-colors"
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
                  className="pressable border px-4 py-2 no-underline"
                  style={{
                    backgroundColor: "var(--color-brand-tertiary)",
                    borderColor: "var(--color-brand-tertiary)",
                    color: "var(--color-on-primary)",
                    fontSize: "0.9rem",
                    fontWeight: 600,
                    clipPath:
                      "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
                  }}
                >
                  Daftar
                </Link>
              </div>
              )}
            </div>
          </div>

          <div className="flex items-center gap-1 lg:hidden">
            <ThemeToggle />

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

      {mobileSearchOpen && (
        <div
          ref={mobileSearchRef}
          className="animate-slide-down-in lg:hidden"
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
        </div>
      )}

      <div
        className="collapse-panel lg:hidden"
        data-open={mobileMenuOpen}
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
            <div className="mb-3 grid gap-1 border-b pb-3" style={{ borderColor: "var(--color-navbar-border)" }}>
              {navLinks.map((item) => {
                const isActive =
                  item.to === "/"
                    ? location.pathname === "/"
                    : location.pathname.startsWith(item.to);

                return (
                  <Link
                    key={item.to}
                    to={item.to}
                    onClick={() => setMobileMenuOpen(false)}
                    aria-current={isActive ? "page" : undefined}
                    className="relative px-3 py-2 no-underline"
                    style={{
                      color: isActive
                        ? "var(--color-brand-primary)"
                        : "var(--color-brand-secondary)",
                      fontSize: "0.875rem",
                      fontWeight: isActive ? 600 : 500,
                    }}
                  >
                    {item.label}
                    {isActive && (
                      <span
                        aria-hidden="true"
                        className="absolute inset-y-2 left-0 w-px"
                        style={{ backgroundColor: "var(--color-brand-tertiary)" }}
                      />
                    )}
                  </Link>
                );
              })}
            </div>

            {isAuthenticated ? (
            <>
              <Link
                to="/profile"
                onClick={() => setMobileMenuOpen(false)}
                className="pressable flex items-center gap-3 px-3 py-2 rounded-lg no-underline"
                style={{ color: "var(--color-brand-primary)" }}
              >
                <User size={16} />
                <span>{displayName}</span>
              </Link>

              <Link
                to="/history"
                onClick={() => setMobileMenuOpen(false)}
                className="pressable flex items-center gap-3 px-3 py-2 rounded-lg no-underline"
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
                className="pressable block px-3 py-2 rounded-lg no-underline text-center"
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
                className="pressable block border px-3 py-2 no-underline text-center"
                style={{
                  backgroundColor: "var(--color-brand-tertiary)",
                  borderColor: "var(--color-brand-tertiary)",
                  color: "var(--background)",
                  fontWeight: 600,
                  clipPath:
                    "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
                }}
              >
                Daftar
              </Link>
            </>
          )}
        </div>
      </div>
    </nav>
  );
}
