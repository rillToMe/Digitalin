import React from "react";
import type { LucideIcon } from "lucide-react";

interface Props {
  icon: LucideIcon;
  title: string;
  iconColor?: string;
  rightElement?: React.ReactNode;
}

export function SettingSectionHeader({
  icon: Icon,
  title,
  iconColor = "var(--color-tertiary)",
  rightElement,
}: Props) {
  return (
    <div
      className="flex items-center justify-between mb-5 pb-3"
      style={{ borderBottom: "1px solid var(--color-border-lighter)" }}
    >
      <div className="flex items-center gap-2.5">
        <Icon size={18} style={{ color: iconColor }} />
        <h3
          style={{
            color: "var(--color-primary)",
            fontWeight: 600,
            fontSize: "1rem",
          }}
        >
          {title}
        </h3>
      </div>
      {rightElement && <div>{rightElement}</div>}
    </div>
  );
}
