import { useEffect, useState } from "react";
import { User } from "lucide-react";

/**
 * Avatar user Digitalin — foto profil (avatarUrl dari GET /auth/profile)
 * dengan fallback ikon default. Provider-agnostic: komponen hanya tahu
 * satu field avatarUrl — tidak ada logika per Google/Facebook/local.
 *
 * Foto gagal dimuat (URL provider expired — mis. Graph Facebook bertoken
 * sementara) → otomatis jatuh kembali ke ikon, dan dicoba ulang bila URL
 * baru (mis. setelah login OAuth berikutnya) masuk.
 */
export default function UserAvatar({
  src,
  name,
  size = 32,
  iconSize,
}: {
  src?: string | null;
  name: string;
  /** Ukuran lingkaran (px). */
  size?: number;
  /** Ukuran ikon fallback (px) — default separuh lingkaran. */
  iconSize?: number;
}) {
  const [failed, setFailed] = useState(false);

  // URL berganti (avatar diperbarui backend) → reset status gagal lama.
  useEffect(() => {
    setFailed(false);
  }, [src]);

  return (
    <div
      className="flex items-center justify-center overflow-hidden flex-shrink-0"
      style={{
        width: size,
        height: size,
        borderRadius: "50%",
        backgroundColor: "var(--color-brand-tertiary)",
      }}
    >
      {src && !failed ? (
        <img
          src={src}
          alt={name}
          className="w-full h-full object-cover"
          referrerPolicy="no-referrer"
          onError={() => setFailed(true)}
        />
      ) : (
        <User size={iconSize ?? Math.round(size / 2)} color="var(--color-on-primary)" />
      )}
    </div>
  );
}
