import { BrowserRouter, Routes, Route } from "react-router-dom";

// ── Layouts ──
import { CustomerLayout } from "@/layouts/CustomerLayout";
import { AdminLayout } from "@/layouts/AdminLayout";

// ── Customer Pages ──
import { HomePage } from "@/pages/customer/HomePage";
import { GamesPage } from "@/pages/customer/GamesPage";
import { GameDetailPage } from "@/pages/customer/GameDetailPage";
import { LoginPage } from "@/pages/customer/LoginPage";
import { RegisterPage } from "@/pages/customer/RegisterPage";
import { ProfilePage } from "@/pages/customer/ProfilePage";
import { OrderHistoryPage } from "@/pages/customer/OrderHistoryPage";

// ── Admin Pages ──
import { AdminLoginPage } from "@/pages/admin/AdminLoginPage";
import { AdminDashboardPage } from "@/pages/admin/AdminDashboardPage";
import { AdminProductsPage } from "@/pages/admin/AdminProductsPage";
import { AdminOrdersPage } from "@/pages/admin/AdminOrdersPage";
import { AdminBannersPage } from "@/pages/admin/AdminBannersPage";
import { AdminSystemPage } from "@/pages/admin/AdminSystemPage";

// ── Shared ──
import { NotFoundPage } from "@/pages/NotFoundPage";

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* ═══════════════════════════════════════════════════════════════════
         *  PUBLIC / CUSTOMER ROUTES
         * ═══════════════════════════════════════════════════════════════════ */}
        <Route element={<CustomerLayout />}>
          <Route index element={<HomePage />} />
          <Route path="games" element={<GamesPage />} />
          <Route path="games/:brandSlug" element={<GameDetailPage />} />
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route path="profile" element={<ProfilePage />} />
          <Route path="my-history" element={<OrderHistoryPage />} />
        </Route>

        {/* ═══════════════════════════════════════════════════════════════════
         *  ADMIN ROUTES
         * ═══════════════════════════════════════════════════════════════════ */}
        {/* Admin login is a standalone page (no sidebar layout) */}
        <Route path="admin/login" element={<AdminLoginPage />} />

        <Route path="admin" element={<AdminLayout />}>
          <Route index element={<AdminDashboardPage />} />
          <Route path="products" element={<AdminProductsPage />} />
          <Route path="orders" element={<AdminOrdersPage />} />
          <Route path="banners" element={<AdminBannersPage />} />
          <Route path="system" element={<AdminSystemPage />} />
        </Route>

        {/* ═══════════════════════════════════════════════════════════════════
         *  404 — Catch-all
         * ═══════════════════════════════════════════════════════════════════ */}
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
