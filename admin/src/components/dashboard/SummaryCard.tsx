"use client";

import React from "react";
import Link from "next/link";
import { LucideIcon, ArrowRight, AlertCircle, RefreshCw } from "lucide-react";
import { Card } from "../ui/card";
import { cn } from "../../lib/utils";

export interface SummaryCardProps {
  label: string;
  value: string | number | null | undefined;
  icon: LucideIcon;
  subtext?: string;
  href?: string;
  linkText?: string;
  variant?: "default" | "gold" | "emerald" | "blue";
  isLoading?: boolean;
  error?: string | null;
  onRetry?: () => void;
  className?: string;
}

export function SummaryCard({
  label,
  value,
  icon: Icon,
  subtext,
  href,
  linkText,
  variant = "default",
  isLoading = false,
  error = null,
  onRetry,
  className,
}: SummaryCardProps) {
  const iconVariantStyles = {
    default: "bg-[#C5A059]/12 text-[#0B3D0B] border-[#C5A059]/25",
    gold: "bg-[#D4AF37]/15 text-[#8C6D2D] border-[#D4AF37]/30",
    emerald: "bg-[#ECFDF5] text-[#065F46] border-[#A7F3D0]",
    blue: "bg-blue-50 text-blue-900 border-blue-200/60",
  };

  const figureVariantStyles = {
    default: "text-[#0B3D0B]",
    gold: "text-[#8C6D2D]",
    emerald: "text-[#065F46]",
    blue: "text-[#151A14]",
  };

  // Error State
  if (error) {
    return (
      <Card
        className={cn(
          "p-4 sm:p-5 border-rose-200 bg-rose-50/40 relative overflow-hidden flex flex-col justify-between rounded-2xl shadow-xs",
          className
        )}
      >
        <div className="flex items-center justify-between text-stone-500 mb-2">
          <span className="text-[11px] font-bold uppercase tracking-wider text-rose-800">
            {label}
          </span>
          <div className="p-1.5 rounded-xl bg-rose-100 text-rose-700">
            <AlertCircle className="w-3.5 h-3.5" />
          </div>
        </div>
        <div className="py-2">
          <p className="text-xs text-rose-700 font-medium line-clamp-2">{error}</p>
        </div>
        {onRetry && (
          <button
            onClick={onRetry}
            className="inline-flex items-center gap-1 text-[11px] font-bold text-rose-800 hover:text-rose-950 underline mt-1 cursor-pointer"
          >
            <RefreshCw className="w-3 h-3" /> Retry
          </button>
        )}
      </Card>
    );
  }

  // Loading State
  if (isLoading) {
    return (
      <Card
        className={cn(
          "p-5 relative overflow-hidden flex flex-col justify-between bg-white border border-[#E8E5DD] rounded-2xl shadow-xs",
          className
        )}
      >
        <div className="flex items-center justify-between">
          <div className="h-3 w-24 bg-stone-100 rounded-md animate-pulse" />
          <div className="w-8 h-8 rounded-xl bg-stone-100 animate-pulse" />
        </div>
        <div className="my-3">
          <div className="h-9 w-20 bg-stone-100 rounded-lg animate-pulse" />
        </div>
        <div className="h-3 w-32 bg-stone-100 rounded-md animate-pulse" />
      </Card>
    );
  }

  const isEmpty = value === null || value === undefined || value === "";
  const displayValue = isEmpty ? "—" : typeof value === "number" ? value.toLocaleString("en-LK") : value;

  return (
    <Card
      className={cn(
        "p-5 relative overflow-hidden flex flex-col justify-between bg-white border border-[#E8E5DD] rounded-2xl shadow-xs hover:border-[#D8D3C5] hover:shadow-md transition-all duration-200 group",
        className
      )}
    >
      {/* Top Header Row */}
      <div className="flex items-center justify-between gap-2 mb-2">
        <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-stone-500 truncate">
          {label}
        </span>
        <div
          className={cn(
            "p-2 rounded-xl border flex-shrink-0 transition-transform duration-200 group-hover:scale-110",
            iconVariantStyles[variant]
          )}
        >
          <Icon className="w-4 h-4" />
        </div>
      </div>

      {/* Main Metric Focal Point */}
      <div className="my-1.5">
        <div
          className={cn(
            "text-3xl sm:text-4xl font-extrabold tracking-tight font-sans tabular-nums",
            figureVariantStyles[variant]
          )}
        >
          {displayValue}
        </div>
      </div>

      {/* Bottom Subtext or Link */}
      <div className="mt-1 text-xs text-stone-500 font-medium">
        {href ? (
          <Link
            href={href}
            className="text-[#0B3D0B] font-bold hover:text-[#124E12] inline-flex items-center gap-1.5 group/link focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-[#0B3D0B] rounded-sm transition-colors"
          >
            <span>{linkText || subtext || "View details"}</span>
            <ArrowRight className="w-3 h-3 text-[#C5A059] transition-transform duration-200 group-hover/link:translate-x-1" />
          </Link>
        ) : (
          <span className="truncate block text-stone-500 font-medium">
            {subtext || (isEmpty ? "No records recorded" : "")}
          </span>
        )}
      </div>
    </Card>
  );
}
