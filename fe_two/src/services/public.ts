import { api } from "./api";
import type {
  BrandsResponse,
  BrandDetailResponse,
  CreateOrderPayload,
  CreateOrderResponse,
} from "@/types";

/**
 * GET /games/brands
 * Fetches all game brands for the storefront catalog.
 * Public endpoint — no authentication required.
 */
export async function getGameBrands(): Promise<BrandsResponse> {
  const { data } = await api.get<BrandsResponse>("/games/brands");
  return data;
}

/**
 * GET /games/{brandSlug}
 * Fetches a specific brand's details and its available products.
 * Public endpoint — no authentication required.
 */
export async function getGameDetail(brandSlug: string): Promise<BrandDetailResponse> {
  const { data } = await api.get<BrandDetailResponse>(`/games/${brandSlug}`);
  return data;
}

/**
 * POST /orders/
 * Creates a new top-up order with optional voucher.
 * Auth is optional (JWT accepted but not required).
 *
 * ⚠️ CRITICAL: playerId = targetId + zoneId (NO separator) — handled by backend.
 * ⚠️ CRITICAL: zoneId is REQUIRED when inputType == "USER_ID_AND_ZONE_ID".
 */
export async function createOrder(payload: CreateOrderPayload): Promise<CreateOrderResponse> {
  const { data } = await api.post<CreateOrderResponse>("/orders/", payload);
  return data;
}
