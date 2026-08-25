import { Search, X } from "lucide-react";

interface AdminSearchBarProps {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
}

export default function AdminSearchBar({ value, onChange, placeholder = "Cari..." }: AdminSearchBarProps) {
  const hasValue = value.trim().length > 0;

  return (
    <div
      className="flex items-center gap-2 rounded-xl"
      style={{
        backgroundColor: "var(--color-neutral, #F1F3F5)",
        border: "1.5px solid",
        borderColor: hasValue ? "var(--color-brand-tertiary, #2C5EF5)" : "rgba(0,0,0,0.08)",
        padding: "0 12px",
        height: 40,
        boxSizing: "border-box",
        width: 280,
      }}
    >
      <Search size={16} style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }} />
      <input
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label={placeholder}
        autoComplete="off"
        className="flex-1 outline-none bg-transparent"
        style={{ color: "var(--color-brand-primary, #0F1419)", fontSize: "0.875rem", minWidth: 0 }}
      />
      {hasValue && (
        <button
          onClick={() => onChange("")}
          aria-label="Hapus pencarian"
          className="p-0.5 rounded-full transition-colors hover:opacity-70"
          style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }}
        >
          <X size={14} />
        </button>
      )}
    </div>
  );
}
