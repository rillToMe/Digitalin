import type { ReactNode, FormEvent } from "react";

// Digitalin - TypeScript Type Definitions
// Never use `any`. Always reference these types.

// Enums / Literals

/** Tema tampilan. Nilainya juga dipakai sebagai class di <html> ("dark"). */
export type Theme = "light" | "dark";

export type GameInputFieldType =
  | "text"
  | "number"
  | "select";

export interface GameInputField {
  key: string;
  label: string;
  placeholder: string;
  type: GameInputFieldType;
  required: boolean;
  order: number;
  options: string[];
}

export type TargetValues = Record<string, string>;

export type OrderStatus =
  | "PENDING"
  | "PAID"
  | "PROCESSING"
  | "COMPLETED"
  | "NEEDS_ACTION"
  | "FAILED"
  | "EXPIRED"
  | "REFUNDED";

export type ChartRange = "today" | "yesterday" | "7days" | "30days" | "custom";

export type DiscountType = "Percentage" | "FixedAmount";

export type VoucherStatus = "Active" | "Disabled" | "Expired" | "QuotaExceeded";

// Auth

export interface RequestOtpPayload {
  email: string;
  captchaToken: string;
}

export interface RegisterPayload {
  email: string;
  password: string;
  fullName: string;
  otpCode: string;
}

export interface RegisterResponse {
  token: string;
}

export interface LoginPayload {
  email: string;
  password: string;
  captchaToken: string;
}

export interface AdminLoginPayload {
  username: string;
  password: string;
  captchaToken: string;
}

export interface GoogleLoginPayload {
  idToken: string;
}

export interface AuthTokenResponse {
  token: string;
}

export interface UpdateProfilePayload {
  fullName: string;
  newPassword: string | null;
}

export interface ProfileResponse {
  email: string;
  fullName: string;
  /** ⚠️ TYPO IS INTENTIONAL - backend returns "createtAt" not "createdAt" */
  createtAt: string;
}

// Brands / Catalog

export interface BrandItem {
  brand: string;
  slug: string;
  imageUrl: string | null;
  /** Kategori produk Digiflazz — saat ini BE selalu mengirim "Games" */
  category: string;
  /** Slug kategori storefront (mobile/pc/dst), boleh lebih dari satu; kosong → "Lainnya" */
  categorySlugs: string[];
}

export type BrandsResponse = BrandItem[];

export interface PopularGamesAdminResponse {
  selected: BrandItem[];
  options: GameMetadataItem[];
}

export interface UpdatePopularGamesResponse {
  message: string;
  selected: BrandItem[];
}

/**
 * Query param GET /games/brands. Dipaginasi per kategori supaya section homepage
 * tidak mengunduh seluruh katalog + ikonnya sekaligus. Tanpa param = katalog penuh.
 */
export interface BrandsQueryParams {
  /** Slug kategori; "all"/kosong = semua, "lainnya" = belum dikategorikan */
  category?: string;
  /** Jumlah brand yang sudah termuat di section itu */
  offset?: number;
  /** Ukuran batch; BE membatasi maksimal 60 */
  limit?: number;
}

// Game Categories (Storefront)

export interface GameCategoryItem {
  slug: string;
  name: string;
  brandCount: number;
}

export type GameCategoriesResponse = GameCategoryItem[];

/** Satu baris di editor kategori admin. slug null = kategori baru (BE turunkan dari name). */
export interface GameCategoryEntryPayload {
  slug: string | null;
  name: string;
}

/** Urutan array = urutan tampil di storefront. */
export interface UpdateGameCategoriesPayload {
  categories: GameCategoryEntryPayload[];
}

export interface UpdateGameCategoriesResponse {
  message: string;
  categories: GameCategoryItem[];
}

/** State satu baris di editor kategori admin. localId lokal saja — dipakai
 *  sebagai React key supaya reorder tidak memindahkan fokus tombol. */
export interface AdminCategoryRow {
  localId: number;
  /** null = kategori baru, slug diturunkan BE dari name */
  slug: string | null;
  name: string;
  brandCount: number;
}

export interface ProductItem {
  id: number;
  name: string;
  price: number;
  gameCode: string;
  brand: string;
  category: string;
  imageUrl: string | null;
  customMargin: number | null;
}

export interface BrandDetailResponse {
  brand: string;
  gameCode: string;
  inputFields: GameInputField[];
  imageUrl: string | null;
  products: ProductItem[];
}

// Admin Products

export interface AdminProductResponse {
  id: number;
  name: string;
  price: number;
  gameCode: string;
  buyerSkuCode: string;
  brand: string;
  category: string;
  imageUrl: string | null;
  customMargin: number | null;
}

export interface AdminProductsListResponse {
  data: AdminProductResponse[];
  totalItems: number;
  totalPages: number;
  currentPage: number;
}

export interface CreateProductPayload {
  name: string;
  price: number;
  gameCode: string;
  buyerSkuCode: string;
  brand: string;
  category: string;
  imageUrl: string | null;
  customMargin: number | null;
}

