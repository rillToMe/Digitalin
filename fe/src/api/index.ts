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
  /** URL foto profil (dari OAuth Google/Facebook); null untuk user tanpa avatar. */
  avatarUrl: string | null;
}

// FAQ (CMS — dikelola admin, kategori dari API, tidak di-hardcode FE)

export interface FaqItem {
  id: number;
  question: string;
  answer: string;
  category: string;
  featured: boolean;
}

// Leaderboard pembeli (derived dari order lunak — FE hanya render hasil BE)

export interface LeaderboardEntry {
  rank: number;
  displayName: string;
  /** Null = tampilkan avatar default (sama seperti profil). */
  avatarUrl: string | null;
  totalSpent: number;
}

export type LeaderboardPeriod = "today" | "week" | "month";

// Cek Transaksi (public invoice lookup — tanpa login)

export type InvoicePaymentStatus = "UNPAID" | "PAID" | "FAILED" | "REFUNDED";

export interface PublicInvoiceResponse {
  invoiceNumber: string;
  productName: string;
  imageUrl: string | null;
  /**
   * Slug katalog brand snapshot (identifier publik URL /game/{slug}) — untuk
   * cookie "Terakhir Dibeli" guest. Null untuk order pra-kolom.
   */
  brandSlug: string | null;
  amount: number;
  status: OrderStatus;
  paymentStatus: InvoicePaymentStatus;
  createdAt: string;
  /** Hanya ada saat status PENDING (batas bayar Midtrans); null selainnya. */
  expiresAt: string | null;
  /** True bila invoice ini milik akun yang sedang login (optional-auth). */
  belongsToAccount: boolean;
  /** payment_type Core API ("qris"|"gopay"|"bank_transfer") — hanya saat PENDING; null selainnya. */
  paymentType: string | null;
  /** Instruksi bayar Core API — hanya saat PENDING; null selainnya. */
  paymentInstructions: OrderPaymentInstructions | null;
}

/** Respons POST /invoices/resolve — Cek Transaksi: OrderNo → PublicHash saja. */
export interface InvoiceResolveResponse {
  publicHash: string;
}

export interface CreateFaqPayload {
  question: string;
  answer: string;
  category: string;
  featured: boolean;
}

export type UpdateFaqPayload = CreateFaqPayload;

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

/**
 * Query param GET /games/brands/recent. Login → tanpa param (riwayat DB);
 * guest → slug cookie "Terakhir Dibeli" (comma-separated, divalidasi BE).
 */
export interface RecentBrandsQueryParams {
  slugs?: string;
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
  canCheckUsername: boolean;
  products: ProductItem[];
}

export interface UsernameCheckResponse {
  isValid: boolean;
  username: string | null;
  message: string;
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

/**
 * Metode pembayaran Midtrans Core API yang bisa di-charge (whitelist provider).
 * DANA/LinkAja/OVO bukan kanal Midtrans — pembayaran dari aplikasi tersebut
 * lewat QRIS.
 */
export type PaymentMethodId = "qris" | "gopay" | "shopeepay" | "bank_transfer" | "cstore";

/** Sub-kanal di bawah metode (bank VA untuk bank_transfer, toko untuk cstore). */
export type PaymentChannelId = string;

// Payment methods (checkout) — SSOT tampilan dari backend (ikon, urutan, aktif)

export interface PaymentBankInfo {
  code: string;
  name: string;
  iconUrl: string | null;
}

export interface PaymentMethodInfo {
  code: PaymentMethodId;
  name: string;
  description: string | null;
  iconUrl: string | null;
  banks: PaymentBankInfo[];
}

// Payment methods (admin) — semua state termasuk nonaktif

export interface AdminPaymentBankInfo {
  code: string;
  name: string;
  iconUrl: string | null;
  enabled: boolean;
  sortOrder: number;
}

export interface AdminPaymentMethodInfo {
  code: string;
  name: string;
  description: string | null;
  iconUrl: string | null;
  enabled: boolean;
  sortOrder: number;
  banks: AdminPaymentBankInfo[];
}

export interface UpdatePaymentMethodPayload {
  displayName?: string | null;
  iconUrl?: string | null;
  enabled?: boolean | null;
  sortOrder?: number | null;
}

export interface UpdatePaymentBankPayload {
  displayName?: string | null;
  iconUrl?: string | null;
  enabled?: boolean | null;
  sortOrder?: number | null;
}

export interface OrderPaymentAction {
  name: string; // mis. "generate-qr-code", "deeplink-redirect"
  url: string;
}

export interface OrderPaymentInstructions {
  qrString: string | null;
  actions: OrderPaymentAction[];
  vaBank: string | null;
  vaNumber: string | null;
  /** Mandiri Bill (echannel): kode perusahaan (mis. 70012). */
  billerCode: string | null;
  /** Mandiri Bill (echannel): kode pembayaran. */
  billKey: string | null;
  /** cstore: toko tujuan ("indomaret" | "alfamart"). */
  store: string | null;
  /** cstore: kode bayar di kasir. */
  paymentCode: string | null;
  /** cstore: merchant id Midtrans — diminta simulator sandbox Indomaret & sebagian alur kasir. */
  merchantId: string | null;
}

export interface CreateOrderPayload {
  productId: number;
  targetValues: TargetValues;
  voucherCode: string | null;
  /** Metode pembayaran Midtrans Core API — wajib. */
  paymentMethod: PaymentMethodId;
  /** Wajib bila paymentMethod = "bank_transfer" (bca/bni/bri/cimb/permata/mandiri). */
  paymentBank?: string | null;
  /** Wajib bila paymentMethod = "cstore" (indomaret/alfamart). */
  paymentStore?: string | null;
}

export interface OrderResponse {
  id: number;
  orderNo: string;
  playerId: string;
  status: OrderStatus;
  productName: string;
  price: number;
  orderDate: string;
  sn: string | null;
  imageUrl: string | null;
  discountAmount: number;
  finalPrice: number;
  /** payment_type Midtrans Core API. */
  paymentType: string | null;
  /** Instruksi bayar Core API (QR/VA/deeplink). */
  paymentInstructions: OrderPaymentInstructions | null;
  /** HMAC-SHA256(OrderNo) hex 64 — URL status publik (/order/status/{publicHash}); null pra-backfill. */
  publicHash: string | null;
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
  usernameCheckProductId: number | null;
}

export type MetadataListResponse = GameMetadataItem[];

export interface UpdateMetadataPayload {
  inputFields: GameInputField[];
  targetTemplate: string;
  /** Kategori storefront (boleh beberapa); dinormalisasi BE ke kebab-case, kosong → tanpa kategori */
  categories?: string[];
  usernameCheckProductId: number | null;
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

export interface AdminOrdersQueryParams extends PaginationParams {
  status?: OrderStatus;
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
