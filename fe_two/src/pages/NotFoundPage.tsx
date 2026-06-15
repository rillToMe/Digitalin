import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center bg-neutral text-center">
      <h1 className="text-display text-primary">404</h1>
      <p className="mt-4 text-lg text-secondary">Halaman yang Anda cari tidak ditemukan.</p>
      <Link
        to="/"
        className="mt-6 rounded-md bg-tertiary px-6 py-3 text-sm font-medium text-on-primary transition-colors hover:bg-tertiary/90"
      >
        Kembali ke Beranda
      </Link>
    </div>
  );
}
