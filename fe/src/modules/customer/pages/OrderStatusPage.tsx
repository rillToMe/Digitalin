import { useEffect } from "react";
import { useParams, useNavigate, Link } from "react-router";
import {
  Check,
  Loader2,
  Clock,
  Gamepad2,
  CreditCard,
  CheckCircle2,
  XCircle,
  TimerOff,
  PackageX,
  Home,
  History,
} from "lucide-react";
import { toast } from "sonner";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { invoiceService } from "../../../services/api";
import { paySnap, preloadSnap, SnapError } from "../../../services/midtransSnap";
import type { PublicInvoiceResponse, OrderStatus } from "../../../api/index";
import { ImageWithFallback } from "../../../utils/ImageWithFallback";
import BackButton from "../../../components/common/BackButton";

/* ─── Pemetaan status → tahap timeline ───
   Backend punya 8 status internal; halaman ini hanya memvisualkan 4 tahap
   logis. Status di luar daftar (REFUNDED) berakhir di panel terminal. */

const POLL_INTERVAL_MS = 5_000;

/** Terminal = polling berhenti otomatis. */
const TERMINAL: ReadonlySet<OrderStatus> = new Set([
  "COMPLETED",
  "FAILED",
  "EXPIRED",
  "REFUNDED",
]);

/**
 * Tahap timeline untuk status non-terminal:
 * 1 = Menunggu Pembayaran (current)
 * 2 = Pembayaran Berhasil (current)
 * 3 = Memproses Pesanan (current) — PAID/PROCESSING/NEEDS_ACTION
 * 4 = Pesanan Selesai (current, hanya COMPLETED — terminal)
 */
function stageOf(status: OrderStatus): number {
  switch (status) {
    case "PENDING":
      return 1;
    case "PAID":
    case "PROCESSING":
    case "NEEDS_ACTION":
      return 3;
    case "COMPLETED":
      return 4;
    default:
      return 1; // terminal error — timeline digantikan panel error
  }
}

const STAGES = [
  { n: 1, label: "Menunggu Pembayaran" },
  { n: 2, label: "Pembayaran Berhasil" },
  { n: 3, label: "Memproses Pesanan" },
  { n: 4, label: "Pesanan Selesai" },
] as const;

