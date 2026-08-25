import { create } from "zustand";
import { persist } from "zustand/middleware";
import type { Theme } from "../api/index";

/**
 * Fallback tema (nilai storage kosong/rusak). Terang dipilih karena dipakai
 * sebagai default area admin; area pelanggan selalu dipaksa gelap (lihat
 * onRehydrateStorage) karena artwork game terbaca lebih kuat di permukaan gelap.
 * Terang/gelap tetap bisa diatur lewat toggle di navbar admin & pelanggan.
 */
export const DEFAULT_THEME: Theme = "light";

/** Key localStorage. Dibaca juga oleh script inline di index.html. */
export const THEME_STORAGE_KEY = "digitalin-theme";

/**
 * Tema tersimpan → tema yang dipakai. Nilai asing (versi lama, storage
 * diedit tangan, atau JSON rusak) jatuh ke DEFAULT_THEME, bukan crash.
 */
export function resolveTheme(stored: unknown): Theme {
  return stored === "light" || stored === "dark" ? stored : DEFAULT_THEME;
}

/** Satu-satunya tempat class tema ditempel — komponen tidak menyentuh DOM. */
export function applyTheme(theme: Theme): void {
  document.documentElement.classList.toggle("dark", theme === "dark");
}

interface ThemeState {
  theme: Theme;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

export const useThemeStore = create<ThemeState>()(
  persist(
    (set, get) => ({
      theme: DEFAULT_THEME,
      setTheme: (theme) => {
        applyTheme(theme);
        set({ theme });
      },
      toggleTheme: () => get().setTheme(get().theme === "dark" ? "light" : "dark"),
    }),
    {
      name: THEME_STORAGE_KEY,
      partialize: (s) => ({ theme: s.theme }),
      /* Script inline di index.html sudah menempel class sebelum paint
         pertama; ini hanya menyelaraskan state kalau storage dinormalisasi. */
      onRehydrateStorage: () => (state) => {
        if (state) {
          let theme = resolveTheme(state.theme);
          // Area pelanggan selalu default gelap — pilihan "light" tersimpan
          // hanya berlaku di /admin. Toggle navbar tetap bisa mengganti ke
          // terang untuk sesi itu.
          if (!window.location.pathname.startsWith("/admin") && theme === "light") {
            theme = "dark";
          }
          state.theme = theme;
          applyTheme(theme);
        }
      },
    },
  ),
);
