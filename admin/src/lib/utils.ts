/**
 * Utility functions for SJewls Admin styling and formatting
 */

export type ClassValue = string | number | boolean | undefined | null | { [key: string]: any } | ClassValue[];

/**
 * Lightweight and robust classnames merger compatible with shadcn/ui
 */
export function cn(...inputs: ClassValue[]): string {
  const classes: string[] = [];

  for (const input of inputs) {
    if (!input) continue;

    if (typeof input === "string") {
      classes.push(input);
    } else if (typeof input === "number") {
      classes.push(String(input));
    } else if (Array.isArray(input)) {
      const inner = cn(...input);
      if (inner) classes.push(inner);
    } else if (typeof input === "object") {
      for (const [key, value] of Object.entries(input)) {
        if (value) classes.push(key);
      }
    }
  }

  return classes.join(" ");
}

/**
 * Format currency in LKR with thousands separators
 */
export function formatCurrency(amount: number | null | undefined, currency: string = "LKR"): string {
  if (amount === null || amount === undefined || isNaN(amount)) {
    return "—";
  }
  return `${currency} ${amount.toLocaleString("en-LK", { maximumFractionDigits: 2 })}`;
}

/**
 * Format gold weight in grams with 2 decimals
 */
export function formatGrams(grams: number | null | undefined): string {
  if (grams === null || grams === undefined || isNaN(grams)) {
    return "—";
  }
  return `${grams.toFixed(2)} g`;
}

