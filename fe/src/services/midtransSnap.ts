/**
 * Loader snap.js on-demand.
 *
 * Sebelumnya <script> Snap dipasang di index.html, jadi ~726 kB ikut terunduh
 * di setiap halaman — termasuk yang tidak punya transaksi sama sekali (home,
 * FAQ, login). Sekarang hanya halaman transaksi yang memanggil preloadSnap()
 * atau paySnap(), dan hasil unduhannya dibagi lewat memo di module scope.
 *
 * Rilis production: ganti URL ke https://app.midtrans.com/snap/snap.js dan
 * VITE_MIDTRANS_CLIENT_KEY ke client key production.
 */
const SNAP_SCRIPT_URL = "https://app.sandbox.midtrans.com/snap/snap.js";
const SNAP_CLIENT_KEY = import.meta.env.VITE_MIDTRANS_CLIENT_KEY?.trim();
const SNAP_SCRIPT_ID = "midtrans-snap";

/** Kegagalan dari sisi Snap, bukan dari API — `message`-nya aman ditampilkan ke user. */
export class SnapError extends Error {}

let scriptPromise: Promise<void> | null = null;

function loadSnapScript(): Promise<void> {
  if (window.snap) {
    return Promise.resolve();
  }

  if (!SNAP_CLIENT_KEY) {
    return Promise.reject(
      new SnapError("Client Key Midtrans belum dikonfigurasi. Hubungi admin."),
    );
  }

  if (scriptPromise) {
    return scriptPromise;
  }

  scriptPromise = new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");

    script.id = SNAP_SCRIPT_ID;
    script.src = SNAP_SCRIPT_URL;
    script.dataset.clientKey = SNAP_CLIENT_KEY;
    script.async = true;

    script.onload = () => resolve();

    script.onerror = () => {
      // Elemen yang gagal wajib dibuang: kalau ditinggal di <head>, percobaan
      // berikutnya menempel listener ke <script> mati yang tidak akan pernah
      // memicu load/error lagi — promise-nya menggantung dan tombol muter terus.
      script.remove();
      scriptPromise = null;
      reject(new SnapError("Gagal memuat sistem pembayaran. Cek koneksi lalu coba lagi."));
    };

    document.head.appendChild(script);
  });

  return scriptPromise;
}

/**
 * Mulai unduh tanpa menunggu, untuk halaman yang tujuan utamanya transaksi.
 * Dipanggil saat halaman dibuka supaya popup tidak menunggu unduhan saat diklik.
 */
export function preloadSnap(): void {
  void loadSnapScript().catch(() => {
    // Diamkan: kegagalan pramuat dilaporkan ulang saat paySnap benar-benar
    // dipanggil, di situ baru ada konteks untuk memberi tahu user.
  });
}

/** Buka popup pembayaran; menunggu script siap kalau pramuat belum selesai. */
export async function paySnap(token: string, options: MidtransSnapOptions): Promise<void> {
  await loadSnapScript();

  if (!window.snap) {
    throw new SnapError("Sistem pembayaran tidak tersedia. Muat ulang halaman lalu coba lagi.");
  }

  window.snap.pay(token, options);
}
