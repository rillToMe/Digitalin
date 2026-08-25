import { useRef, useEffect, useCallback, useState } from "react";
import { useNavigate } from "react-router";
import { Search, X, Loader2, AlertCircle, Gamepad2 } from "lucide-react";
import { useSearchGames } from "../../hooks/useSearchGames";

interface SearchBarProps {
  /** Variant controls layout: "expandable" for desktop (icon → expand), "overlay" for mobile fullscreen */
  variant?: "expandable" | "overlay";
  /** Called when overlay should close (only used in overlay variant) */
  onClose?: () => void;
}

export default function SearchBar({ variant = "expandable", onClose }: SearchBarProps) {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  const {
    query,
    setQuery,
    results,
    isLoading,
    isError,
    isFetching,
    isOpen,
    setIsOpen,
    activeIndex,
    setActiveIndex,
  } = useSearchGames();

  const [isExpanded, setIsExpanded] = useState(false);
  const hasQuery = query.trim().length > 0;
  const showDropdown = isOpen && hasQuery && (isExpanded || variant === "overlay");

  // Auto-focus input when expanded
  useEffect(() => {
    if (isExpanded || variant === "overlay") {
      const timer = setTimeout(() => inputRef.current?.focus(), 150);
      return () => clearTimeout(timer);
    }
  }, [isExpanded, variant]);

  // Click outside handler
  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsOpen(false);
        if (variant === "expandable") {
          setIsExpanded(false);
          setQuery("");
        }
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, [setIsOpen, variant, setQuery]);

  // Navigate to selected game
  const handleSelect = useCallback(
    (slug: string) => {
      setIsOpen(false);
      setQuery("");
      if (variant === "expandable") setIsExpanded(false);
      onClose?.();
      navigate(`/game/${slug}`);
    },
    [navigate, setIsOpen, setQuery, onClose, variant, setIsExpanded]
  );

  // Keyboard navigation
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      e.preventDefault();
      setIsOpen(false);
      if (variant === "expandable") {
        setIsExpanded(false);
        setQuery("");
      }
      if (variant === "overlay") onClose?.();
      return;
    }
    if (!showDropdown) return;
    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        setActiveIndex(Math.min(activeIndex + 1, results.length - 1));
        break;
      case "ArrowUp":
        e.preventDefault();
        setActiveIndex(Math.max(activeIndex - 1, -1));
        break;
      case "Enter":
        e.preventDefault();
        if (activeIndex >= 0 && activeIndex < results.length) {
          handleSelect(results[activeIndex].slug);
        }
        break;
    }
  };

  // Scroll active item into view
  useEffect(() => {
    if (activeIndex >= 0 && listRef.current) {
      const activeItem = listRef.current.children[activeIndex] as HTMLElement | undefined;
      activeItem?.scrollIntoView({ block: "nearest" });
    }
  }, [activeIndex]);

