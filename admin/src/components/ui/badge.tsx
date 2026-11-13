import * as React from "react";
import { cn } from "@/lib/utils";

export interface BadgeProps extends React.HTMLAttributes<HTMLDivElement> {
  variant?:
    | "default"
    | "secondary"
    | "gold"
    | "success"
    | "warning"
    | "destructive"
    | "outline"
    | "stone";
  size?: "sm" | "default";
}

function Badge({
  className,
  variant = "default",
  size = "default",
  ...props
}: BadgeProps) {
  const variantStyles: Record<string, string> = {
    // Primary deep green with white text
    default:
      "border-transparent bg-[#0B3D0B] text-white hover:bg-[#124e12] shadow-xs",
    // Neutral soft stone
    secondary:
      "border-transparent bg-stone-100 text-stone-700 hover:bg-stone-200",
    // Gold luxury accent with deep green text
    gold:
      "border-[#FFD700]/50 bg-[#FFD700]/15 text-[#5A4500] font-semibold border shadow-2xs",
    // Emerald active / paid
    success:
      "border-emerald-200/80 bg-emerald-50 text-emerald-900 border",
    // Amber pending / scheduled / warning
    warning:
      "border-amber-200/80 bg-amber-50 text-amber-900 border",
    // Rose overdue / deactivated / unpaid
    destructive:
      "border-rose-200/80 bg-rose-50 text-rose-800 border",
    // Clean outline
    outline:
      "text-stone-700 border-stone-300 border bg-transparent",
    // Soft stone
    stone:
      "border-stone-200 bg-stone-50 text-stone-600 border",
  };

  const sizeStyles = {
    sm: "px-2 py-0.5 text-[10px]",
    default: "px-2.5 py-0.5 text-xs",
  };

  return (
    <div
      className={cn(
        "inline-flex items-center gap-1 rounded-full font-semibold transition-colors focus:outline-hidden focus:ring-2 focus:ring-[#0B3D0B]/30",
        variantStyles[variant] || variantStyles.default,
        sizeStyles[size],
        className
      )}
      {...props}
    />
  );
}

export { Badge };

