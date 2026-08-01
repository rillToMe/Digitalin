import { useEffect } from "react";
import { useLocation } from "react-router";

/**
 * Scrolls the window to the top whenever the route pathname changes.
 * Skips scrolling when a hash is present (allows anchor-based navigation).
 * Place this component once inside <BrowserRouter> for global coverage.
 */
export default function ScrollToTop() {
  const { pathname, hash } = useLocation();

  useEffect(() => {
    if (!hash) {
      window.scrollTo({ top: 0, behavior: "instant" });
    }
  }, [pathname, hash]);

  return null;
}
