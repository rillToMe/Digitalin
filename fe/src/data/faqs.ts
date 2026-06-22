/**
 * FAQ Data Source
 *
 * Centralized FAQ data structured for future backend integration.
 * When a backend FAQ endpoint becomes available, replace this file's
 * exports with API calls — no UI changes needed.
 */

export interface FaqItem {
  id: string;
  question: string;
  answer: string;
  category: FaqCategory;
  /** Whether this item appears on the HomePage preview */
  featured: boolean;
}

export type FaqCategory =
  | "Top Up"
  | "Pembayaran"
  | "Akun"
  | "Transaksi"
  | "Voucher"
  | "Lainnya";

export const FAQ_CATEGORIES: FaqCategory[] = [
  "Top Up",
  "Pembayaran",
  "Akun",
  "Transaksi",
  "Voucher",
  "Lainnya",
];

export const FAQ_DATA: FaqItem[] = [
  // ── Top Up ──────────────────────────────────────────────────────────────
  {
    id: "topup-1",
    question: "Bagaimana cara melakukan top up game?",
    answer:
      "Pilih game yang ingin di-top up, masukkan Player ID, pilih nominal, lalu lakukan pembayaran. Proses otomatis setelah pembayaran berhasil.",
    category: "Top Up",
    featured: true,
  },
  {
    id: "topup-2",
    question: "Berapa lama proses top up?",
    answer:
      "Proses top up umumnya instan (1–5 menit) setelah pembayaran terkonfirmasi. Beberapa produk mungkin membutuhkan waktu hingga 15 menit.",
    category: "Top Up",
    featured: true,
  },
  {
    id: "topup-3",
    question: "Apakah perlu membuat akun untuk top up?",
    answer:
      "Tidak wajib. Anda bisa melakukan top up sebagai guest. Namun, dengan akun Anda bisa melihat riwayat transaksi.",
    category: "Top Up",
    featured: false,
  },
  {
    id: "topup-4",
    question: "Player ID salah, bisa dibatalkan?",
    answer:
      "Jika sudah dibayar, top up tidak dapat dibatalkan. Pastikan Player ID benar sebelum melakukan pembayaran.",
    category: "Top Up",
    featured: true,
  },

  // ── Pembayaran ──────────────────────────────────────────────────────────
  {
    id: "pay-1",
    question: "Metode pembayaran apa saja yang didukung?",
    answer:
      "Kami mendukung berbagai metode pembayaran melalui Midtrans: GoPay, OVO, ShopeePay, transfer bank, kartu kredit, dan convenience store.",
    category: "Pembayaran",
    featured: true,
  },
  {
    id: "pay-2",
    question: "Apakah pembayaran aman?",
    answer:
      "Ya. Seluruh transaksi diproses melalui Midtrans yang sudah terenkripsi dan tersertifikasi PCI DSS.",
    category: "Pembayaran",
    featured: false,
  },
  {
    id: "pay-3",
    question: "Bagaimana jika pembayaran gagal?",
    answer:
      "Jika pembayaran gagal, Anda bisa mencoba kembali. Tidak ada biaya yang dikenakan untuk transaksi yang gagal.",
    category: "Pembayaran",
    featured: false,
  },

  // ── Akun ────────────────────────────────────────────────────────────────
  {
    id: "acc-1",
    question: "Bagaimana cara mendaftar?",
    answer:
      "Klik tombol 'Daftar' di navbar, masukkan email dan password, lalu verifikasi dengan kode OTP yang dikirim ke email Anda.",
    category: "Akun",
    featured: false,
  },
  {
    id: "acc-2",
    question: "Apakah bisa login dengan Google?",
    answer:
      "Ya. Kami mendukung login menggunakan akun Google untuk kemudahan akses.",
    category: "Akun",
    featured: false,
  },
  {
    id: "acc-3",
    question: "Bagaimana cara mengubah password?",
    answer:
      "Masuk ke halaman Profile, lalu pilih opsi ubah password. Masukkan password baru dan simpan.",
    category: "Akun",
    featured: false,
  },

  // ── Transaksi ───────────────────────────────────────────────────────────
  {
    id: "tx-1",
    question: "Bagaimana cara melihat riwayat transaksi?",
    answer:
      "Masuk ke halaman Riwayat Order (jika memiliki akun) untuk melihat status seluruh transaksi Anda.",
    category: "Transaksi",
    featured: true,
  },
  {
    id: "tx-2",
    question: "Diamond belum masuk setelah pembayaran berhasil?",
    answer:
      "Tunggu 5–15 menit. Jika masih belum masuk, hubungi kami melalui halaman Kontak dengan menyertakan nomor invoice.",
    category: "Transaksi",
    featured: true,
  },
  {
    id: "tx-3",
    question: "Apa arti status PENDING pada pesanan?",
    answer:
      "Status PENDING berarti pembayaran belum terkonfirmasi. Selesaikan pembayaran Anda agar pesanan diproses.",
    category: "Transaksi",
    featured: false,
  },

  // ── Voucher ─────────────────────────────────────────────────────────────
  {
    id: "vch-1",
    question: "Bagaimana cara menggunakan kode voucher?",
    answer:
      "Masukkan kode voucher pada kolom yang tersedia saat melakukan checkout. Diskon akan otomatis diterapkan.",
    category: "Voucher",
    featured: false,
  },
  {
    id: "vch-2",
    question: "Voucher tidak bisa digunakan?",
    answer:
      "Pastikan kode voucher masih aktif, belum kedaluwarsa, dan memenuhi minimum transaksi. Jika masih bermasalah, hubungi support.",
    category: "Voucher",
    featured: false,
  },

  // ── Lainnya ─────────────────────────────────────────────────────────────
  {
    id: "other-1",
    question: "Apakah Digitalin aman?",
    answer:
      "Ya. Digitalin menggunakan enkripsi SSL, pembayaran diproses via Midtrans (PCI DSS certified), dan data Anda dilindungi.",
    category: "Lainnya",
    featured: false,
  },
  {
    id: "other-2",
    question: "Apakah bisa refund?",
    answer:
      "Refund hanya dapat dilakukan jika pesanan gagal diproses oleh sistem. Hubungi tim support kami untuk pengajuan refund.",
    category: "Lainnya",
    featured: false,
  },
];

/**
 * Returns FAQ items marked as featured (for HomePage preview).
 */
export function getFeaturedFaqs(): FaqItem[] {
  return FAQ_DATA.filter((item) => item.featured);
}

/**
 * Returns all FAQ items, optionally filtered by category and/or search query.
 */
export function searchFaqs(params?: {
  category?: FaqCategory | "Semua";
  query?: string;
}): FaqItem[] {
  let results = FAQ_DATA;

  if (params?.category && params.category !== "Semua") {
    results = results.filter((item) => item.category === params.category);
  }

  if (params?.query?.trim()) {
    const q = params.query.toLowerCase().trim();
    results = results.filter(
      (item) =>
        item.question.toLowerCase().includes(q) ||
        item.answer.toLowerCase().includes(q)
    );
  }

  return results;
}
