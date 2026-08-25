import { useEffect, useState } from "react";
import { useNavigate } from "react-router";
import { ChevronRight, Menu } from "lucide-react";
import { useAuthStore } from "../store/authStore";
import { AdminSidebar } from "../components/admin/sidenav/AdminSidebar";
import ThemeToggle from "../components/common/ThemeToggle";

interface AdminLayoutProps {
  children: React.ReactNode;
  title: string;
  breadcrumb?: string[];
}

const COLLAPSE_KEY = "admin.sidebar.collapsed";

export function AdminLayout({ children, title, breadcrumb }: AdminLayoutProps) {
  const { adminLogout, isSuperAdmin } = useAuthStore();
  const navigate = useNavigate();
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(COLLAPSE_KEY) === "1");

  const superAdmin = isSuperAdmin();

  const toggleCollapse = () => {
    const next = !collapsed;
    setCollapsed(next);
    localStorage.setItem(COLLAPSE_KEY, next ? "1" : "0");
  };

  const handleLogout = () => {
    adminLogout();
    navigate("/admin/login");
  };

  useEffect(() => {
    if (!drawerOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") setDrawerOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [drawerOpen]);

  return (
    <div className="admin-shell">
      <div className="admin-sidebar-static">
        <AdminSidebar
          isSuperAdmin={superAdmin}
          onLogout={handleLogout}
          collapsed={collapsed}
          onToggleCollapse={toggleCollapse}
        />
      </div>

      {drawerOpen && (
        <>
          <div className="admin-overlay" onClick={() => setDrawerOpen(false)} />
          <div className="admin-drawer" role="dialog" aria-modal="true" aria-label="Menu admin">
            <AdminSidebar
              variant="drawer"
              isSuperAdmin={superAdmin}
              onLogout={handleLogout}
              onClose={() => setDrawerOpen(false)}
            />
          </div>
        </>
      )}

      <div className="admin-main">
        <header className="admin-topbar">
          <button
            type="button"
            className="admin-iconbtn admin-menu-btn"
            onClick={() => setDrawerOpen(true)}
            aria-label="Buka menu"
            aria-expanded={drawerOpen}
          >
            <Menu size={20} aria-hidden="true" />
          </button>

          <div className="admin-topbar-heading">
            <h1 className="admin-title">{title}</h1>
            {breadcrumb && breadcrumb.length > 0 && (
              <nav aria-label="Breadcrumb">
                <ol className="admin-crumbs">
                  {breadcrumb.map((crumb, i) => (
                    <li
                      key={`${crumb}-${i}`}
                      className="admin-crumb"
                      aria-current={i === breadcrumb.length - 1 ? "page" : undefined}
                    >
                      {i > 0 && <ChevronRight size={11} aria-hidden="true" />}
                      {crumb}
                    </li>
                  ))}
                </ol>
              </nav>
            )}
          </div>

          <ThemeToggle />
        </header>

        <main className="admin-canvas">{children}</main>
      </div>
    </div>
  );
}
