import type { HTMLAttributes } from "react";

interface CustomScrollbarProps extends HTMLAttributes<HTMLDivElement> {
  children: React.ReactNode;
}

export function CustomScrollbar({ children, className = "", ...props }: CustomScrollbarProps) {
  return (
    <div className={`custom-scrollbar ${className}`.trim()} {...props}>
      {children}
    </div>
  );
}
