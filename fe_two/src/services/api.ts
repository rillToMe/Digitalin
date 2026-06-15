import axios, { type AxiosError, type InternalAxiosRequestConfig } from "axios";
import toast from "react-hot-toast";
import { useAuthStore } from "@/stores/authStore";
import type { ApiError } from "@/types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "/api";

/**
 * Pre-configured Axios instance for all Digitalin API calls.
 * - Injects JWT Bearer token from Zustand store on every request.
 * - Handles 401 (Unauthorized) and 429 (Rate Limited) globally.
 */
export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
  timeout: 30_000,
});

// ─── Request Interceptor: Attach JWT Token ──────────────────────────────────

api.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    const { customerToken, adminToken } = useAuthStore.getState();

    // Admin routes get the admin token; all others get the customer token
    const isAdminRoute = config.url?.startsWith("/admin");
    const token = isAdminRoute ? adminToken : customerToken;

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => Promise.reject(error),
);

// ─── Response Interceptor: Global Error Handling ────────────────────────────

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiError>) => {
    const status = error.response?.status;

    switch (status) {
      case 401: {
        // Clear expired/invalid token and redirect to login
        const { clearAll } = useAuthStore.getState();
        clearAll();

        // Determine which login page to redirect to
        const requestUrl = error.config?.url ?? "";
        const isAdmin = requestUrl.startsWith("/admin");
        const redirectPath = isAdmin ? "/admin/login" : "/login";

        toast.error("Sesi Anda telah berakhir. Silakan login kembali.");
        window.location.href = redirectPath;
        break;
      }

      case 429:
        toast.error("Terlalu banyak permintaan. Silakan tunggu beberapa saat.");
        break;

      default:
        // Let individual components handle other errors if needed
        break;
    }

    return Promise.reject(error);
  },
);

export default api;