//   const isOverlay = variant === "overlay";

  // Desktop search stays visible; mobile still uses the overlay variant.
  if (variant === "expandable") {
    return (
      <div ref={containerRef} className="min-w-0" style={{ position: "relative" }}>
        <div
          className="flex h-10 w-[220px] items-center gap-2 border px-3 xl:w-[260px]"
          style={{
            backgroundColor: "var(--color-surface-raised)",
            borderColor:
              isOpen && hasQuery
                ? "var(--color-brand-tertiary)"
                : "var(--color-border-light)",
            clipPath:
              "polygon(0 0, calc(100% - 6px) 0, 100% 6px, 100% 100%, 6px 100%, 0 calc(100% - 6px))",
          }}
        >
            <Search size={16} style={{ color: "var(--color-brand-secondary)", flexShrink: 0 }} />
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={(e) => {
                setQuery(e.target.value);
                if (!isOpen) setIsOpen(true);
              }}
              onFocus={() => {
                setIsExpanded(true);
                if (hasQuery) setIsOpen(true);
              }}
              onKeyDown={handleKeyDown}
              placeholder="Cari game..."
              aria-label="Cari game"
              aria-expanded={showDropdown}
              aria-controls="search-results-list"
              aria-activedescendant={activeIndex >= 0 ? `search-result-${activeIndex}` : undefined}
              autoComplete="off"
              className="flex-1 outline-none bg-transparent"
              style={{ color: "var(--color-brand-primary)", fontSize: "0.875rem", minWidth: 0 }}
            />
            {isFetching && <Loader2 size={14} className="animate-spin" style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }} />}
            {hasQuery && !isFetching && (
              <button
                onClick={() => { setQuery(""); setIsOpen(false); inputRef.current?.focus(); }}
                aria-label="Hapus pencarian"
                className="p-0.5 rounded-full transition-colors hover:opacity-70"
                style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }}
              >
                <X size={14} />
              </button>
            )}
        </div>

        {/* Dropdown */}
        {showDropdown && (
          <div
            style={{
              position: "absolute",
              top: "calc(100% + 8px)",
              left: 0,
              width: "min(340px, 90vw)",
              backgroundColor: "var(--color-surface, #fff)",
              border: "1px solid rgba(0,0,0,0.08)",
              borderRadius: 12,
              boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
              overflow: "hidden",
              zIndex: 100,
              maxHeight: 380,
            }}
          >
            {isLoading && results.length === 0 && (
              <div className="flex items-center gap-3 px-4 py-4">
                <Loader2 size={16} className="animate-spin" style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />
                <span style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem" }}>Mencari game...</span>
              </div>
            )}
            {isError && (
              <div className="flex items-center gap-3 px-4 py-4">
                <AlertCircle size={16} style={{ color: "#ef4444", flexShrink: 0 }} />
                <span style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem" }}>Terjadi kesalahan. Silakan coba lagi.</span>
              </div>
            )}
            {!isLoading && !isError && results.length === 0 && (
              <div className="flex flex-col items-center gap-2 px-4 py-6">
                <Gamepad2 size={24} style={{ color: "var(--color-brand-secondary, #4A5568)", opacity: 0.5 }} />
                <span style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem", textAlign: "center" }}>Tidak ditemukan game yang sesuai.</span>
              </div>
            )}
            {results.length > 0 && (
              <ul
                ref={listRef}
                id="search-results-list"
                role="listbox"
                aria-label="Hasil pencarian game"
                style={{ listStyle: "none", margin: 0, padding: 0, overflowY: "auto", maxHeight: 380 }}
              >
                {results.map((game, index) => {
                  const isActive = index === activeIndex;
                  return (
                    <li
                      key={game.id}
                      id={`search-result-${index}`}
                      role="option"
                      aria-selected={isActive}
                      onMouseEnter={() => setActiveIndex(index)}
                      onClick={() => handleSelect(game.slug)}
                      className="flex items-center gap-3 cursor-pointer transition-colors"
                      style={{
                        padding: "10px 16px",
                        backgroundColor: isActive ? "rgba(44,94,245,0.07)" : "transparent",
                        borderBottom: index < results.length - 1 ? "1px solid rgba(0,0,0,0.05)" : undefined,
                      }}
                    >
                      <div
                        className="flex-shrink-0 rounded-lg overflow-hidden flex items-center justify-center"
                        style={{ width: 40, height: 40, backgroundColor: game.imageUrl ? "transparent" : "var(--color-neutral, #F1F3F5)" }}
                      >
                        {game.imageUrl ? (
                          <img src={game.imageUrl} alt={game.brand} className="w-full h-full object-cover" />
                        ) : (
                          <Gamepad2 size={18} style={{ color: "var(--color-brand-secondary, #4A5568)" }} />
                        )}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="truncate" 
                        style={{ 
                          color: "var(--color-brand-primary, #0F1419)", 
                          fontSize: "0.875rem", 
                          fontWeight: 600, 
                          margin: 0 
                          }}>{game.brand}
                        </p>

                        <p
                          className="truncate"
                            style={{
                              color: "var(--color-brand-secondary, #4A5568)",
                              fontSize: "0.75rem",
                              margin: "2px 0 0",
                            }}
                          >
                          Top Up Game
                        </p>
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        )}
      </div>
    );
  }

  // Overlay (Mobile)
  return (
    <div
      ref={containerRef}
      className="w-full"
      style={{ maxWidth: 560, margin: "0 auto" }}
    >
      <div
        className="flex items-center gap-2 rounded-xl transition-all"
        style={{
          backgroundColor: "var(--color-surface, #fff)",
          border: "1.5px solid",
          borderColor: isOpen && hasQuery ? "var(--color-brand-tertiary, #2C5EF5)" : "rgba(0,0,0,0.08)",
          padding: "0 12px",
          height: 48,
        }}
      >
        <Search size={18} style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }} />
        <input
          ref={inputRef}
          type="text"
          value={query}
          onChange={(e) => {
            setQuery(e.target.value);
            if (!isOpen) setIsOpen(true);
          }}
          onFocus={() => { if (hasQuery) setIsOpen(true); }}
          onKeyDown={handleKeyDown}
          placeholder="Cari game..."
          aria-label="Cari game"
          aria-expanded={showDropdown}
          aria-controls="search-results-list"
          aria-activedescendant={activeIndex >= 0 ? `search-result-${activeIndex}` : undefined}
          autoComplete="off"
          className="flex-1 outline-none bg-transparent"
          style={{ color: "var(--color-brand-primary, #0F1419)", fontSize: "0.95rem" }}
        />
        {isFetching && <Loader2 size={14} className="animate-spin" style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }} />}
        {hasQuery && !isFetching && (
          <button
            onClick={() => { setQuery(""); setIsOpen(false); inputRef.current?.focus(); }}
            aria-label="Hapus pencarian"
            className="p-0.5 rounded-full transition-colors hover:opacity-70"
            style={{ color: "var(--color-brand-secondary, #4A5568)", flexShrink: 0 }}
          >
            <X size={14} />
          </button>
        )}
      </div>

      {showDropdown && (
        <div
          className="mt-2"
          style={{
            backgroundColor: "var(--color-surface, #fff)",
            border: "1px solid rgba(0,0,0,0.08)",
            borderRadius: 12,
            boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
            overflow: "hidden",
            zIndex: 100,
            maxHeight: 380,
          }}
        >
          {isLoading && results.length === 0 && (
            <div className="flex items-center gap-3 px-4 py-4">
              <Loader2 size={16} className="animate-spin" style={{ color: "var(--color-brand-tertiary, #2C5EF5)" }} />
              <span style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem" }}>Mencari game...</span>
            </div>
          )}
          {isError && (
            <div className="flex items-center gap-3 px-4 py-4">
              <AlertCircle size={16} style={{ color: "#ef4444", flexShrink: 0 }} />
              <span style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem" }}>Terjadi kesalahan. Silakan coba lagi.</span>
            </div>
          )}
          {!isLoading && !isError && results.length === 0 && (
            <div className="flex flex-col items-center gap-2 px-4 py-6">
              <Gamepad2 size={24} style={{ color: "var(--color-brand-secondary, #4A5568)", opacity: 0.5 }} />
              <span style={{ color: "var(--color-brand-secondary, #4A5568)", fontSize: "0.875rem", textAlign: "center" }}>Tidak ditemukan game yang sesuai.</span>
            </div>
          )}
          {results.length > 0 && (
            <ul
              ref={listRef}
              id="search-results-list"
              role="listbox"
              aria-label="Hasil pencarian game"
              style={{ listStyle: "none", margin: 0, padding: 0, overflowY: "auto", maxHeight: 380 }}
            >
              {results.map((game, index) => {
                const isActive = index === activeIndex;
                return (
                  <li
                    key={game.id}
                    id={`search-result-${index}`}
                    role="option"
                    aria-selected={isActive}
                    onMouseEnter={() => setActiveIndex(index)}
                    onClick={() => handleSelect(game.slug)}
                    className="flex items-center gap-3 cursor-pointer transition-colors"
                    style={{
                      padding: "10px 16px",
                      backgroundColor: isActive ? "rgba(44,94,245,0.07)" : "transparent",
                      borderBottom: index < results.length - 1 ? "1px solid rgba(0,0,0,0.05)" : undefined,
                    }}
                  >
                    <div
                      className="flex-shrink-0 rounded-lg overflow-hidden flex items-center justify-center"
                      style={{ width: 40, height: 40, backgroundColor: game.imageUrl ? "transparent" : "var(--color-neutral, #F1F3F5)" }}
                    >
                      {game.imageUrl ? (
                        <img src={game.imageUrl} alt={game.brand} className="w-full h-full object-cover" />
                      ) : (
                        <Gamepad2 size={18} style={{ color: "var(--color-brand-secondary, #4A5568)" }} />
                      )}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="truncate" 
                      style={{ 
                        color: "var(--color-brand-primary, #0F1419)", 
                        fontSize: "0.875rem", 
                        fontWeight: 600, 
                        margin: 0 
                        }}>{game.brand}
                      </p>

                      <p
                        className="truncate"
                        style={{
                          color: "var(--color-brand-secondary, #4A5568)",
                          fontSize: "0.75rem",
                          margin: "2px 0 0",
                        }}
                      >
                        Top Up Game
                      </p>
                    </div>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
