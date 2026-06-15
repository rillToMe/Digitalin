import { Outlet, Link, useNavigate } from "react-router-dom";
import { Gamepad2, User, LogOut, History } from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { cn } from "@/lib/utils";

export function CustomerLayout() {
  const { customerToken, setCustomerToken } = useAuthStore();
  const navigate = useNavigate();
  const isLoggedIn = !!customerToken;

  const handleLogout = () => {
    setCustomerToken(null);
    navigate("/");
  };

  return (
    <div className="flex min-h-screen flex-col bg-neutral">
      {/* ── Navigation Bar ── */}
      <header className="glass sticky top-0 z-50 border-b border-surface/40">
        <nav className="mx-auto flex h-16 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
          {/* Logo */}
          <Link to="/" className="flex items-center gap-2">
            <Gamepad2 className="h-6 w-6 text-tertiary" />
            <span className="text-lg font-semibold text-primary">Digitalin</span>
          </Link>

          {/* Nav Links */}
          <div className="flex items-center gap-4">
            <Link
              to="/games"
              className="text-sm font-medium text-secondary transition-colors hover:text-primary"
            >
              Games
            </Link>

            {isLoggedIn ? (
              <>
                <Link
                  to="/my-history"
                  className={cn(
                    "flex items-center gap-1.5 text-sm font-medium",
                    "text-secondary transition-colors hover:text-primary",
                  )}
                >
                  <History className="h-4 w-4" />
                  Riwayat
                </Link>
                <Link
                  to="/profile"
                  className={cn(
                    "flex items-center gap-1.5 text-sm font-medium",
                    "text-secondary transition-colors hover:text-primary",
                  )}
                >
                  <User className="h-4 w-4" />
                  Profil
                </Link>
                <button
                  type="button"
                  onClick={handleLogout}
                  className={cn(
                    "flex items-center gap-1.5 text-sm font-medium",
                    "text-secondary transition-colors hover:text-red-500",
                  )}
                >
                  <LogOut className="h-4 w-4" />
                  Keluar
                </button>
              </>
            ) : (
              <Link
                to="/login"
                className={cn(
                  "rounded-md bg-tertiary px-4 py-2 text-sm font-medium",
                  "text-on-primary transition-colors hover:bg-tertiary/90",
                )}
              >
                Masuk
              </Link>
            )}
          </div>
        </nav>
      </header>

      {/* ── Main Content ── */}
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
        <Outlet />
      </main>

      {/* ── Footer ── */}
      <footer className="border-t border-secondary/10 py-6 text-center text-sm text-secondary">
        <p>&copy; {new Date().getFullYear()} Digitalin. All rights reserved.</p>
      </footer>
    </div>
  );
}
