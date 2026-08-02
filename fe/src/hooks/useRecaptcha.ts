import { useCallback, useEffect } from "react";

declare global {
  interface Window {
    grecaptcha?: {
      ready: (callback: () => void) => void;
      execute: (
        siteKey: string,
        options: { action: string },
      ) => Promise<string>;
    };
  }
}

const RECAPTCHA_SCRIPT_ID = "google-recaptcha-v3";

/** Kegagalan dari sisi reCAPTCHA, bukan dari API — `message`-nya aman
 *  ditampilkan ke user, jadi pemanggil bisa membedakannya dari error login. */
export class RecaptchaError extends Error {}

let scriptPromise: Promise<void> | null = null;

function loadRecaptchaScript(siteKey: string): Promise<void> {
  if (window.grecaptcha) {
    return Promise.resolve();
  }

  if (scriptPromise) {
    return scriptPromise;
  }

  scriptPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");

    script.id = RECAPTCHA_SCRIPT_ID;
    script.src = `https://www.google.com/recaptcha/api.js?render=${siteKey}`;
    script.async = true;
    script.defer = true;

    script.onload = () => resolve();

    script.onerror = () => {
      // Elemen yang gagal wajib dibuang: kalau ditinggal di <head>, percobaan
      // berikutnya menempel listener ke <script> mati yang tidak akan pernah
      // memicu load/error lagi — promise-nya menggantung dan tombol muter terus.
      script.remove();
      scriptPromise = null;
      reject(new RecaptchaError("Gagal memuat verifikasi keamanan. Cek koneksi lalu coba lagi."));
    };

    document.head.appendChild(script);
  });

  return scriptPromise;
}

export function useRecaptcha() {
  const siteKey = import.meta.env.VITE_RECAPTCHA_SITE_KEY;

  /* Script-nya ditarik saat halaman dibuka, bukan saat tombol diklik. Kalau
     baru dimulai di klik, ratusan milidetik pertama murni unduhan — user sudah
     selesai mengisi form jauh sebelum itu, jadi waktunya lebih baik dipakai
     sekarang. Promise-nya di-memo di module scope, jadi klik memakai hasil
     yang sama dan tidak mengunduh dua kali. */
  useEffect(() => {
    if (!siteKey) return;
    void loadRecaptchaScript(siteKey).catch(() => {
      // Diamkan: kegagalan pramuat dilaporkan ulang saat tombol benar-benar
      // ditekan, di situ baru ada konteks untuk memberi tahu user.
    });
  }, [siteKey]);

  const executeRecaptcha = useCallback(
    async (action: string): Promise<string> => {
      if (!siteKey) {
        console.error("VITE_RECAPTCHA_SITE_KEY belum dikonfigurasi.");
        throw new RecaptchaError("Verifikasi keamanan belum dikonfigurasi. Hubungi admin.");
      }

      await loadRecaptchaScript(siteKey);

      if (!window.grecaptcha) {
        throw new RecaptchaError("Verifikasi keamanan tidak tersedia. Muat ulang halaman.");
      }

      return new Promise<string>((resolve, reject) => {
        window.grecaptcha!.ready(async () => {
          try {
            const token = await window.grecaptcha!.execute(
              siteKey,
              { action },
            );

            if (!token) {
              reject(new RecaptchaError("Gagal mendapatkan token reCAPTCHA."));

              return;
            }

            resolve(token);
          } catch {
            reject(new RecaptchaError("Verifikasi keamanan gagal. Silakan coba lagi."));
          }
        });
      });
    },
    [siteKey],
  );

  return {
    executeRecaptcha,
  };
}
