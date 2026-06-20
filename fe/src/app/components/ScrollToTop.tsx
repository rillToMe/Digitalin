import { useEffect } from "react";
import { useLocation } from "react-router";

/**
 * Scrolls the window to the top whenever the route pathname changes.
 * Place this component once inside <BrowserRouter> for global coverage.
 * Uses "instant" behavior to avoid jarring visual jumps after navigation.
 */
export default function ScrollToTop() {
  const { pathname } = useLocation();

  useEffect(() => {
    window.scrollTo({ top: 0, behavior: "instant" });
  }, [pathname]);

  return null;
}