function formatRp(n: number) {
  return "Rp " + n.toLocaleString("id-ID");
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString("id-ID", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/* ─── Timeline ─── */

function TimelineStep({
  stage,
  current,
}: {
  stage: (typeof STAGES)[number];
  current: number;
}) {
  const done = stage.n < current;
  const active = stage.n === current;

  return (
    <li className="flex items-start gap-3.5">
      {/* Titik tahap */}
      <span
        className="flex items-center justify-center rounded-full flex-shrink-0"
        style={{
          width: 26,
          height: 26,
          backgroundColor: done
            ? "var(--color-success)"
            : active
              ? "var(--color-surface)"
              : "var(--color-neutral)",
          border: active ? "2px solid var(--color-brand-tertiary)" : "none",
          color: done ? "var(--color-on-primary)" : active ? "var(--color-brand-tertiary)" : "var(--color-brand-secondary)",
        }}
      >
        {done ? (
          <Check size={13} strokeWidth={3} aria-hidden="true" />
        ) : active ? (
          <Loader2 size={13} className="animate-spin" aria-hidden="true" />
        ) : (
          <span style={{ fontSize: "0.62rem", fontWeight: 700 }}>{stage.n}</span>
        )}
      </span>

      <div className="pt-1.5 min-w-0">
        <p
          className="truncate"
          style={{
            color: done || active ? "var(--color-brand-primary)" : "var(--color-brand-secondary)",
            fontWeight: done || active ? 600 : 500,
            fontSize: "0.85rem",
          }}
        >
          {stage.label}
        </p>
      </div>
    </li>
  );
}

/** Timeline 4 tahap: selesai = centang hijau, aktif = spinner, sisanya idle. */
function Timeline({ current }: { current: number }) {
  return (
    <ol className="relative">
      {STAGES.map((stage, i) => (
        <div key={stage.n}>
          {/* Konektor: hijau bila tahap di atasnya sudah selesai. */}
          {i > 0 && (
            <span
              className="block ml-[12.5px]"
              style={{
                width: 2,
                height: 16,
                backgroundColor: i < current ? "var(--color-success)" : "var(--color-border-light)",
              }}
            />
          )}
          <TimelineStep stage={stage} current={current} />
        </div>
      ))}
    </ol>
  );
}

/* ─── Panel terminal ─── */

function TerminalPanel({ status }: { status: OrderStatus }) {
  const cfg =
    status === "COMPLETED"
      ? {
          icon: <CheckCircle2 size={30} aria-hidden="true" />,
          color: "var(--color-success)",
          bg: "var(--color-success-bg)",
          title: "Pesanan Selesai",
          desc: "Pembayaran berhasil dan top-up sudah dikirim ke akun game kamu.",
        }
      : status === "EXPIRED"
        ? {
            icon: <TimerOff size={30} aria-hidden="true" />,
            color: "var(--color-warning)",
            bg: "var(--color-warning-bg)",
            title: "Pembayaran Kedaluwarsa",
            desc: "Batas waktu pembayaran sudah terlewat. Silakan buat pesanan baru bila masih ingin top-up.",
          }
        : status === "REFUNDED"
          ? {
              icon: <TimerOff size={30} aria-hidden="true" />,
              color: "var(--color-info)",
              bg: "var(--color-info-bg)",
              title: "Dana Dikembalikan",
              desc: "Pesanan tidak dapat diproses dan dana kamu telah dikembalikan.",
            }
          : {
              icon: <XCircle size={30} aria-hidden="true" />,
              color: "var(--color-danger)",
              bg: "var(--color-danger-bg)",
              title: "Pesanan Tidak Berhasil",
              desc: "Pembayaran tidak berhasil diproses. Bila ada dana terpotong, akan otomatis dikembalikan oleh pihak pembayaran.",
            };

  return (
    <div
      className="flex items-center gap-4 rounded-2xl p-5"
      style={{ backgroundColor: cfg.bg }}
    >
      <span style={{ color: cfg.color, flexShrink: 0 }}>{cfg.icon}</span>
      <div className="min-w-0">
        <h2 style={{ color: cfg.color, fontWeight: 700, fontSize: "0.95rem" }}>{cfg.title}</h2>
        <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem", marginTop: 2, lineHeight: 1.5 }}>
          {cfg.desc}
        </p>
      </div>
    </div>
  );
}

/* ─── Halaman ─── */

/**
 * Status Pesanan — halaman PUBLIK (guest boleh): timeline tahap dari status
 * order aktual (backend = sumber kebenaran), polling 5 detik via TanStack
 * Query selama non-terminal, berhenti otomatis saat selesai/gagal/expired.
 * Data dari endpoint invoice publik yang sama dengan Cek Transaksi.
 */
export default function OrderStatusPage() {
  const { orderNo } = useParams<{ orderNo: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: invoice, isLoading, isError, error } = useQuery({
    queryKey: ["invoice", orderNo],
    queryFn: () => invoiceService.get(orderNo!),
    // Polling hanya saat non-terminal; false = berhenti permanen.
    refetchInterval: (query) => {
      const data = query.state.data as PublicInvoiceResponse | undefined;
      if (!data || TERMINAL.has(data.status)) return false;
      return POLL_INTERVAL_MS;
    },
    // 404 = definitif, jangan retry; error lain (jaringan) retry singkat.
    retry: (failureCount, err) => {
      const status = (err as { response?: { status?: number } })?.response?.status;
      if (status === 404) return false;
      return failureCount < 2;
    },
  });

  const isTerminal = invoice ? TERMINAL.has(invoice.status) : false;
  const isCompleted = invoice?.status === "COMPLETED";
  const isPending = invoice?.status === "PENDING";
  const isNeedsAction = invoice?.status === "NEEDS_ACTION";

  /* snap.js hanya ditarik saat benar-benar akan dipakai (ada pesanan PENDING
     yang bisa dibayar) — pola yang sama dengan Riwayat Order. */
  const canPay = isPending && !!invoice?.snapToken;
  useEffect(() => {
    if (canPay) preloadSnap();
  }, [canPay]);

  const handlePay = () => {
    if (!invoice?.snapToken) return;
    void paySnap(invoice.snapToken, {
      onSuccess: () => {
        toast.success("Pembayaran Berhasil!");
        queryClient.invalidateQueries({ queryKey: ["invoice", orderNo] });
      },
      onPending: () => {
        toast.info("Pembayaran Sedang Diproses.");
        queryClient.invalidateQueries({ queryKey: ["invoice", orderNo] });
      },
      onError: () => {
        toast.error("Pembayaran Gagal. Silakan coba lagi.");
      },
      onClose: () => {
        // Popup ditutup — status akan ter-refresh polling berikutnya.
        queryClient.invalidateQueries({ queryKey: ["invoice", orderNo] });
      },
    }).catch((err: unknown) => {
      toast.error(
        err instanceof SnapError ? err.message : "Sistem pembayaran belum siap. Silakan coba lagi.",
      );
    });
  };

  const notFound =
    isError && (error as { response?: { status?: number } })?.response?.status === 404;

  return (
    <div className="max-w-xl mx-auto px-4 py-10">
      <BackButton />

      <h1 style={{ color: "var(--color-brand-primary,#0F1419)", fontWeight: 700, fontSize: "1.3rem" }}>
        Status Pesanan
      </h1>
      <p className="label-mono" style={{ color: "var(--color-brand-secondary)", fontSize: "0.72rem", marginTop: 4, marginBottom: 28 }}>
        {orderNo}
      </p>

      {isLoading ? (
        /* Skeleton — konteks halaman, bukan spinner global. */
        <div className="space-y-3">
          <div className="h-20 rounded-2xl animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
          <div className="h-10 rounded-2xl animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
          <div className="h-32 rounded-2xl animate-pulse" style={{ backgroundColor: "var(--color-skeleton)" }} />
        </div>
      ) : notFound ? (
        <div className="text-center py-14 rounded-2xl" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
          <PackageX size={36} style={{ color: "var(--color-brand-secondary)", margin: "0 auto 10px" }} />
          <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.95rem" }}>Invoice tidak ditemukan.</p>
          <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", marginTop: 4 }}>
            Periksa kembali tautan status pesanan kamu.
          </p>
        </div>
      ) : isError ? (
        <div className="text-center py-14 rounded-2xl" style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}>
          <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.95rem" }}>
            Transaksi tidak dapat dimuat.
          </p>
          <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.82rem", marginTop: 4 }}>
            Silakan coba lagi nanti.
          </p>
          <button
            type="button"
            onClick={() => queryClient.invalidateQueries({ queryKey: ["invoice", orderNo] })}
            className="mt-4 px-5 py-2.5 rounded-xl"
            style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.85rem" }}
          >
            Coba Lagi
          </button>
        </div>
      ) : invoice ? (
        <div className="space-y-5">
          {/* ── Timeline / status utama ── */}
          {isTerminal ? (
            <TerminalPanel status={invoice.status} />
          ) : (
            <div
              className="rounded-2xl p-6"
              style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
            >
              <Timeline current={stageOf(invoice.status)} />

              {/* Catatan tahap saat ini */}
              <div className="mt-6 pt-4" style={{ borderTop: "1px solid var(--color-border-lighter)" }}>
                {isPending ? (
                  <div className="space-y-2">
                    <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.9rem" }}>
                      Menunggu Pembayaran
                    </p>
                    <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem", lineHeight: 1.5 }}>
                      Selesaikan pembayaran sebesar{" "}
                      <span className="tabular-price" style={{ color: "var(--color-brand-tertiary)", fontWeight: 700 }}>
                        {formatRp(invoice.amount)}
                      </span>{" "}
                      agar pesanan diproses.
                    </p>
                    {invoice.expiresAt && (
                      <p className="flex items-center gap-1.5" style={{ color: "var(--color-warning)", fontSize: "0.78rem" }}>
                        <Clock size={12} />
                        Batas pembayaran: {formatDateTime(invoice.expiresAt)}
                      </p>
                    )}
                    {canPay && (
                      <button
                        type="button"
                        onClick={handlePay}
                        className="w-full mt-2 py-3 rounded-xl flex items-center justify-center gap-2"
                        style={{
                          backgroundColor: "var(--color-brand-tertiary)",
                          color: "var(--color-on-primary)",
                          fontWeight: 600,
                          fontSize: "0.9rem",
                        }}
                      >
                        <CreditCard size={15} />
                        Bayar Sekarang
                      </button>
                    )}
                  </div>
                ) : (
                  <div className="space-y-2">
                    <p style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.9rem" }}>
                      Memproses Pesanan
                    </p>
                    <p style={{ color: "var(--color-brand-secondary)", fontSize: "0.8rem", lineHeight: 1.5 }}>
                      {isNeedsAction
                        ? "Pembayaran sudah diterima. Pesanan sedang ditinjau tim kami — status akan diperbarui otomatis di halaman ini."
                        : "Pembayaran sudah diterima dan pesanan sedang diproses. Status akan diperbarui otomatis di halaman ini."}
                    </p>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* ── Aksi terminal ── */}
          {isCompleted && (
            <div className="flex flex-col sm:flex-row gap-3">
              {invoice.belongsToAccount && (
                <button
                  type="button"
                  onClick={() => navigate("/history")}
                  className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2"
                  style={{ backgroundColor: "var(--color-brand-tertiary)", color: "var(--color-on-primary)", fontWeight: 600, fontSize: "0.9rem" }}
                >
                  <History size={15} />
                  Lihat Riwayat Order
                </button>
              )}
              <Link
                to="/"
                className="flex-1 py-3 rounded-xl flex items-center justify-center gap-2 no-underline"
                style={{ border: "1px solid var(--color-border)", color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.9rem" }}
              >
                <Home size={15} />
                Kembali ke Beranda
              </Link>
            </div>
          )}

          {/* ── Info pesanan ── */}
          <div
            className="rounded-2xl p-5"
            style={{ backgroundColor: "var(--color-surface)", border: "1px solid var(--color-border-light)" }}
          >
            <div className="flex items-center gap-4">
              <div
                className="w-12 h-12 rounded-xl overflow-hidden flex-shrink-0 flex items-center justify-center"
                style={{ backgroundColor: "var(--color-neutral)" }}
              >
                {invoice.imageUrl ? (
                  <ImageWithFallback
                    src={invoice.imageUrl}
                    alt={invoice.productName}
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <Gamepad2 size={20} style={{ color: "var(--color-brand-tertiary)" }} />
                )}
              </div>

              <div className="flex-1 min-w-0">
                <h2 className="truncate" style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontSize: "0.95rem" }}>
                  {invoice.productName}
                </h2>
                <p className="tabular-price" style={{ color: "var(--color-brand-tertiary)", fontWeight: 700, fontSize: "1.05rem", marginTop: 2 }}>
                  {formatRp(invoice.amount)}
                </p>
              </div>
            </div>

            <div className="mt-4 pt-4 space-y-2" style={{ borderTop: "1px solid var(--color-border-lighter)" }}>
              <div className="flex justify-between items-center gap-3" style={{ fontSize: "0.82rem" }}>
                <span style={{ color: "var(--color-brand-secondary)" }}>Invoice</span>
                <span style={{ color: "var(--color-brand-primary)", fontWeight: 600, fontFamily: "monospace" }}>
                  {invoice.invoiceNumber}
                </span>
              </div>
              <div className="flex justify-between items-center gap-3" style={{ fontSize: "0.82rem" }}>
                <span style={{ color: "var(--color-brand-secondary)" }}>Waktu Transaksi</span>
                <span style={{ color: "var(--color-brand-primary)" }}>{formatDateTime(invoice.createdAt)}</span>
              </div>
            </div>

            {invoice.belongsToAccount && (
              <p
                className="mt-4 px-4 py-3 rounded-xl"
                style={{ backgroundColor: "var(--color-info-bg)", color: "var(--color-info)", fontSize: "0.8rem" }}
              >
                Transaksi ini tersimpan di riwayat akun kamu.
              </p>
            )}
          </div>
        </div>
      ) : null}
    </div>
  );
}
