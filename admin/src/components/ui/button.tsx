import * as React from "react";
import { cn } from "@/lib/utils";

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?:
    | "default"
    | "gold"
    | "destructive"
    | "outline"
    | "secondary"
    | "ghost"
    | "link";
  size?: "default" | "sm" | "lg" | "icon";
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant = "default", size = "default", ...props }, ref) => {
    const baseStyles =
      "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-xl text-xs font-semibold ring-offset-white transition-all focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#0B3D0B] focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-40 cursor-pointer select-none active:scale-[0.98]";

    const variantStyles: Record<string, string> = {
      // Primary deep green
      default:
        "bg-[#0B3D0B] text-white hover:bg-[#124e12] shadow-xs hover:shadow-sm",
      // Gold accent with dark text
      gold:
        "bg-[#FFD700] text-[#0B3D0B] hover:bg-[#f2cb00] font-bold shadow-xs hover:shadow-sm",
      // Destructive
      destructive:
        "bg-rose-600 text-white hover:bg-rose-700 shadow-xs",
      // Fine border outline
      outline:
        "border border-stone-200 bg-white hover:bg-stone-50 hover:border-stone-300 text-stone-800 shadow-2xs",
      // Subtle secondary
      secondary:
        "bg-stone-100 text-stone-900 hover:bg-stone-200",
      // Ghost
      ghost:
        "hover:bg-stone-100 text-stone-700 hover:text-stone-900",
      // Link
      link:
        "text-[#0B3D0B] underline-offset-4 hover:underline p-0 h-auto font-medium",
    };

    const sizeStyles: Record<string, string> = {
      default: "h-9 px-4 py-2",
      sm: "h-7 rounded-lg px-2.5 text-[11px]",
      lg: "h-11 rounded-xl px-6 text-sm",
      icon: "h-9 w-9 p-0",
    };

    return (
      <button
        ref={ref}
        className={cn(
          baseStyles,
          variantStyles[variant] || variantStyles.default,
          sizeStyles[size] || sizeStyles.default,
          className
        )}
        {...props}
      />
    );
  }
);
Button.displayName = "Button";

export { Button };