export interface SetMarginPayload {
  margin: number | null;
}

// Orders

export interface CreateOrderPayload {
  productId: number;
  targetValues: TargetValues;
  voucherCode: string | null;
}

export interface OrderResponse {
  id: number;
  orderNo: string;
  playerId: string;
  status: OrderStatus;
  productName: string;
  price: number;
  snapToken: string | null;
  redirectUrl: string | null;
  orderDate: string;
  sn: string | null;
  imageUrl: string | null;
  discountAmount: number;
  finalPrice: number;
}

export interface CreateOrderResponse extends OrderResponse { }

// Checkout Preview

export interface CheckoutPreviewRequest {
  productId: number;
  voucherCode?: string | null;
}

export interface CheckoutPreviewResponse {
  productId: number;
  productName: string;
  imageUrl: string | null;
  gameCode: string;
  originalPrice: number;
  discountAmount: number;
  finalPrice: number;
  currency: string;
  isVoucherApplied: boolean;
  voucherCode: string | null;
  discountType: DiscountType | null;
  discountValue: number | null;
  maximumDiscount: number | null;
}

export interface PaginatedOrdersResponse {
  data: OrderResponse[];
  totalItems: number;
  totalPages: number;
  currentPage: number;
}

export interface RetryOrderResponse {
  message: string;
  status: string;
}

export interface RefundOrderResponse {
  message: string;
  status: "REFUNDED";
}

// Banners

export interface BannerItem {
  id: number;
  title: string;
  imageUrl: string;
  isActive: boolean;
  createdAt: string;
}

export type BannersResponse = BannerItem[];

export interface CreateBannerResponse {
  id: number;
  title: string;
  imageUrl: string;
  isActive: boolean;
  createdAt: string;
}

// Dashboard

export interface DashboardStatsResponse {
  totalRevenueToday: number;
  totalProfitToday: number;
  totalOrdersToday: number;
  successOrdersToday: number;
  pendingOrdersToday: number;
  failedOrdersToday: number;
}

// Saldo supplier — live dari Digiflazz, bukan dari database Digitalin.
export interface DigiflazzBalanceResponse {
  deposit: number;
}

export interface ChartDataPoint {
  name: string;
  pendapatan: number;
  profit: number;
}

export type ChartResponse = ChartDataPoint[];

export interface ChartQueryParams {
  range: ChartRange;
  startDate?: string;
  endDate?: string;
}

// System / Metadata

export interface GameMetadataItem {
  id: number;
  brand: string;
  /** Slug brand dari BE — identitas game di URL & pilihan populer. Jangan hitung ulang di FE. */
  brandSlug: string;
  imageUrl: string | null;
  inputFields: GameInputField[];
  targetTemplate: string;
  /** Instruksi supplier Digiflazz (read-only). Informatif — bukan pengganti targetTemplate/inputFields. */
  supplierNote: string | null;
  /** Slug kategori storefront (kebab-case), boleh lebih dari satu; kosong = belum dikategorikan */
  categories: string[];
}

export type MetadataListResponse = GameMetadataItem[];

export interface UpdateMetadataPayload {
  inputFields: GameInputField[];
  targetTemplate: string;
  /** Kategori storefront (boleh beberapa); dinormalisasi BE ke kebab-case, kosong → tanpa kategori */
  categories?: string[];
}

// Built-in input schema presets (SSOT: backend GameTemplateFactory)
export interface GameTemplate {
  type: string;
  displayName: string;
  targetTemplate: string;
  inputFields: GameInputField[];
}

export type GameTemplateListResponse = GameTemplate[];

export interface MidtransExpiryResponse {
  expiry: number;
}

export interface UpdateMidtransExpiryPayload {
  expiry: number;
}

export interface SyncResponse {
  message: string;
}

export interface CleanDummyResponse {
  message: string;
}

// Search

export interface GameSearchResult {
  id: number;
  brand: string;
  slug: string;
  imageUrl: string | null;
  categorySlugs: string[];
}

export type SearchGamesResponse = GameSearchResult[];

export interface SearchGamesParams {
  q?: string;
  limit?: number;
}

// Generic API Responses

export interface MessageResponse {
  message: string;
}

export interface UploadImageResponse {
  message: string;
  url: string;
}

export interface ApiError {
  message: string;
}

// Pagination Query Params

export interface PaginationParams {
  page?: number;
  limit?: number;
}

export interface AdminProductsQueryParams extends PaginationParams {
  search?: string;
  brand?: string;
}

// Vouchers

export interface AdminVoucherItem {
  id: number;
  code: string;
  discountType: DiscountType;
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  usedCount: number;
  expiredAt: string;
  isActive: boolean;
  status: VoucherStatus;
  createdAt: string;
  updatedAt: string;
}

export type AdminVoucherDetailResponse = AdminVoucherItem;

export interface AdminVoucherListResponse {
  items: AdminVoucherItem[];
  totalCount: number;
  currentPage: number;
  totalPages: number;
}

