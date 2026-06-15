import { Link } from "react-router-dom";
import { Gamepad2 } from "lucide-react";
import { cn } from "@/lib/utils";
import type { BrandItem } from "@/types";

export interface GameCardProps {
  brand: BrandItem;
}

/**
 * GameCard — displays a single game brand with glassmorphism styling.
 * Links to the brand detail page where products can be selected for top-up.
 */
export function GameCard({ brand }: GameCardProps) {
  const { slug, brand: name, imageUrl } = brand;

  return (
    <Link
      to={`/games/${slug}`}
      className={cn(
        "group relative flex flex-col overflow-hidden rounded-lg",
        "bg-surface/60 backdrop-blur-md border border-surface/40 shadow-sm",
        "transition-all duration-300 hover:shadow-md hover:-translate-y-0.5",
      )}
    >
      {/* Image container with 1:1 aspect ratio */}
      <div className="relative aspect-square w-full overflow-hidden bg-neutral">
        {imageUrl ? (
          <img
            src={imageUrl}
            alt={name}
            className={cn(
              "h-full w-full object-cover",
              "transition-transform duration-300 group-hover:scale-105",
            )}
            loading="lazy"
          />
        ) : (
          /* Fallback placeholder when imageUrl is null */
          <div className="flex h-full w-full items-center justify-center bg-neutral">
            <Gamepad2 className="h-10 w-10 text-secondary/30" />
          </div>
        )}
      </div>

      {/* Brand name */}
      <div className="flex items-center justify-center px-3 py-3">
        <span className="text-sm font-medium text-primary text-center leading-tight line-clamp-2">
          {name}
        </span>
      </div>
    </Link>
  );
}
