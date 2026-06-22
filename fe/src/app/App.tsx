import { BrowserRouter, Routes, Route, Navigate, useLocation } from "react-router";
import { Toaster } from "sonner";
import ScrollToTop from "./components/ScrollToTop";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { useAuthStore } from "../store/authStore";
import { CustomerLayout } from "../layouts/CustomerLayout";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 2,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
});

// Customer pages
import HomePage from "../modules/customer/pages/HomePage";
import GameDetailPage from "../modules/customer/pages/GameDetailPage";
import LoginPage from "../modules/customer/pages/LoginPage";
import RegisterPage from "../modules/customer/pages/RegisterPage";
import ProfilePage from "../modules/customer/pages/ProfilePage";
import HistoryPage from "../modules/customer/pages/HistoryPage";
import FaqPage from "../modules/customer/pages/FaqPage";
import ContactPage from "../modules/customer/pages/ContactPage";
import VouchersPage from "../modules/customer/pages/VouchersPage";

// Admin pages
import AdminLoginPage from "../modules/admin/pages/AdminLoginPage";
import DashboardPage from "../modules/admin/pages/DashboardPage";
import BannersPage from "../modules/admin/pages/BannersPage";
import MetadataPage from "../modules/admin/pages/MetadataPage";
import AdminVouchersPage from "../modules/admin/pages/VouchersPage";

function CustomerRoute({ children }: { children: React.ReactNode }) {
  return <CustomerLayout>{children}</CustomerLayout>;
}

function ProtectedCustomerRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, hasHydrated } = useAuthStore();
  const location = useLocation();
  if (!hasHydrated) return null;
  if (!isAuthenticated()) return <Navigate to="/login" state={{ from: location }} replace />;
  return <CustomerLayout>{children}</CustomerLayout>;
}

/**
 * Smart /admin entry route.
 * - No token / token expired → /admin/login
 * - Valid token → /admin/dashboard
 * Validates JWT exp claim before allowing access.
 */
function AdminEntryRoute() {
  const { isAdmin, isAdminTokenValid, adminLogout, hasHydrated } = useAuthStore();

  if (!hasHydrated) return null;

  // No token at all → login
  if (!isAdmin()) return <Navigate to="/admin/login" replace />;

  // Token exists but expired → clear and redirect to login
  if (!isAdminTokenValid()) {
    adminLogout();
    return <Navigate to="/admin/login" replace />;
  }

  // Token is present and not expired → dashboard
  return <Navigate to="/admin/dashboard" replace />;
}

/**
 * If admin is already logged in with a valid token,
 * redirect /admin/login → /admin/dashboard automatically.
 */
function AdminLoginRoute() {
  const { isAdmin, isAdminTokenValid, hasHydrated } = useAuthStore();

  if (!hasHydrated) return null;

  // Already authenticated with valid token → go to dashboard
  if (isAdmin() && isAdminTokenValid()) {
    return <Navigate to="/admin/dashboard" replace />;
  }

  return <AdminLoginPage />;
}

function ProtectedAdminRoute({ children }: { children: React.ReactNode }) {
  const { isAdmin, isAdminTokenValid, adminLogout, hasHydrated } = useAuthStore();
  if (!hasHydrated) return null;
  if (!isAdmin() || !isAdminTokenValid()) {
    if (isAdmin()) adminLogout(); // clear expired session
    return <Navigate to="/admin/login" replace />;
  }
  return <>{children}</>;
}

const googleClientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;

function AppProviders({ children }: { children: React.ReactNode }) {
  const inner = (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        {children}
      </BrowserRouter>
    </QueryClientProvider>
  );

  if (googleClientId) {
    return <GoogleOAuthProvider clientId={googleClientId}>{inner}</GoogleOAuthProvider>;
  }

  return inner;
}

export default function App() {
  return (
    <AppProviders>
      <ScrollToTop />
      <Toaster position="top-right" richColors closeButton />
      <Routes>
        {/* Customer routes */}
        <Route path="/" element={<CustomerRoute><HomePage /></CustomerRoute>} />
        <Route path="/game/:slug" element={<CustomerRoute><GameDetailPage /></CustomerRoute>} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/profile" element={<ProtectedCustomerRoute><ProfilePage /></ProtectedCustomerRoute>} />
        <Route path="/history" element={<ProtectedCustomerRoute><HistoryPage /></ProtectedCustomerRoute>} />
        <Route path="/faq" element={<CustomerRoute><FaqPage /></CustomerRoute>} />
        <Route path="/contact" element={<CustomerRoute><ContactPage /></CustomerRoute>} />
        <Route path="/vouchers" element={<CustomerRoute><VouchersPage /></CustomerRoute>} />

        {/* Admin routes */}
        <Route path="/admin" element={<AdminEntryRoute />} />
        <Route path="/admin/login" element={<AdminLoginRoute />} />
        <Route path="/admin/dashboard" element={<ProtectedAdminRoute><DashboardPage /></ProtectedAdminRoute>} />
        <Route path="/admin/banners" element={<ProtectedAdminRoute><BannersPage /></ProtectedAdminRoute>} />
        <Route path="/admin/metadata" element={<ProtectedAdminRoute><MetadataPage /></ProtectedAdminRoute>} />
        <Route path="/admin/vouchers" element={<ProtectedAdminRoute><AdminVouchersPage /></ProtectedAdminRoute>} />

        {/* Fallback */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppProviders>
  );
}
