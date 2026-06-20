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

  return (
    <>
      <div className="flex items-center gap-3 my-5">
        <div className="flex-1 h-px bg-gray-200" />
        <span className="text-sm text-gray-500">
          atau
        </span>
        <div className="flex-1 h-px bg-gray-200" />
      </div>

      {/* Wrap with pointer-events-none when disabled to prevent double-submit */}
      <div
        className="flex justify-center"
        style={{
          opacity: disabled ? 0.5 : 1,
          pointerEvents: disabled ? "none" : "auto",
          transition: "opacity 0.2s",
        }}
      >
        <GoogleLogin
          theme="outline"
          text="continue_with"
          shape="pill"
          size="large"
          width="320"
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
      </div>
    </>
  );
}