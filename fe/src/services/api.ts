import axios from "axios";
import { toast } from "sonner";
import { useAuthStore } from "../store/authStore";
import type {
  AuthTokenResponse,
  LoginPayload,
  RegisterPayload,
  RequestOtpPayload,
  AdminLoginPayload,
  UpdateProfilePayload,
  ProfileResponse,
  BrandsResponse,
  BrandDetailResponse,
  CreateOrderPayload,
  CreateOrderResponse,
  PaginatedOrdersResponse,
  BannersResponse,
  CreateBannerResponse,
  DashboardStatsResponse,
  ChartResponse,
  ChartQueryParams,
  MetadataListResponse,
  UpdateMetadataPayload,
  GameMetadataItem,
  AdminProductsListResponse,
  AdminProductResponse,
  CreateProductPayload,
  SetMarginPayload,
  MessageResponse,
  UploadImageResponse,
  AdminProductsQueryParams,
  MidtransExpiryResponse,
  UpdateMidtransExpiryPayload,
  SyncResponse,
} from "../api/index";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5210",
  headers: { "Content-Type": "application/json" },
});

// Request interceptor - attach token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem("token");
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

// Response interceptor - error handling
api.interceptors.response.use(
  (res) => res,
  (error) => {
    if (error.response?.status === 401) {
      const store = useAuthStore.getState();
      const requestUrl: string = error.config?.url ?? "";
      const isAdminRequest = requestUrl.startsWith("/admin");

      if (isAdminRequest) {
        // Admin 401 → clear admin session, redirect to admin login
        store.adminLogout();
        if (!window.location.pathname.startsWith("/admin/login")) {
          toast.error("Session admin telah berakhir, silakan login kembali.");
          window.location.href = "/admin/login";
        }
      } else {
        // Customer 401 → clear customer session, redirect to customer login
        store.logout();
        if (!window.location.pathname.includes("/login")) {
          window.location.href = "/login";
        }
      }
    }
    return Promise.reject(error);
  }
);

// Auth
export const authService = {
  requestOtp: (payload: RequestOtpPayload) =>
    api.post<MessageResponse>("/auth/request-otp", payload).then((r) => r.data),
  register: (payload: RegisterPayload) =>
    api.post<AuthTokenResponse>("/auth/register", payload).then((r) => r.data),
  login: (payload: LoginPayload) =>
    api.post<AuthTokenResponse>("/auth/login", payload).then((r) => r.data),
  getProfile: () =>
    api.get<ProfileResponse>("/auth/profile").then((r) => r.data),
  updateProfile: (payload: UpdateProfilePayload) =>
    api.put<MessageResponse>("/auth/profile", payload).then((r) => r.data),
};

// Admin auth
export const adminAuthService = {
  login: (payload: AdminLoginPayload) =>
    api.post<AuthTokenResponse>("/admin/auth/login", payload).then((r) => r.data),
};

// Brands
export const brandService = {
  getAll: () =>
    api.get<BrandsResponse>("/games/brands").then((r) => r.data),
  getBySlug: (slug: string) =>
    api.get<BrandDetailResponse>(`/games/${slug}`).then((r) => r.data),
};

// Orders
export const orderService = {
  create: (payload: CreateOrderPayload) =>
    api.post<CreateOrderResponse>("/orders", payload).then((r) => r.data),
  getHistory: (page = 1, limit = 10) =>
    api
      .get<PaginatedOrdersResponse>("/orders/my-history", { params: { page, limit } })
      .then((r) => r.data),
};

// Banners
export const bannerService = {
  getAll: () =>
    api.get<BannersResponse>("/banners").then((r) => r.data),
};

// Profile
export const profileService = {
  get: () => authService.getProfile(),
  update: (payload: UpdateProfilePayload) => authService.updateProfile(payload),
};

// Admin
export const adminService = {
  getDashboardStats: () =>
    api.get<DashboardStatsResponse>("/admin/dashboard/stats").then((r) => r.data),
  getChart: (params: ChartQueryParams) =>
    api.get<ChartResponse>("/admin/dashboard/chart", { params }).then((r) => r.data),

  // Products
  getProducts: (params: AdminProductsQueryParams) =>
    api.get<AdminProductsListResponse>("/admin/products", { params }).then((r) => r.data),
  createProduct: (payload: CreateProductPayload) =>
    api.post<AdminProductResponse>("/admin/products", payload).then((r) => r.data),
  updateProduct: (id: number, payload: Partial<CreateProductPayload>) =>
    api.put<AdminProductResponse>(`/admin/products/${id}`, payload).then((r) => r.data),
  deleteProduct: (id: number) =>
    api.delete<MessageResponse>(`/admin/products/${id}`).then((r) => r.data),
  setMargin: (id: number, payload: SetMarginPayload) =>
    api.put<MessageResponse>(`/admin/products/${id}/margin`, payload).then((r) => r.data),

  // Banners
  getBanners: () =>
    api.get<BannersResponse>("/admin/banners").then((r) => r.data),
  createBanner: (formData: FormData) =>
    api.post<CreateBannerResponse>("/admin/banners/upload", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }).then((r) => r.data),
  toggleBanner: (id: number) =>
    api.put<MessageResponse>(`/admin/banners/${id}/toggle`).then((r) => r.data),
  deleteBanner: (id: number) =>
    api.delete<MessageResponse>(`/admin/banners/${id}`).then((r) => r.data),

  // Metadata
  getMetadata: () =>
    api.get<MetadataListResponse>("/admin/system/metadata").then((r) => r.data),
  updateMetadata: (id: number, payload: UpdateMetadataPayload) =>
    api.put<GameMetadataItem>(`/admin/system/metadata/${id}`, payload).then((r) => r.data),

  // System
  getMidtransExpiry: () =>
    api.get<MidtransExpiryResponse>("/admin/system/midtrans-expiry").then((r) => r.data),
  updateMidtransExpiry: (payload: UpdateMidtransExpiryPayload) =>
    api.put<MessageResponse>("/admin/system/midtrans-expiry", payload).then((r) => r.data),
  syncProducts: () =>
    api.post<SyncResponse>("/admin/system/sync-digiflazz").then((r) => r.data),

  uploadImage: (formData: FormData) =>
    api.post<UploadImageResponse>("/admin/upload", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }).then((r) => r.data),
};

export default api;
