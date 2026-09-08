import { Link, useLocation } from "react-router";
import {
  LayoutDashboard,
  ClipboardList,
  Gamepad2,
  LayoutGrid,
  Flame,
  Image as ImageIcon,
  Ticket,
  CircleHelp,
  ShieldCheck,
  Settings,
  CreditCard,
  LogOut,
  PanelLeftClose,
  PanelLeftOpen,
  X,
} from "lucide-react";
import type { LucideIcon } from "lucide-react";

interface AdminNavItem {
  label: string;
  path: string;
  icon: LucideIcon;
  superAdminOnly?: boolean;
}

interface AdminNavGroup {
  label: string;
  items: AdminNavItem[];
}

const NAV_GROUPS: AdminNavGroup[] = [
  {
    label: "Operasional",
    items: [
      { label: "Dashboard", path: "/admin/dashboard", icon: LayoutDashboard },
      { label: "Orders", path: "/admin/orders", icon: ClipboardList },
    ],
  },
  {
    label: "Katalog",
    items: [
      { label: "Metadata Game", path: "/admin/metadata", icon: Gamepad2 },
      { label: "Kategori", path: "/admin/categories", icon: LayoutGrid },
      { label: "Game Populer", path: "/admin/popular", icon: Flame },
      { label: "Banners", path: "/admin/banners", icon: ImageIcon },
      { label: "Vouchers", path: "/admin/vouchers", icon: Ticket },
      { label: "FAQ", path: "/admin/faqs", icon: CircleHelp },
    ],
  },
  {
    label: "Sistem",
    items: [
      { label: "Kelola Admin", path: "/admin/admins", icon: ShieldCheck, superAdminOnly: true },
      { label: "Pembayaran", path: "/admin/payment-methods", icon: CreditCard },
      { label: "Settings", path: "/admin/settings", icon: Settings },
    ],
  },
];

interface AdminSidebarProps {
  isSuperAdmin: boolean;
  onLogout: () => void;
  /** Icon-rail mode. Ignored in the drawer variant. */
  collapsed?: boolean;
  onToggleCollapse?: () => void;
  /** "drawer" swaps the rail toggle for a close button (mobile). */
  variant?: "static" | "drawer";
  onClose?: () => void;
}

export function AdminSidebar({
  isSuperAdmin,
  onLogout,
  collapsed = false,
  onToggleCollapse,
  variant = "static",
  onClose,
}: AdminSidebarProps) {
  const { pathname } = useLocation();
  const isDrawer = variant === "drawer";
  const railed = collapsed && !isDrawer;

  return (
    <aside
      className={`admin-sidebar${railed ? " admin-sidebar--collapsed" : ""}`}
      aria-label="Navigasi admin"
    >
      <div className="admin-brand">
        <img src="/favicon.svg" alt="Digitalin" className="admin-brand-logo" />
        {!railed && (
          <span className="admin-brand-text">
            <span className="admin-brand-name">Digitalin</span>
            <span className="admin-brand-role">{isSuperAdmin ? "Super Admin" : "Admin"}</span>
          </span>
        )}
        {isDrawer && (
          <button type="button" className="admin-iconbtn" onClick={onClose} aria-label="Tutup menu">
            <X size={18} aria-hidden="true" />
          </button>
        )}
      </div>

      <nav className="admin-nav">
        {NAV_GROUPS.map((group) => {
          const items = group.items.filter((item) => !item.superAdminOnly || isSuperAdmin);
          if (items.length === 0) return null;

          return (
            <div key={group.label} className="admin-nav-group">
              {!railed && <span className="admin-nav-label">{group.label}</span>}
              {items.map((item) => {
                const active = pathname === item.path;
                const Icon = item.icon;
                return (
                  <Link
                    key={item.path}
                    to={item.path}
                    onClick={onClose}
                    className={`admin-nav-item${active ? " admin-nav-item--active" : ""}`}
                    aria-current={active ? "page" : undefined}
                    title={railed ? item.label : undefined}
                  >
                    <span className="admin-nav-icon">
                      <Icon size={18} aria-hidden="true" />
                    </span>
                    {!railed && <span className="admin-nav-text">{item.label}</span>}
                  </Link>
                );
              })}
            </div>
          );
        })}
      </nav>

      <div className="admin-sidebar-footer">
        {!isDrawer && (
          <button
            type="button"
            className="admin-sidebar-action"
            onClick={onToggleCollapse}
            aria-pressed={railed}
            aria-label={railed ? "Perluas sidebar" : "Ringkas sidebar"}
            title={railed ? "Perluas sidebar" : undefined}
          >
            <span className="admin-nav-icon">
              {railed ? <PanelLeftOpen size={18} aria-hidden="true" /> : <PanelLeftClose size={18} aria-hidden="true" />}
            </span>
            {!railed && <span className="admin-nav-text">Ringkas</span>}
          </button>
        )}
        <button
          type="button"
          className="admin-sidebar-action admin-sidebar-action--danger"
          onClick={onLogout}
          title={railed ? "Logout" : undefined}
        >
          <span className="admin-nav-icon">
            <LogOut size={18} aria-hidden="true" />
          </span>
          {!railed && <span className="admin-nav-text">Logout</span>}
        </button>
      </div>
    </aside>
  );
}
