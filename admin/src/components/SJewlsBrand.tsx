"use client";

import React from "react";
import { cn } from "@/lib/utils";

interface SJewlsBrandProps {
  collapsed?: boolean;
  theme?: "dark" | "light";
  className?: string;
  size?: "sm" | "md" | "lg";
}

export function SJewlsBrand({
  collapsed = false,
  theme = "dark",
  className = "",
  size = "md",
}: SJewlsBrandProps) {
  const iconBoxSize = size === "sm" ? "w-8 h-8" : size === "lg" ? "w-11 h-11" : "w-9 h-9";
  const gemSize = size === "sm" ? "w-4 h-4" : size === "lg" ? "w-6 h-6" : "w-5 h-5";
  const titleSize = size === "sm" ? "text-lg" : size === "lg" ? "text-2xl" : "text-xl";
  const subtitleSize = size === "sm" ? "text-[8.5px]" : size === "lg" ? "text-[11px]" : "text-[9.5px]";

  return (
    <div className={cn("flex items-center gap-3 select-none", className)}>
      {/* High-craft Jewel Seal Icon */}
      <div
        className={cn(
          "relative flex-shrink-0 rounded-xl bg-gradient-to-br from-[#D4AF37] via-[#C5A059] to-[#8C6D2D] p-[1.5px] shadow-sm shadow-[#C5A059]/25 transition-transform duration-200 group-hover:scale-105",
          iconBoxSize
        )}
      >
        <div className="w-full h-full bg-gradient-to-b from-[#092B09] to-[#041604] rounded-[10px] flex items-center justify-center relative overflow-hidden">
          {/* Subtle gold facet ambient glow */}
          <div className="absolute inset-0 bg-gradient-to-tr from-transparent via-[#C5A059]/20 to-transparent" />
          
          {/* Precision Diamond Facet SVG */}
          <svg
            viewBox="0 0 24 24"
            fill="none"
            className={cn("text-[#D4AF37] drop-shadow-[0_1px_3px_rgba(0,0,0,0.6)]", gemSize)}
            xmlns="http://www.w3.org/2000/svg"
          >
            {/* Outer Diamond Table & Pavilion */}
            <path
              d="M6 3.5L2.5 9.5L12 21.5L21.5 9.5L18 3.5H6Z"
              stroke="currentColor"
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            {/* Girdle Line */}
            <path
              d="M2.5 9.5H21.5"
              stroke="currentColor"
              strokeWidth="1.4"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            {/* Left Pavilion & Kite Facets */}
            <path
              d="M12 21.5L7.5 9.5L9.5 3.5"
              stroke="currentColor"
              strokeWidth="1.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            {/* Right Pavilion & Kite Facets */}
            <path
              d="M12 21.5L16.5 9.5L14.5 3.5"
              stroke="currentColor"
              strokeWidth="1.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </div>
      </div>

      {/* Brand Typography */}
      {!collapsed && (
        <div className="flex flex-col min-w-0">
          <div className="flex items-center gap-1.5 leading-none">
            <span
              className={cn(
                "font-serif-luxury font-bold tracking-[0.04em]",
                titleSize,
                theme === "dark" ? "text-white" : "text-[#0B3D0B]"
              )}
            >
              SJewls
            </span>
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-[#C5A059] shadow-xs" />
          </div>
          <span
            className={cn(
              "font-semibold uppercase tracking-[0.2em] mt-1 truncate",
              subtitleSize,
              theme === "dark" ? "text-[#C5A059]" : "text-[#8C6D2D]"
            )}
          >
            Haute Joaillerie & Chitu
          </span>
        </div>
      )}
    </div>
  );
}
