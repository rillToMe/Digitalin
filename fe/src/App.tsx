import { BrowserRouter, Navigate, Route, Routes, useLocation } from "react-router";
import { Suspense, lazy, useEffect, useRef } from "react";
import { Toaster } from "sonner";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { GoogleOAuthProvider } from "@react-oauth/google";

import ScrollToTop from "./components/common/ScrollToTop";
import { CustomerLayout } from "./layouts/CustomerLayout";
import { useAuthStore } from "./store/authStore";
import { useThemeStore } from "./store/themeStore";
import PageLoader from "./components/common/PageLoader";
import MaintenanceGate from "./components/common/MaintenanceGate";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 2,
      gcTime: 1000 * 60 * 10,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

// Customer Pages
const HomePage = lazy(() => import("./modules/customer/pages/HomePage"));
const GameDetailPage = lazy(() => import("./modules/customer/pages/GameDetailPage"));
const AuthPage = lazy(() => import("./modules/customer/pages/AuthPage"));
const ForgotPasswordPage = lazy(() => import("./modules/customer/pages/ForgotPasswordPage"));
const MaintenancePage = lazy(() => import("./modules/customer/pages/MaintenancePage"));
const ProfilePage = lazy(() => import("./modules/customer/pages/ProfilePage"));
const HistoryPage = lazy(() => import("./modules/customer/pages/HistoryPage"));
const FaqPage = lazy(() => import("./modules/customer/pages/FaqPage"));
const ContactPage = lazy(() => import("./modules/customer/pages/ContactPage"));
// const VouchersPage = lazy(() => import("./modules/customer/pages/VouchersPage"));

//Admin Pages
const AdminLoginPage = lazy(() => import("./modules/admin/pages/AdminLoginPage"));
const DashboardPage = lazy(() => import("./modules/admin/pages/DashboardPage"));
const OrdersPage = lazy(() => import("./modules/admin/pages/OrdersPage"));
const BannersPage = lazy(() => import("./modules/admin/pages/BannersPage"));
const MetadataPage = lazy(() => import("./modules/admin/pages/MetadataPage"));
const CategoriesPage = lazy(() => import("./modules/admin/pages/CategoriesPage"));
const PopularPage = lazy(() => import("./modules/admin/pages/PopularPage"));
const AdminVouchersPage = lazy(() => import("./modules/admin/pages/VouchersPage"));
const SettingsPage = lazy(() => import("./modules/admin/pages/SettingsPage"));
const AdminManagementPage = lazy(() => import("./modules/admin/pages/AdminManagementPage"));

function CustomerRoute({ children }: { children: React.ReactNode }) {
  return <CustomerLayout>{children}</CustomerLayout>;
}

function ProtectedCustomerRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, hasHydrated } = useAuthStore();
  const location = useLocation();

  if (!hasHydrated) return <PageLoader />;

  if (!isAuthenticated()) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <CustomerLayout>{children}</CustomerLayout>;
}

function AdminEntryRoute() {
  const { isAdmin, isAdminTokenValid, adminLogout, hasHydrated } = useAuthStore();

  if (!hasHydrated) return <PageLoader />;

  if (!isAdmin()) {
    return <Navigate to="/admin/login" replace />;
  }

  if (!isAdminTokenValid()) {
    adminLogout();
    return <Navigate to="/admin/login" replace />;
  }

  return <Navigate to="/admin/dashboard" replace />;
}

function AdminLoginRoute() {
  const { isAdmin, isAdminTokenValid, hasHydrated } = useAuthStore();

  if (!hasHydrated) return <PageLoader />;

  if (isAdmin() && isAdminTokenValid()) {
    return <Navigate to="/admin/dashboard" replace />;
  }

  return <AdminLoginPage />;
}

function ProtectedAdminRoute({ children }: { children: React.ReactNode }) {
  const { isAdmin, isAdminTokenValid, adminLogout, hasHydrated } = useAuthStore();

  if (!hasHydrated) return <PageLoader />;

  if (!isAdmin() || !isAdminTokenValid()) {
    if (isAdmin()) adminLogout();
    return <Navigate to="/admin/login" replace />;
  }

  return children;
}

