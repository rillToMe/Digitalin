import { Outlet, NavLink, useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Package,
  ShoppingCart,
  Image,
  Settings,
  LogOut,
  Gamepad2,
} from "lucide-react";
import { useAuthStore } from "@/stores/authStore";
import { cn } from "@/lib/utils";

const adminNavItems = [
  { to: "/admin", icon: LayoutDashboard, label: "Dashboard", end: true },
  { to: "/admin/products", icon: Package, label: "Produk", end: false },
  { to: "/admin/orders", icon: ShoppingCart, label: "Pesanan", end: false },
  { to: "/admin/banners", icon: Image, label: "Banner", end: false },
  { to: "/admin/system", icon: Settings, label: "Sistem", end: false },
] as const;

export function AdminLayout() {
  const { setAdminToken } = useAuthStore();
  const navigate = useNavigate();

  const handleLogout = () => {
    setAdminToken(null);
    navigate("/admin/login");
  };

  return (
    <div className="flex min-h-screen bg-neutral">
      {/* ── Sidebar ── */}
      <aside className="glass fixed inset-y-0 left-0 z-40 flex w-64 flex-col border-r border-surface/40">
        {/* Logo */}
        <div className="flex h-16 items-center gap-2 border-b border-secondary/10 px-6">
          <Gamepad2 className="h-6 w-6 text-tertiary" />
          <span className="text-lg font-semibold text-primary">Digitalin</span>
          <span className="rounded-sm bg-tertiary/10 px-1.5 py-0.5 text-xs font-medium text-tertiary">
            Admin
          </span>
        </div>

        {/* Nav */}
        <nav className="flex-1 space-y-1 px-3 py-4">
          {adminNavItems.map(({ to, icon: Icon, label, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                  isActive
                    ? "bg-tertiary/10 text-tertiary"
                    : "text-secondary hover:bg-surface/60 hover:text-primary",
                )
              }
            >
              <Icon className="h-4 w-4" />
              {label}
            </NavLink>
          ))}
        </nav>

        {/* Logout */}
        <div className="border-t border-secondary/10 p-3">
          <button
            type="button"
            onClick={handleLogout}
            className={cn(
              "flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm font-medium",
              "text-secondary transition-colors hover:bg-red-50 hover:text-red-500",
            )}
          >
            <LogOut className="h-4 w-4" />
            Keluar
          </button>
        </div>
      </aside>

      {/* ── Main Content ── */}
      <div className="flex-1 pl-64">
        <main className="mx-auto max-w-6xl px-6 py-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
