"use client";

import React from "react";

interface SJewlsBrandProps {
  collapsed?: boolean;
  theme?: "dark" | "light"; // "dark" for deep-green sidebar/dark backgrounds, "light" for white/offwhite backgrounds
  className?: string;
  size?: "sm" | "md" | "lg";
}

export function SJewlsBrand({
  collapsed = false,
  theme = "dark",
  className = "",
  size = "md",
}: SJewlsBrandProps) {
  const iconSize = size === "sm" ? "w-7 h-7" : size === "lg" ? "w-11 h-11" : "w-9 h-9";
  const titleSize = size === "sm" ? "text-lg" : size === "lg" ? "text-2xl" : "text-xl";
  const subtitleSize = size === "sm" ? "text-[9px]" : size === "lg" ? "text-xs" : "text-[10px]";

  return (
    <div className={`flex items-center gap-3 select-none ${className}`}>
      {/* Jewellery-inspired Gem / Diamond Icon */}
      <div
        className={`relative ${iconSize} flex-shrink-0 rounded-xl bg-gradient-to-br from-[#FFD700] via-[#F4C430] to-[#B8860B] p-[1.5px] shadow-sm shadow-[#FFD700]/30`}
      >
        <div className="w-full h-full bg-[#0B3D0B] rounded-[10px] flex items-center justify-center relative overflow-hidden">
          {/* Subtle background glow */}
          <div className="absolute inset-0 bg-gradient-to-tr from-transparent via-[#FFD700]/15 to-transparent" />
          
          {/* Faceted Gem SVG Icon */}
          <svg
            viewBox="0 0 24 24"
            fill="none"
            className="w-5 h-5 text-[#FFD700] drop-shadow-[0_1px_2px_rgba(0,0,0,0.5)]"
            xmlns="http://www.w3.org/2000/svg"
          >
            {/* Diamond Crown */}
            <path
              d="M6 3L2 9L12 21L22 9L18 3H6Z"
              stroke="#FFD700"
              strokeWidth="1.8"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            {/* Inner Facet Lines */}
            <path
              d="M2 9H22"
              stroke="#FFD700"
              strokeWidth="1.4"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <path
              d="M12 21L7.5 9L9.5 3"
              stroke="#FFD700"
              strokeWidth="1.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <path
              d="M12 21L16.5 9L14.5 3"
              stroke="#FFD700"
              strokeWidth="1.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        </div>
      </div>

      {/* Wordmark */}
      {!collapsed && (
        <div className="flex flex-col min-w-0">
          <div className="flex items-center gap-1.5">
            <span
              className={`font-extrabold tracking-tight ${titleSize} ${
                theme === "dark" ? "text-white" : "text-[#0B3D0B]"
              }`}
            >
              SJewls
            </span>
            <span className="inline-block w-1.5 h-1.5 rounded-full bg-[#FFD700]" />
          </div>
          <span
            className={`font-semibold uppercase tracking-wider ${subtitleSize} ${
              theme === "dark" ? "text-emerald-300/80" : "text-emerald-800/80"
            } -mt-0.5 truncate`}
          >
            Admin Portal
          </span>
        </div>
      )}
    </div>
  );
}