function ProtectedSuperAdminRoute({ children }: { children: React.ReactNode }) {
  const {
    isAdmin,
    isAdminTokenValid,
    isSuperAdmin,
    adminLogout,
    hasHydrated,
  } = useAuthStore();

  if (!hasHydrated) return <PageLoader />;

  if (!isAdmin() || !isAdminTokenValid()) {
    if (isAdmin()) adminLogout();
    return <Navigate to="/admin/login" replace />;
  }

  if (!isSuperAdmin()) {
    return <Navigate to="/admin/dashboard" replace />;
  }

  return children;
}

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;

function AppProviders({ children }: { children: React.ReactNode }) {
  const content = (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>{children}</BrowserRouter>
    </QueryClientProvider>
  );

  if (googleClientId) {
    return (
      <GoogleOAuthProvider clientId={googleClientId}>
        {content}
      </GoogleOAuthProvider>
    );
  }

  return content;
}

/**
 * Tema mengikuti area saat navigasi SPA antar /admin dan halaman pelanggan:
 * area pelanggan dipaksa gelap, admin memakai tema tersimpan (default terang).
 * Re-hydrate ulang dari storage setiap kali menyeberang batas area — area
 * pelanggan tidak pernah menulis pilihannya ke storage (lihat themeStore).
 */
function ThemeRouteSync() {
  const location = useLocation();
  const isAdminRoute = location.pathname.startsWith("/admin");
  const prev = useRef(isAdminRoute);

  useEffect(() => {
    if (prev.current === isAdminRoute) return;
    prev.current = isAdminRoute;
    void useThemeStore.persist.rehydrate();
  }, [isAdminRoute]);

  return null;
}

export default function App() {
  return (
    <AppProviders>
      <ScrollToTop />
      <ThemeRouteSync />

      <Toaster
        position="top-right"
        richColors
        closeButton
      />

      <Suspense fallback={<PageLoader />}>
        <Routes>
          {/* Customer — semua dibungkus MaintenanceGate (guard global):
              saat maintenance aktif, user dialihkan ke /maintenance dari
              route mana pun, termasuk deep link dan refresh. /maintenance
              dan /admin/* sengaja di luar guard. */}
          <Route element={<MaintenanceGate />}>
            <Route
              path="/"
              element={
                <CustomerRoute>
                  <HomePage />
                </CustomerRoute>
              }
            />

            <Route
              path="/game/:slug"
              element={
                <CustomerRoute>
                  <GameDetailPage />
                </CustomerRoute>
              }
            />

            <Route path="/login" element={<AuthPage />} />

            <Route path="/register" element={<AuthPage />} />

            <Route path="/forgot-password" element={<ForgotPasswordPage />} />

            <Route
              path="/profile"
              element={
                <ProtectedCustomerRoute>
                  <ProfilePage />
                </ProtectedCustomerRoute>
              }
            />

            <Route
              path="/history"
              element={
                <ProtectedCustomerRoute>
                  <HistoryPage />
                </ProtectedCustomerRoute>
              }
            />

            <Route
              path="/faq"
              element={
                <CustomerRoute>
                  <FaqPage />
                </CustomerRoute>
              }
            />

            <Route
              path="/contact"
              element={
                <CustomerRoute>
                  <ContactPage />
                </CustomerRoute>
              }
            />

            {/* <Route
              path="/vouchers"
              element={
                <CustomerRoute>
                  <VouchersPage />
                </CustomerRoute>
              }
            /> */}
          </Route>

          <Route path="/maintenance" element={<MaintenancePage />} />

          {/* Admin */}

          <Route path="/admin" element={<AdminEntryRoute />} />

          <Route path="/admin/login" element={<AdminLoginRoute />} />

          <Route
            path="/admin/dashboard"
            element={
              <ProtectedAdminRoute>
                <DashboardPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/orders"
            element={
              <ProtectedAdminRoute>
                <OrdersPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/banners"
            element={
              <ProtectedAdminRoute>
                <BannersPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/metadata"
            element={
              <ProtectedAdminRoute>
                <MetadataPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/categories"
            element={
              <ProtectedAdminRoute>
                <CategoriesPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/popular"
            element={
              <ProtectedAdminRoute>
                <PopularPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/vouchers"
            element={
              <ProtectedAdminRoute>
                <AdminVouchersPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/settings"
            element={
              <ProtectedAdminRoute>
                <SettingsPage />
              </ProtectedAdminRoute>
            }
          />

          <Route
            path="/admin/admins"
            element={
              <ProtectedSuperAdminRoute>
                <AdminManagementPage />
              </ProtectedSuperAdminRoute>
            }
          />

          {/* Fallback */}

          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Suspense>
    </AppProviders>
  );
}
