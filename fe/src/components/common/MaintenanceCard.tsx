interface MaintenanceCardProps {
  title: string;
  message: string;
  estimatedFinish: string | null;
  onRefresh: () => void;
  isChecking: boolean;
  countdown: number;
}

function formatEstimatedFinish(isoString: string | null) {
  if (!isoString) return null;

  try {
    const date = new Date(isoString);

    if (isNaN(date.getTime())) return null;

    const formatter = new Intl.DateTimeFormat("id-ID", {
      timeZone: "Asia/Jakarta",
      day: "numeric",
      month: "long",
      year: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      hour12: false,
    });

    const parts = formatter.formatToParts(date);

    const getPart = (type: Intl.DateTimeFormatPartTypes) =>
      parts.find((part) => part.type === type)?.value ?? "";

    return {
      dateStr: `${getPart("day")} ${getPart("month")} ${getPart("year")}`,
      timeStr: `${getPart("hour")}:${getPart("minute")} WIB`,
    };
  } catch {
    return null;
  }
}

export default function MaintenanceCard({
  title,
  message,
  estimatedFinish,
}: MaintenanceCardProps) {
  const isEstPast = estimatedFinish
    ? new Date(estimatedFinish).getTime() <= Date.now()
    : false;

  const est = !isEstPast
    ? formatEstimatedFinish(estimatedFinish)
    : null;

  return (
    <div
      className="min-h-dvh flex items-center justify-center px-6 py-12"
      style={{
        backgroundColor: "var(--color-neutral)",
      }}
    >
      <main className="w-full max-w-md text-center">
        {/* Brand + maintenance status */}
        <header className="mb-9">
          {/* Brand */}
          <div
            className="flex items-center justify-center gap-2"
            style={{
              marginBottom: "10px",
            }}
          >
            <img
              src="/favicon.svg"
              alt=""
              aria-hidden="true"
              style={{
                height: 28,
                width: "auto",
              }}
            />

            <span
              style={{
                color: "var(--color-brand-primary)",
                fontWeight: 700,
                fontSize: "1.1rem",
                letterSpacing: "-0.02em",
                lineHeight: 1,
              }}
            >
              Digitalin
            </span>
          </div>

          {/* Status */}
          <p
            className="inline-flex items-center justify-center gap-2"
            style={{
              color: "var(--color-brand-secondary)",
              fontSize: "0.85rem",
              fontWeight: 500,
              lineHeight: 1.4,
              margin: 0,
            }}
          >
            <span
              aria-hidden="true"
              style={{
                width: 7,
                height: 7,
                borderRadius: "999px",
                backgroundColor: "var(--color-warning)",
                flexShrink: 0,
              }}
            />

            Maintenance sedang berlangsung
          </p>
        </header>

        {/* Main message */}
        <section>
          <h1
            style={{
              margin: 0,
              fontFamily: "var(--admin-font-sans)",
              color: "var(--color-brand-primary)",
              fontWeight: 650,
              fontSize: "1.6rem",
              letterSpacing: "-0.025em",
              lineHeight: 1.3,
              textTransform: "none",
            }}
          >
            {title || "Website sedang maintenance"}
          </h1>

          <p
            className="mx-auto"
            style={{
              color: "var(--color-brand-secondary)",
              fontSize: "0.92rem",
              lineHeight: 1.65,
              maxWidth: "40ch",
              marginTop: "12px",
              marginBottom: 0,
            }}
          >
            {message ||
              "Kami sedang melakukan peningkatan sistem. Silakan kembali beberapa saat lagi."}
          </p>
        </section>

        {/* Estimated completion */}
        {est && (
          <div
            style={{
              display: "inline-flex",
              flexDirection: "column",
              alignItems: "center",
              marginTop: "32px",
              padding: "12px 22px",
              backgroundColor: "var(--color-surface)",
              border: "1px solid var(--color-border-light)",
              borderRadius: "var(--admin-radius-md)",
            }}
          >
            <span
              style={{
                color: "var(--color-brand-secondary)",
                fontFamily: "var(--admin-font-mono)",
                fontSize: "0.67rem",
                fontWeight: 500,
                letterSpacing: "0.07em",
                textTransform: "uppercase",
                lineHeight: 1.4,
              }}
            >
              Perkiraan selesai
            </span>

            <span
              className="tabular-price"
              style={{
                color: "var(--color-brand-primary)",
                fontSize: "0.9rem",
                fontWeight: 600,
                lineHeight: 1.4,
                marginTop: "4px",
              }}
            >
              {est.dateStr} · {est.timeStr}
            </span>
          </div>
        )}
      </main>
    </div>
  );
}