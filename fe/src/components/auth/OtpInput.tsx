import React, { useRef, useEffect } from "react";

import type { OtpInputProps } from "../../api/index";

export default function OtpInput({ value, onChange, length = 6 }: OtpInputProps) {
  const inputsRef = useRef<(HTMLInputElement | null)[]>([]);
  const vals = value.split("").concat(Array(length).fill("")).slice(0, length);

  // Auto focus first input on mount
  useEffect(() => {
    inputsRef.current[0]?.focus();
  }, []);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>, idx: number) => {
    const char = e.target.value.replace(/[^0-9]/g, "");
    if (!char) return;
    const nextVals = [...vals];
    nextVals[idx] = char.charAt(char.length - 1);
    const nextStr = nextVals.join("");
    onChange(nextStr);
    
    // Focus next input
    if (idx < length - 1) {
      inputsRef.current[idx + 1]?.focus();
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>, idx: number) => {
    if (e.key === "Backspace") {
      if (!vals[idx] && idx > 0) {
        // If current cell is empty, move focus back and erase
        const nextVals = [...vals];
        nextVals[idx - 1] = "";
        onChange(nextVals.join(""));
        inputsRef.current[idx - 1]?.focus();
      } else {
        // Erase current
        const nextVals = [...vals];
        nextVals[idx] = "";
        onChange(nextVals.join(""));
      }
    }
  };

  const handlePaste = (e: React.ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    const text = e.clipboardData.getData("text").replace(/[^0-9]/g, "").slice(0, length);
    if (text) {
      onChange(text);
      const nextFocus = Math.min(text.length, length - 1);
      inputsRef.current[nextFocus]?.focus();
    }
  };

  return (
    <div className="flex gap-2.5 justify-center">
      {Array.from({ length }).map((_, idx) => (
        <input
          key={idx}
          ref={(el) => (inputsRef.current[idx] = el)}
          type="text"
          inputMode="numeric"
          pattern="[0-9]*"
          maxLength={1}
          value={vals[idx]}
          onChange={(e) => handleChange(e, idx)}
          onKeyDown={(e) => handleKeyDown(e, idx)}
          onPaste={handlePaste}
          className="w-11 h-11 text-center font-bold text-lg rounded-xl outline-none"
          style={{
            border: "1.5px solid var(--color-border-input)",
            color: "var(--color-brand-primary)",
            backgroundColor: "var(--color-surface)",
          }}
          onFocus={(e) => { e.target.style.borderColor = "var(--color-brand-tertiary)"; }}
          onBlur={(e) => { e.target.style.borderColor = "var(--color-border-input)"; }}
          aria-label={`OTP Digit ${idx + 1}`}
        />
      ))}
    </div>
  );
}