export interface CreateVoucherPayload {
  code: string;
  discountType: DiscountType;
  discountValue: number;
  minimumTransaction: number;
  maximumDiscount: number | null;
  usageLimit: number;
  expiredAt: string;
  isActive: boolean;
}

export type UpdateVoucherPayload = CreateVoucherPayload;

export interface UpdateVoucherStatusPayload {
  isActive: boolean;
}

export interface UpdateVoucherStatusResponse {
  message: string;
  isActive: boolean;
}

export interface DeleteVoucherResponse {
  message: string;
}

export interface VoucherUsageInfo {
  orderId: number;
  orderNo: string;
  customerName: string | null;
  productName: string;
  price: number;
  discountAmount: number;
  finalPrice: number;
  orderDate: string;
  orderStatus: string;
}

export interface VoucherUsageResponse {
  items: VoucherUsageInfo[];
  totalCount: number;
  currentPage: number;
  totalPages: number;
}

export interface AdminVouchersQueryParams extends PaginationParams {
  search?: string;
  status?: VoucherStatus;
  sortBy?: "code" | "createdAt" | "expiredAt" | "discountValue";
  sortOrder?: "asc" | "desc";
}

// Password Reset
export interface RequestPasswordResetPayload {
  email: string;
}

export interface ResetPasswordPayload {
  email: string;
  otpCode: string;
  newPassword: string;
}

// Maintenance Mode
export interface MaintenanceStatusResponse {
  enabled: boolean;
  title: string;
  message: string;
  estimatedFinish: string | null;
  allowAdmin: boolean;
}

export interface UpdateMaintenancePayload {
  enabled: boolean;
  title: string;
  message: string;
  estimatedFinish: string | null;
  allowAdmin: boolean;
}

export interface MaintenancePayload {
  enabled: boolean;
  title: string;
  message: string;
  estimatedFinish: string | null;
  allowAdmin: boolean;
}

export interface PaymentExpiryPayload {
  expiry: number;
}

// --- UI Component & Page Types ---

export interface SettingToggleProps {
  checked: boolean;
  onChange: () => void;
  disabled?: boolean;
  isLoading?: boolean;
  activeColor?: string;
  inactiveColor?: string;
  size?: "sm" | "md";
  title?: string;
}

export type ModalTab = "info" | "products";

export interface GameEditModalProps {
  item: GameMetadataItem;

  form: UpdateMetadataPayload;
  setForm: (form: UpdateMetadataPayload) => void;

  activeTab: ModalTab;
  setActiveTab: (tab: ModalTab) => void;

  coverFile: File | null;
  setCoverFile: (file: File | null) => void;

  coverPreview: string | null;
  setCoverPreview: (url: string | null) => void;

  products: AdminProductResponse[];
  productsLoading: boolean;
  productsError: Error | null;
  totalProducts: number;

  updateMutation: {
    mutate: (
      vars: {
        id: number;
        payload: UpdateMetadataPayload;
      },
      options?: { onSuccess?: () => void }
    ) => void;
    isPending: boolean;
  };

  coverMutation: {
    mutate: (vars: {
      id: number;
      formData: FormData;
    }) => void;
    isPending: boolean;
  };

  marginMutation: {
    mutate: (vars: {
      id: number;
      margin: number | null;
    }) => void;
    isPending: boolean;
  };

  onClose: () => void;
}

export interface ProductMarginListProps {
  products: AdminProductResponse[];
  brand: string;
  marginMutation: {
    mutate: (vars: { id: number; margin: number | null }) => void;
    isPending: boolean;
  };
  loading: boolean;
  error: Error | null;
  totalCount: number;
}

export interface ForgotPasswordEmailStepProps {
  email: string;
  setEmail: (val: string) => void;
  onSubmit: (e: FormEvent) => void;
  isPending: boolean;
}

export interface ForgotPasswordOtpStepProps {
  email: string;
  otpCode: string;
  setOtpCode: (val: string) => void;
  onSubmit: (e: FormEvent) => void;
  isPending: boolean;
  onResendOtp: () => Promise<void>;
  isResending: boolean;
}

export interface ForgotPasswordSuccessStepProps {
  onBackToLogin: () => void;
}

export interface OtpInputProps {
  value: string;
  onChange: (val: string) => void;
  length?: number;
}

export interface ContactItem {
  icon: ReactNode;
  label: string;
  value: string;
  link?: string;
  description: string;
}

// Admin Management (SuperAdminOnly)

export type AdminRole = "ADMIN" | "SUPER_ADMIN";

export interface AdminListItem {
  id: number;
  username: string;
  role: AdminRole;
  createdAt: string;
  lastLoginAt: string | null;
}

export type AdminListResponse = AdminListItem[];

export interface CreateAdminPayload {
  username: string;
  password: string;
  role: AdminRole;
}

export interface UpdateAdminRolePayload {
  role: AdminRole;
}

export interface ResetAdminPasswordPayload {
  newPassword: string;
}
