/* ─────────────────────────────────────────────────────────────────────────────
 * Digitalin — TypeScript Type Definitions
 * Source of truth: API_CONTRACT.md
 * ⚠️  Never use `any`. Always reference these types.
 * ───────────────────────────────────────────────────────────────────────────── */

// ─── Enums / Literals ───────────────────────────────────────────────────────

export type InputType = "USER_ID_ONLY" | "USER_ID_AND_ZONE_ID";

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

// ─── Auth ────────────────────────────────────────────────────────────────────

export interface RequestOtpPayload {
  email: string;
}

export interface RegisterPayload {
  email: string;
  password: string;
  fullName: string;
  otpCode: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface AdminLoginPayload {
  username: string;
  password: string;
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
  /** ⚠️ TYPO IS INTENTIONAL — backend returns "createtAt" not "createdAt" */
  createtAt: string;
}

// ─── Brands / Catalog ────────────────────────────────────────────────────────

export interface BrandItem {
  id: number;
  brand: string;
  slug: string;
  imageUrl: string | null;
  inputType: InputType;
}

export type BrandsResponse = BrandItem[];

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
  inputType: InputType;
  imageUrl: string | null;
  products: ProductItem[];
}

// ─── Admin Products ──────────────────────────────────────────────────────────

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
  items: AdminProductResponse[];
  totalCount: number;
  page: number;
  limit: number;
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

// ─── Orders ──────────────────────────────────────────────────────────────────

export interface CreateOrderPayload {
  productId: number;
  targetId: string;
  zoneId: string | null;
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

export interface CreateOrderResponse extends OrderResponse {}

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

// ─── Banners ─────────────────────────────────────────────────────────────────

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

// ─── Dashboard ───────────────────────────────────────────────────────────────

export interface DashboardStatsResponse {
  totalRevenueToday: number;
  totalProfitToday: number;
  totalOrdersToday: number;
  successOrdersToday: number;
  pendingOrdersToday: number;
  failedOrdersToday: number;
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

// ─── System / Metadata ───────────────────────────────────────────────────────

export interface GameMetadataItem {
  id: number;
  brand: string;
  inputType: InputType;
  serverList: string | null;
  imageUrl: string | null;
}

export type MetadataListResponse = GameMetadataItem[];

export interface UpdateMetadataPayload {
  inputType: InputType;
  serverList: string | null;
}

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

// ─── Generic API Responses ───────────────────────────────────────────────────

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

// ─── Pagination Query Params ─────────────────────────────────────────────────

export interface PaginationParams {
  page?: number;
  limit?: number;
}

export interface AdminProductsQueryParams extends PaginationParams {
  search?: string;
}
