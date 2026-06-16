import { useState } from "react";
import { Link, useNavigate, useLocation } from "react-router";
import { LayoutDashboard, Package, Image, Settings, LogOut, Menu, X, Zap, ChevronRight } from "lucide-react";
import { useAuthStore } from "../store/authStore";

interface AdminLayoutProps {
  children: React.ReactNode;
  title: string;
  breadcrumb?: string[];
}

const MENU = [
  { label: "Dashboard", path: "/admin/dashboard", icon: LayoutDashboard },
  { label: "Products", path: "/admin/products", icon: Package },
  { label: "Banners", path: "/admin/banners", icon: Image },
  { label: "Metadata", path: "/admin/metadata", icon: Settings },
];

export function AdminLayout({ children, title, breadcrumb }: AdminLayoutProps) {
  const { adminLogout } = useAuthStore();
  const navigate = useNavigate();
  const location = useLocation();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  const handleLogout = () => {
    adminLogout();
    navigate("/admin/login");
  };

  const Sidebar = () => (
    <aside
      style={{ backgroundColor: "var(--color-primary, #0F1419)", width: 240, minHeight: "100vh", flexShrink: 0 }}
      className="flex flex-col"
    >
      {/* Logo */}
      <div className="flex items-center gap-2 px-6 py-5" style={{ borderBottom: "1px solid rgba(255,255,255,0.08)" }}>
        <div style={{ backgroundColor: "var(--color-tertiary, #2C5EF5)", borderRadius: 7 }} className="w-8 h-8 flex items-center justify-center">
          <Zap size={15} color="#fff" fill="#fff" />
        </div>
        <div>
          <div style={{ color: "#fff", fontWeight: 700, fontSize: "0.95rem" }}>Digitalin</div>
          <div style={{ color: "rgba(255,255,255,0.4)", fontSize: "0.7rem" }}>Admin Panel</div>
        </div>
      </div>

      {/* Menu */}
      <nav className="flex-1 py-4 px-3">
        {MENU.map((item) => {
          const active = location.pathname === item.path;
          const Icon = item.icon;
          return (
            <Link
              key={item.path}
              to={item.path}
              onClick={() => setSidebarOpen(false)}
              className="flex items-center gap-3 px-3 py-2.5 rounded-lg mb-1 no-underline transition-colors"
              style={{
                color: active ? "#fff" : "rgba(255,255,255,0.55)",
                backgroundColor: active ? "rgba(44,94,245,0.25)" : "transparent",
                borderLeft: active ? "3px solid var(--color-tertiary, #2C5EF5)" : "3px solid transparent",
              }}
            >
              <Icon size={17} />
              <span style={{ fontSize: "0.9rem", fontWeight: active ? 600 : 400 }}>{item.label}</span>
            </Link>
          );
        })}
      </nav>

      {/* Logout */}
      <div className="px-3 py-4" style={{ borderTop: "1px solid rgba(255,255,255,0.08)" }}>
        <button
          onClick={handleLogout}
          className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg transition-colors"
          style={{ color: "rgba(255,255,255,0.55)", fontSize: "0.9rem" }}
        >
          <LogOut size={17} />
          <span>Logout</span>
        </button>
      </div>
    </aside>
  );

  return (
    <div style={{ fontFamily: "Inter, sans-serif", display: "flex", minHeight: "100vh", backgroundColor: "var(--color-neutral, #F1F3F5)" }}>
      {/* Desktop Sidebar */}
      <div className="hidden md:flex">
        <Sidebar />
      </div>

      {/* Mobile Sidebar Overlay */}
      {sidebarOpen && (
        <div className="md:hidden fixed inset-0 z-50 flex">
          <div className="fixed inset-0 bg-black/40" onClick={() => setSidebarOpen(false)} />
          <div className="relative z-10">
            <Sidebar />
          </div>
        </div>
      )}

      {/* Main */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Header */}
        <header style={{ backgroundColor: "var(--color-surface, #fff)", borderBottom: "1px solid rgba(0,0,0,0.07)" }} className="px-4 sm:px-8 py-4 flex items-center gap-4 sticky top-0 z-40">
          <button className="md:hidden" onClick={() => setSidebarOpen(true)} style={{ color: "var(--color-primary, #0F1419)" }}>
            <Menu size={20} />
          </button>
          <div className="flex-1">
            <h1 style={{ color: "var(--color-primary, #0F1419)", fontSize: "1.1rem", fontWeight: 700 }}>{title}</h1>
            {breadcrumb && (
              <div className="flex items-center gap-1 mt-0.5">
                {breadcrumb.map((crumb, i) => (
                  <span key={i} className="flex items-center gap-1">
                    {i > 0 && <ChevronRight size={12} style={{ color: "var(--color-secondary, #4A5568)" }} />}
                    <span style={{ color: i === breadcrumb.length - 1 ? "var(--color-tertiary, #2C5EF5)" : "var(--color-secondary, #4A5568)", fontSize: "0.78rem" }}>
                      {crumb}
                    </span>
                  </span>
                ))}
              </div>
            )}
          </div>
          <button
            onClick={handleLogout}
            className="hidden md:flex items-center gap-2 px-3 py-1.5 rounded-lg transition-colors"
            style={{ color: "#ef4444", fontSize: "0.85rem", border: "1px solid rgba(239,68,68,0.2)" }}
          >
            <LogOut size={14} />
            <span>Logout</span>
          </button>
        </header>

        {/* Content */}
        <main className="flex-1 p-4 sm:p-8">{children}</main>
      </div>
    </div>
  );
}
