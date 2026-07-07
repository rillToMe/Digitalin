import axios from "axios";
import { toast } from "sonner";
import { useAuthStore } from "../store/authStore";
import { useMaintenanceStore } from "../store/maintenanceStore";
import type {
  AuthTokenResponse,
  LoginPayload,
  GoogleLoginPayload,
  RegisterPayload,
  RegisterResponse,
  RequestOtpPayload,
  AdminLoginPayload,
  UpdateProfilePayload,
  ProfileResponse,
  BrandsResponse,
  BrandDetailResponse,
  CreateOrderPayload,
  CreateOrderResponse,
  CheckoutPreviewRequest,
  CheckoutPreviewResponse,
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
  AdminVoucherListResponse,
  AdminVoucherDetailResponse,
  CreateVoucherPayload,
  UpdateVoucherPayload,
  UpdateVoucherStatusPayload,
  UpdateVoucherStatusResponse,
  DeleteVoucherResponse,
  VoucherUsageResponse,
  AdminVouchersQueryParams,
  PaginationParams,
  RetryOrderResponse,
  RefundOrderResponse,
  RequestPasswordResetPayload,
  ResetPasswordPayload,
  MaintenanceStatusResponse,
  UpdateMaintenancePayload,
  AdminListResponse,
  CreateAdminPayload,
  UpdateAdminRolePayload,
  ResetAdminPasswordPayload,
} from "../api/index";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5210",
  headers: { "Content-Type": "application/json" },
});

// Request interceptor - attach appropriate token based on route
api.interceptors.request.use((config) => {
  const store = useAuthStore.getState();
  const isAdminRequest = config.url?.startsWith("/admin") ?? false;

  const token = isAdminRequest
    ? store.adminToken
    : store.token;

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

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
    } else if (error.response?.status === 503) {
      const data = error.response.data;
      if (data && data.maintenance === true) {
        const requestUrl: string = error.config?.url ?? "";
        const isAdminRequest = requestUrl.startsWith("/admin") || window.location.pathname.startsWith("/admin");
        
        if (!isAdminRequest) {
          const { setMaintenance } = useMaintenanceStore.getState();
          setMaintenance(true, data.title, data.message, data.estimatedFinish);
          
          if (!window.location.pathname.includes("/maintenance")) {
            window.location.href = "/maintenance";
          }
        }
      }
    }
    return Promise.reject(error);
  }
);

// Auth
export const authService = {
  requestOtp: (payload: RequestOtpPayload) =>
    api.post<MessageResponse>("/auth/local/request-otp", payload).then((r) => r.data),
  register: (payload: RegisterPayload) =>
    // Returns { message } only — no token. Caller must redirect to /login after success.
    api.post<RegisterResponse>("/auth/local/register", payload).then((r) => r.data),
  login: (payload: LoginPayload) =>
    api.post<AuthTokenResponse>("/auth/local/login", payload).then((r) => r.data),
  loginGoogle: (payload: GoogleLoginPayload) =>
    api.post<AuthTokenResponse>("/auth/google", payload).then((r) => r.data),
  getProfile: () =>
    api.get<ProfileResponse>("/auth/profile").then((r) => r.data),
  updateProfile: (payload: UpdateProfilePayload) =>
    api.put<MessageResponse>("/auth/profile", payload).then((r) => r.data),
  requestPasswordReset: (payload: RequestPasswordResetPayload) =>
    api.post<MessageResponse>("/auth/local/request-password-reset", payload).then((r) => r.data),
  resetPassword: (payload: ResetPasswordPayload) =>
    api.post<MessageResponse>("/auth/local/reset-password", payload).then((r) => r.data),
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
  preview: (payload: CheckoutPreviewRequest) =>
    api.post<CheckoutPreviewResponse>("/orders/preview", payload).then((r) => r.data),
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
  uploadGameCover: (id: number, formData: FormData) =>
    api.post<UploadImageResponse>(`/admin/system/metadata/${id}/upload-game-cover`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    }).then((r) => r.data),

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

  // Vouchers
  getVouchers: (params: AdminVouchersQueryParams) =>
    api.get<AdminVoucherListResponse>("/admin/vouchers", { params }).then((r) => r.data),
  getVoucherById: (id: number) =>
    api.get<AdminVoucherDetailResponse>(`/admin/vouchers/${id}`).then((r) => r.data),
  createVoucher: (payload: CreateVoucherPayload) =>
    api.post<AdminVoucherDetailResponse>("/admin/vouchers", payload).then((r) => r.data),
  updateVoucher: (id: number, payload: UpdateVoucherPayload) =>
    api.put<AdminVoucherDetailResponse>(`/admin/vouchers/${id}`, payload).then((r) => r.data),
  updateVoucherStatus: (id: number, payload: UpdateVoucherStatusPayload) =>
    api.patch<UpdateVoucherStatusResponse>(`/admin/vouchers/${id}/status`, payload).then((r) => r.data),
  deleteVoucher: (id: number) =>
    api.delete<DeleteVoucherResponse>(`/admin/vouchers/${id}`).then((r) => r.data),
  getVoucherUsage: (id: number, params?: { page?: number; limit?: number }) =>
    api.get<VoucherUsageResponse>(`/admin/vouchers/${id}/usage`, { params }).then((r) => r.data),

  // Orders
  getOrders: (params: PaginationParams) =>
    api.get<PaginatedOrdersResponse>("/admin/orders", { params }).then((r) => r.data),
  retryOrder: (orderNo: string) =>
    api.post<RetryOrderResponse>(`/admin/orders/${orderNo}/retry`).then((r) => r.data),
  refundOrder: (orderNo: string) =>
    api.post<RefundOrderResponse>(`/admin/orders/${orderNo}/refund`).then((r) => r.data),

  // Maintenance Mode
  getMaintenance: () =>
    api.get<MaintenanceStatusResponse>("/admin/settings/maintenance").then((r) => r.data),
  updateMaintenance: (payload: UpdateMaintenancePayload) =>
    api.put<MaintenanceStatusResponse>("/admin/settings/maintenance", payload).then((r) => r.data),
};

// Admin Management (SuperAdminOnly)
export const adminManagementService = {
  getAll: () =>
    api.get<AdminListResponse>("/admin/admins").then((r) => r.data),
  create: (payload: CreateAdminPayload) =>
    api.post<MessageResponse>("/admin/admins", payload).then((r) => r.data),
  updateRole: (id: number, payload: UpdateAdminRolePayload) =>
    api.put<MessageResponse>(`/admin/admins/${id}/role`, payload).then((r) => r.data),
  resetPassword: (id: number, payload: ResetAdminPasswordPayload) =>
    api.put<MessageResponse>(`/admin/admins/${id}/password`, payload).then((r) => r.data),
  deleteAdmin: (id: number) =>
    api.delete<MessageResponse>(`/admin/admins/${id}`).then((r) => r.data),
};

export default api;
