import { useCallback } from "react";

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

let scriptPromise: Promise<void> | null = null;

function loadRecaptchaScript(siteKey: string): Promise<void> {
  if (window.grecaptcha) {
    return Promise.resolve();
  }

  if (scriptPromise) {
    return scriptPromise;
  }

  scriptPromise = new Promise<void>((resolve, reject) => {
    const existingScript = document.getElementById(
      RECAPTCHA_SCRIPT_ID,
    ) as HTMLScriptElement | null;

    if (existingScript) {
      existingScript.addEventListener("load", () => resolve());
      existingScript.addEventListener("error", () => {
        scriptPromise = null;
        reject(new Error("Gagal memuat reCAPTCHA."));
      });

      return;
    }

    const script = document.createElement("script");

    script.id = RECAPTCHA_SCRIPT_ID;
    script.src = `https://www.google.com/recaptcha/api.js?render=${siteKey}`;
    script.async = true;
    script.defer = true;

    script.onload = () => resolve();

    script.onerror = () => {
      scriptPromise = null;
      reject(new Error("Gagal memuat reCAPTCHA."));
    };

    document.head.appendChild(script);
  });

  return scriptPromise;
}

export function useRecaptcha() {
  const siteKey = import.meta.env.VITE_RECAPTCHA_SITE_KEY;

  const executeRecaptcha = useCallback(
    async (action: string): Promise<string> => {
      if (!siteKey) {
        throw new Error(
          "VITE_RECAPTCHA_SITE_KEY belum dikonfigurasi.",
        );
      }

      await loadRecaptchaScript(siteKey);

      if (!window.grecaptcha) {
        throw new Error("reCAPTCHA tidak tersedia.");
      }

      return new Promise<string>((resolve, reject) => {
        window.grecaptcha!.ready(async () => {
          try {
            const token = await window.grecaptcha!.execute(
              siteKey,
              { action },
            );

            if (!token) {
              reject(
                new Error("Gagal mendapatkan token reCAPTCHA."),
              );

              return;
            }

            resolve(token);
          } catch {
            reject(
              new Error("Verifikasi reCAPTCHA gagal."),
            );
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