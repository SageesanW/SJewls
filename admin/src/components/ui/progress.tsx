import * as React from "react";
import { cn } from "@/lib/utils";

interface ProgressProps extends React.HTMLAttributes<HTMLDivElement> {
  value?: number;
  indicatorClassName?: string;
  variant?: "default" | "gold" | "emerald";
}

const Progress = React.forwardRef<HTMLDivElement, ProgressProps>(
  ({ className, value = 0, indicatorClassName, variant = "default", ...props }, ref) => {
    const clampedValue = Math.min(100, Math.max(0, value || 0));

    const indicatorVariantStyles = {
      default: "bg-[#0B3D0B]",
      gold: "bg-gradient-to-r from-[#FFD700] via-[#F4C430] to-[#D4AF37]",
      emerald: "bg-emerald-600",
    };

    return (
      <div
        ref={ref}
        role="progressbar"
        aria-valuenow={clampedValue}
        aria-valuemin={0}
        aria-valuemax={100}
        className={cn(
          "relative h-2 w-full overflow-hidden rounded-full bg-stone-100",
          className
        )}
        {...props}
      >
        <div
          className={cn(
            "h-full rounded-full transition-all duration-500 ease-out",
            indicatorVariantStyles[variant],
            indicatorClassName
          )}
          style={{ width: `${clampedValue}%` }}
        />
      </div>
    );
  }
);
Progress.displayName = "Progress";

export { Progress };

