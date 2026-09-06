import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import { GoogleLogin } from "@react-oauth/google";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { useAuthStore } from "../../store/authStore";
import { authService } from "../../services/api";

export const googleClientId =
  import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;

export default function GoogleLoginButton({
  disabled,
  onLoadingChange,
}: {
  disabled: boolean;
  onLoadingChange: (loading: boolean) => void;
}) {
  const navigate = useNavigate();
  const { setToken } = useAuthStore();

  const googleMutation = useMutation({
    mutationFn: (idToken: string) =>
      authService.loginGoogle({ idToken }),

    onMutate: () => onLoadingChange(true),

    onSuccess: (res) => {
      setToken(res.token);
      toast.success("Login berhasil");
      navigate("/");
    },

    onError: (err: any) => {
      toast.error(
        err?.response?.data?.message ??
        "Login Google gagal"
      );
    },

    onSettled: () => onLoadingChange(false),
  });

  /* Tombol Google dirender GIS di dalam iframe — lebarnya hanya bisa diatur
     lewat prop `width` (min 200 / max 400 px). Diukur dari lebar container
     supaya sama penuh dengan tombol Facebook di bawahnya (lebar tetap 320
     membuat tombol meluber di layar sempit). Deps effect GoogleLogin memuat
     `width`, jadi tombol dirender ulang saat lebar berubah. */
  const wrapRef = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState<number>();

  useEffect(() => {
    const el = wrapRef.current;
    if (!el) return;
    const ro = new ResizeObserver((entries) => {
      const w = Math.floor(entries[0].contentRect.width);
      if (w > 0) setWidth(Math.min(400, Math.max(200, w)));
    });
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  return (
    <>
      {/* Wrap with pointer-events-none when disabled to prevent double-submit.
          Divider "atau" dirender AuthPage (SocialDivider) — dipakai bersama
          tombol sosial lain (Facebook). */}
      <div
        ref={wrapRef}
        className="flex justify-center"
        style={{
          opacity: disabled ? 0.5 : 1,
          pointerEvents: disabled ? "none" : "auto",
          transition: "opacity 0.2s",
        }}
      >
        {width !== undefined && (
          <GoogleLogin
            theme="outline"
            text="continue_with"
            shape="pill"
            size="large"
            width={width}
            onSuccess={(credentialResponse) => {
              if (!credentialResponse.credential) {
                toast.error("Google tidak mengirim ID Token");
                return;
              }

              googleMutation.mutate(
                credentialResponse.credential
              );
            }}
            onError={() => {
              toast.error("Login Google gagal");
            }}
          />
        )}
      </div>
    </>
  );
}
