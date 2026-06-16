import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { ProfileResponse } from "../api/index";

/** Decode a JWT payload and return the parsed JSON (no verification, just base64 decode). */
function decodeJwt(token: string): Record<string, unknown> | null {
  try {
    const payload = token.split(".")[1];
    return JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
  } catch {
    return null;
  }
}

/** Check if a JWT token is expired based on its `exp` claim. Returns true if valid (not expired). */
function isTokenValid(token: string | null): boolean {
  if (!token) return false;
  const payload = decodeJwt(token);
  if (!payload || typeof payload.exp !== "number") return false;
  // exp is in seconds, Date.now() is in ms. Add 10s buffer to avoid edge cases.
  return payload.exp * 1000 > Date.now() + 10_000;
}

interface AuthState {
  token: string | null;
  adminToken: string | null;
  user: ProfileResponse | null;
  hasHydrated: boolean;
  isAuthenticated: () => boolean;
  isAdmin: () => boolean;
  isAdminTokenValid: () => boolean;
  setToken: (token: string) => void;
  setAdminToken: (token: string) => void;
  setUser: (user: ProfileResponse) => void;
  logout: () => void;
  adminLogout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      adminToken: null,
      user: null,
      hasHydrated: false,
      isAuthenticated: () => !!get().token,
      isAdmin: () => !!get().adminToken,
      isAdminTokenValid: () => isTokenValid(get().adminToken),
      setToken: (token) => {
        localStorage.setItem("token", token);
        set({ token });
      },
      setAdminToken: (token) => {
        localStorage.setItem("adminToken", token);
        set({ adminToken: token });
      },
      setUser: (user) => set({ user }),
      logout: () => {
        localStorage.removeItem("token");
        set({ token: null, user: null });
      },
      adminLogout: () => {
        localStorage.removeItem("adminToken");
        set({ adminToken: null });
      },
    }),
    {
      name: "digitalin-auth",
      partialize: (s) => ({ token: s.token, adminToken: s.adminToken }),
      onRehydrateStorage: () => (state) => {
        if (state) state.hasHydrated = true;
      },
    }
  )
);
