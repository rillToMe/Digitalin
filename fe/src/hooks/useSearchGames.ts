import { useState, useEffect, useRef } from "react";
import { useQuery } from "@tanstack/react-query";
import { searchService } from "../services/searchService";
import type { GameSearchResult } from "../api/index";

const DEBOUNCE_MS = 300;
const SEARCH_LIMIT = 10;

interface UseSearchGamesReturn {
  query: string;
  setQuery: (v: string) => void;
  results: GameSearchResult[];
  isLoading: boolean;
  isError: boolean;
  isFetching: boolean;
  /** Whether the dropdown should be visible (open by user interaction, not auto-closed) */
  isOpen: boolean;
  setIsOpen: (v: boolean) => void;
  /** Active index for keyboard navigation (-1 = none) */
  activeIndex: number;
  setActiveIndex: (v: number) => void;
}

/**
 * Custom hook that encapsulates game search logic:
 * - Debounced API calls via TanStack Query
 * - Request cancellation when query changes
 * - Loading / error / empty state exposure
 * - Dropdown open/close state
 */
export function useSearchGames(): UseSearchGamesReturn {
  const [query, setQuery] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);
  const debounceTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounce the query
  useEffect(() => {
    if (debounceTimer.current) clearTimeout(debounceTimer.current);
    debounceTimer.current = setTimeout(() => {
      setDebouncedQuery(query);
    }, DEBOUNCE_MS);
    return () => {
      if (debounceTimer.current) clearTimeout(debounceTimer.current);
    };
  }, [query]);

  // Reset active index when results change
  useEffect(() => {
    setActiveIndex(-1);
  }, [debouncedQuery]);

  const trimmedQuery = debouncedQuery.trim();
  const shouldFetch = trimmedQuery.length > 0;

  const { data, isLoading, isError, isFetching } = useQuery({
    queryKey: ["searchGames", trimmedQuery],
    queryFn: ({ signal }) =>
      searchService.searchGames({ q: trimmedQuery, limit: SEARCH_LIMIT }, signal),
    enabled: shouldFetch,
    // Keep results visible while refetching (no flicker)
    placeholderData: (prev) => prev,
    staleTime: 1000 * 60 * 2, // 2 min — same as global default
    refetchOnWindowFocus: false,
  });

  const results = shouldFetch ? (data ?? []) : [];

  return {
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
  };
}
