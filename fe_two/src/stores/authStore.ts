import { create } from "zustand";

interface AuthState {
  customerToken: string | null;
  adminToken: string | null;

  setCustomerToken: (token: string | null) => void;
  setAdminToken: (token: string | null) => void;
  clearAll: () => void;
}

/**
 * Global auth store — JWT tokens are stored here (not in component state).
 * Tokens are also persisted to sessionStorage for tab persistence.
 */
export const useAuthStore = create<AuthState>((set) => ({
  customerToken: sessionStorage.getItem("customer_token"),
  adminToken: sessionStorage.getItem("admin_token"),

  setCustomerToken: (token) => {
    if (token) {
      sessionStorage.setItem("customer_token", token);
    } else {
      sessionStorage.removeItem("customer_token");
    }
    set({ customerToken: token });
  },

  setAdminToken: (token) => {
    if (token) {
      sessionStorage.setItem("admin_token", token);
    } else {
      sessionStorage.removeItem("admin_token");
    }
    set({ adminToken: token });
  },

  clearAll: () => {
    sessionStorage.removeItem("customer_token");
    sessionStorage.removeItem("admin_token");
    set({ customerToken: null, adminToken: null });
  },
}));
