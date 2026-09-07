import { LoaderCircle } from "lucide-react";

export default function PageLoader() {
  return (
    <div
      className="fixed inset-0 z-[9999] flex items-center justify-center"
      style={{
        backgroundColor: "var(--color-background)",
      }}
    >
      <div className="flex flex-col items-center gap-5">
        {/* Logo */}
        <img
          src="/favicon.svg"
          alt="Digitalin"
          className="h-16 w-16 object-contain select-none"
          draggable={false}
        />

        {/* Spinner */}
        <LoaderCircle
          className="h-8 w-8 animate-spin"
          style={{
            color: "var(--color-primary)",
          }}
        />

        {/* Text */}
        <p
          className="text-sm font-medium tracking-wide"
          style={{
            color: "var(--color-text-secondary)",
          }}
        >
          Loading...
        </p>
      </div>
    </div>
  );
}