import { Loader2 } from "lucide-react";

import type { SettingToggleProps } from "../../../api/index";

export function SettingToggle({
  checked,
  onChange,
  disabled = false,
  isLoading = false,
  activeColor = "var(--color-brand-tertiary)",
  inactiveColor = "var(--color-border)",
  size = "sm",
  title,
}: SettingToggleProps) {
  const isMd = size === "md";
  const width = isMd ? 48 : 40;
  const height = isMd ? 26 : 22;
  const thumbSize = isMd ? 20 : 16;
  const leftOff = isMd ? 4 : 3;
  const leftOn = isMd ? 24 : 20;

  return (
    <button
      type="button"
      onClick={onChange}
      disabled={disabled || isLoading}
      title={title}
      className="relative inline-flex items-center rounded-full focus:outline-none disabled:opacity-50"
      style={{
        width,
        height,
        backgroundColor: checked ? activeColor : inactiveColor,
        transition: "background-color 0.2s",
        flexShrink: 0,
      }}
    >
      {isLoading ? (
        <Loader2
          size={isMd ? 14 : 12}
          className="animate-spin"
          style={{
            position: "absolute",
            left: checked ? leftOff : leftOn,
            color: "white",
          }}
        />
      ) : (
        <span
          style={{
            position: "absolute",
            width: thumbSize,
            height: thumbSize,
            borderRadius: "50%",
            backgroundColor: "white",
            left: checked ? leftOn : leftOff,
            transition: "left 0.2s",
            boxShadow: "0 1px 3px rgba(0,0,0,0.2)",
          }}
        />
      )}
    </button>
  );
}
