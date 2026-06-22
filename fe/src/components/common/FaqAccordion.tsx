import { useState } from "react";
import { ChevronDown } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import type { FaqItem } from "../../data/faqs";

interface FaqAccordionProps {
  items: FaqItem[];
}

/**
 * Reusable FAQ accordion component.
 * Renders a list of FAQ items with animated expand/collapse.
 * Manages its own open/close state internally.
 */
export default function FaqAccordion({ items }: FaqAccordionProps) {
  const [openId, setOpenId] = useState<string | null>(null);

  const toggle = (id: string) => {
    setOpenId((prev) => (prev === id ? null : id));
  };

  return (
    <div>
      {items.map((item) => {
        const isOpen = openId === item.id;
        return (
          <div key={item.id} style={{ borderBottom: "1px solid rgba(0,0,0,0.06)" }}>
            <button
              onClick={() => toggle(item.id)}
              className="w-full flex items-center justify-between text-left py-4 px-1 transition-colors"
              style={{
                color: "var(--color-primary, #0F1419)",
                cursor: "pointer",
                background: "none",
                border: "none",
              }}
              aria-expanded={isOpen}
            >
              <span style={{ fontSize: "0.9rem", fontWeight: 500, paddingRight: 16 }}>
                {item.question}
              </span>
              <ChevronDown
                size={18}
                style={{
                  color: "var(--color-secondary, #4A5568)",
                  flexShrink: 0,
                  transition: "transform 200ms",
                  transform: isOpen ? "rotate(180deg)" : "rotate(0deg)",
                }}
              />
            </button>
            <AnimatePresence initial={false}>
              {isOpen && (
                <motion.div
                  initial={{ height: 0, opacity: 0 }}
                  animate={{ height: "auto", opacity: 1 }}
                  exit={{ height: 0, opacity: 0 }}
                  transition={{ duration: 0.2 }}
                  style={{ overflow: "hidden" }}
                >
                  <p
                    style={{
                      color: "var(--color-secondary, #4A5568)",
                      fontSize: "0.875rem",
                      paddingBottom: 16,
                      paddingLeft: 1,
                      paddingRight: 1,
                      lineHeight: 1.6,
                    }}
                  >
                    {item.answer}
                  </p>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        );
      })}
    </div>
  );
}
