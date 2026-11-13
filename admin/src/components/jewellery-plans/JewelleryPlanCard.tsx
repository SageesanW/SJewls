"use client";

import React, { useState } from "react";
import { JewelleryPlan } from "../../types/jewelleryPlan";
import { Card } from "../ui/card";
import { Button } from "../ui/button";
import { Progress } from "../ui/progress";
import { cn, formatGrams } from "../../lib/utils";
import {
  Gem,
  Users,
  Edit2,
  MoreVertical,
  Power,
  RefreshCw,
  Scale,
  Clock,
  Building2,
  ArrowRight,
} from "lucide-react";

export interface JewelleryPlanCardProps {
  plan: JewelleryPlan;
  onViewCustomers?: () => void;
  onEdit?: () => void;
  onDeactivate?: () => void;
  onReopen?: () => void;
  className?: string;
}

export function JewelleryPlanCard({
  plan,
  onViewCustomers,
  onEdit,
  onDeactivate,
  onReopen,
  className,
}: JewelleryPlanCardProps) {
  const [imageError, setImageError] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  const isScheduled = plan.displayStatus === "Scheduled";
  const isDeactivated = plan.displayStatus === "Deactivated";

  return (
    <Card
      className={cn(
        "flex flex-col justify-between overflow-hidden border border-[#E8E5DD] hover:border-[#D8D3C5] hover:shadow-lg transition-all duration-300 bg-white group rounded-2xl",
        className
      )}
    >
      <div>
        {/* Generous Product Image Showcase Container */}
        <div className="relative w-full h-52 sm:h-56 bg-gradient-to-b from-stone-100 to-stone-50 overflow-hidden border-b border-[#E8E5DD]">
          {plan.imageUrl && !imageError ? (
            <img
              src={plan.imageUrl}
              alt={plan.name}
              onError={() => setImageError(true)}
              className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500 ease-out"
              loading="lazy"
            />
          ) : (
            /* Luxury Fallback with Deep Emerald & Gold Monogram */
            <div className="w-full h-full bg-gradient-to-br from-[#061E06] via-[#092B09] to-[#041604] flex flex-col items-center justify-center p-5 text-center select-none relative overflow-hidden">
              <div className="absolute inset-0 bg-[radial-gradient(#D4AF37_1px,transparent_1px)] [background-size:18px_18px] opacity-15" />
              <div className="w-14 h-14 rounded-2xl bg-white/10 border border-[#C5A059]/40 flex items-center justify-center mb-2.5 shadow-md backdrop-blur-xs">
                <Gem className="w-7 h-7 text-[#D4AF37]" />
              </div>
              <span className="text-sm font-serif-luxury font-bold text-white tracking-wide">
                {plan.categoryName || "Fine Jewellery"}
              </span>
              <span className="text-[10px] text-[#C5A059] uppercase tracking-[0.2em] font-semibold mt-0.5">
                SJewls Haute Collection
              </span>
            </div>
          )}

          {/* Floating Category Tag */}
          <div className="absolute top-3.5 left-3.5">
            <span className="px-3 py-1 rounded-full text-[10.5px] font-bold tracking-wider uppercase bg-white/95 text-[#151A14] shadow-sm backdrop-blur-md border border-white/80">
              {plan.categoryName || "Jewellery"}
            </span>
          </div>

          {/* Floating Status Badge */}
          <div className="absolute top-3.5 right-3.5">
            <span
              className={cn(
                "inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-bold shadow-sm backdrop-blur-md border",
                isDeactivated
                  ? "bg-rose-50/95 text-rose-800 border-rose-200"
                  : isScheduled
                  ? "bg-amber-50/95 text-amber-900 border-amber-200"
                  : "bg-emerald-50/95 text-emerald-900 border-emerald-200"
              )}
            >
              <span
                className={cn(
                  "w-1.5 h-1.5 rounded-full",
                  isDeactivated
                    ? "bg-rose-500"
                    : isScheduled
                    ? "bg-amber-500"
                    : "bg-emerald-500"
                )}
              />
              {plan.displayStatus}
            </span>
          </div>
        </div>

        {/* Content Body */}
        <div className="p-5 space-y-4">
          {/* Plan Header: Branch, Code & Generous Plan Name */}
          <div>
            <div className="flex items-center gap-1.5 text-[11px] text-stone-500 mb-1">
              <Building2 className="w-3.5 h-3.5 text-[#C5A059] flex-shrink-0" />
              <span className="truncate font-medium">{plan.branchName}</span>
              {plan.code && (
                <span className="font-mono text-[10px] text-stone-400">
                  • {plan.code}
                </span>
              )}
            </div>

            <h3
              className="text-lg font-serif-luxury font-bold text-[#151A14] tracking-tight leading-snug line-clamp-2 group-hover:text-[#0B3D0B] transition-colors"
              title={plan.name}
            >
              {plan.name}
            </h3>

            {plan.description && (
              <p className="text-xs text-stone-500 line-clamp-1 mt-1 font-medium">
                {plan.description}
              </p>
            )}
          </div>

          {/* Key Specifications Grid */}
          <div className="grid grid-cols-2 gap-2.5 p-3 rounded-xl bg-[#FAF9F6] border border-[#E8E5DD]">
            {/* Target Weight */}
            <div>
              <span className="text-[10px] font-bold uppercase tracking-[0.14em] text-stone-500 flex items-center gap-1">
                <Scale className="w-3 h-3 text-[#C5A059]" />
                Target Weight
              </span>
              <span className="text-base font-extrabold text-[#0B3D0B] block tabular-nums mt-0.5">
                {formatGrams(plan.targetProductGoldWeightGrams)}
              </span>
            </div>

            {/* Duration */}
            <div>
              <span className="text-[10px] font-bold uppercase tracking-[0.14em] text-stone-500 flex items-center gap-1">
                <Clock className="w-3 h-3 text-[#C5A059]" />
                Durations
              </span>
              <span className="text-sm font-bold text-stone-800 block tabular-nums mt-0.5">
                {plan.allowedDurationsMonths?.length ? `${plan.allowedDurationsMonths.join(", ")} Mos` : "Flexible"}
              </span>
            </div>
          </div>

          {/* Customer Enrolments & Savings Progress */}
          <div>
            <div className="flex items-center justify-between text-xs mb-1.5">
              <span className="text-stone-600 font-semibold flex items-center gap-1 text-[11px]">
                <Users className="w-3.5 h-3.5 text-[#C5A059]" />
                Enrolled Customers
              </span>
              <span className="font-bold text-[#0B3D0B] tabular-nums text-[11px]">
                {plan.activeEnrolments ?? plan.totalEnrolments ?? 0} Active
              </span>
            </div>

            {plan.overallProgressPercentage !== null && plan.overallProgressPercentage !== undefined ? (
              <div className="space-y-1">
                <Progress
                  value={Math.min(100, Math.round(plan.overallProgressPercentage))}
                  className="h-1.5 bg-stone-100"
                  indicatorClassName="bg-gradient-to-r from-[#0B3D0B] to-[#C5A059]"
                />
                <div className="flex justify-between items-center text-[10px] text-stone-400">
                  <span>Savings Progress</span>
                  <span className="font-bold text-stone-700 tabular-nums">
                    {Math.round(plan.overallProgressPercentage)}%
                  </span>
                </div>
              </div>
            ) : (
              <div className="text-[11px] text-stone-400 flex items-center gap-1">
                <span>Instalment accumulation scheme</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Action Footer */}
      <div className="p-4 bg-[#FAF9F6] border-t border-[#E8E5DD] flex items-center justify-between gap-2">
        {onViewCustomers ? (
          <Button
            type="button"
            variant="default"
            size="sm"
            onClick={onViewCustomers}
            className="flex-1 btn-primary-green text-xs font-bold py-2 rounded-xl flex items-center justify-center gap-1.5 cursor-pointer shadow-xs"
          >
            <span>View Customers</span>
            <ArrowRight className="w-3.5 h-3.5 text-[#D4AF37]" />
          </Button>
        ) : (
          <div />
        )}

        {/* Action Menu (Edit / Deactivate / Reopen) */}
        {(onEdit || onDeactivate || onReopen) && (
          <div className="relative">
            <button
              type="button"
              onClick={() => setMenuOpen(!menuOpen)}
              className="p-2 rounded-xl text-stone-500 hover:text-stone-900 hover:bg-white border border-[#E8E5DD] transition-colors cursor-pointer focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#0B3D0B]"
              aria-label="Plan actions"
              aria-expanded={menuOpen}
            >
              <MoreVertical className="w-4 h-4" />
            </button>

            {menuOpen && (
              <>
                <div
                  className="fixed inset-0 z-20"
                  onClick={() => setMenuOpen(false)}
                />
                <div className="absolute right-0 bottom-full mb-2 w-44 rounded-xl bg-white border border-[#E8E5DD] shadow-xl py-1.5 z-30 text-xs animate-fadeIn">
                  {onEdit && (
                    <button
                      type="button"
                      onClick={() => {
                        setMenuOpen(false);
                        onEdit();
                      }}
                      className="w-full px-3.5 py-2 text-left text-stone-700 hover:bg-stone-50 flex items-center gap-2 font-medium cursor-pointer"
                    >
                      <Edit2 className="w-3.5 h-3.5 text-[#C5A059]" />
                      <span>Edit Plan</span>
                    </button>
                  )}

                  {onDeactivate && !isDeactivated && (
                    <button
                      type="button"
                      onClick={() => {
                        setMenuOpen(false);
                        onDeactivate();
                      }}
                      className="w-full px-3.5 py-2 text-left text-rose-700 hover:bg-rose-50 flex items-center gap-2 font-medium cursor-pointer"
                    >
                      <Power className="w-3.5 h-3.5" />
                      <span>Deactivate</span>
                    </button>
                  )}

                  {onReopen && isDeactivated && (
                    <button
                      type="button"
                      onClick={() => {
                        setMenuOpen(false);
                        onReopen();
                      }}
                      className="w-full px-3.5 py-2 text-left text-emerald-700 hover:bg-emerald-50 flex items-center gap-2 font-medium cursor-pointer"
                    >
                      <RefreshCw className="w-3.5 h-3.5" />
                      <span>Reactivate</span>
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
        )}
      </div>
    </Card>
  );
}
