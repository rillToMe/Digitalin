import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { ProfileResponse } from "../api/index";

interface AuthState {
  token: string | null;
  adminToken: string | null;
  user: ProfileResponse | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  setToken: (token: string) => void;
  setAdminToken: (token: string) => void;
  setUser: (user: ProfileResponse) => void;
  logout: () => void;
  adminLogout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      adminToken: null,
      user: null,
      isAuthenticated: false,
      isAdmin: false,
      setToken: (token) => {
        localStorage.setItem("token", token);
        set({ token, isAuthenticated: true });
      },
      setAdminToken: (token) => {
        localStorage.setItem("adminToken", token);
        set({ adminToken: token, isAdmin: true });
      },
      setUser: (user) => set({ user }),
      logout: () => {
        localStorage.removeItem("token");
        set({ token: null, user: null, isAuthenticated: false });
      },
      adminLogout: () => {
        localStorage.removeItem("adminToken");
        set({ adminToken: null, isAdmin: false });
      },
    }),
    { name: "digitalin-auth", partialize: (s) => ({ token: s.token, adminToken: s.adminToken }) }
  )
);
