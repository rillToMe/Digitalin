import { BrowserRouter, Routes, Route, Navigate, useLocation } from "react-router";
import { Toaster } from "sonner";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
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

// Admin pages
import AdminLoginPage from "../modules/admin/pages/AdminLoginPage";
import DashboardPage from "../modules/admin/pages/DashboardPage";
import ProductsPage from "../modules/admin/pages/ProductsPage";
import BannersPage from "../modules/admin/pages/BannersPage";
import MetadataPage from "../modules/admin/pages/MetadataPage";

function CustomerRoute({ children }: { children: React.ReactNode }) {
  return <CustomerLayout>{children}</CustomerLayout>;
}

function ProtectedCustomerRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuthStore();
  const location = useLocation();
  if (!isAuthenticated) return <Navigate to="/login" state={{ from: location }} replace />;
  return <CustomerLayout>{children}</CustomerLayout>;
}

function ProtectedAdminRoute({ children }: { children: React.ReactNode }) {
  const { isAdmin } = useAuthStore();
  if (!isAdmin) return <Navigate to="/admin/login" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Toaster position="top-right" richColors closeButton />
        <Routes>
        {/* Customer routes */}
        <Route path="/" element={<CustomerRoute><HomePage /></CustomerRoute>} />
        <Route path="/game/:slug" element={<CustomerRoute><GameDetailPage /></CustomerRoute>} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/profile" element={<ProtectedCustomerRoute><ProfilePage /></ProtectedCustomerRoute>} />
        <Route path="/history" element={<ProtectedCustomerRoute><HistoryPage /></ProtectedCustomerRoute>} />

        {/* Admin routes */}
        <Route path="/admin/login" element={<AdminLoginPage />} />
        <Route path="/admin/dashboard" element={<ProtectedAdminRoute><DashboardPage /></ProtectedAdminRoute>} />
        <Route path="/admin/products" element={<ProtectedAdminRoute><ProductsPage /></ProtectedAdminRoute>} />
        <Route path="/admin/banners" element={<ProtectedAdminRoute><BannersPage /></ProtectedAdminRoute>} />
        <Route path="/admin/metadata" element={<ProtectedAdminRoute><MetadataPage /></ProtectedAdminRoute>} />

        {/* Fallback */}
        <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
