"use client";

import React from "react";
import Link from "next/link";
import {
  ChituPlanCardDto,
  ChituRedemptionType,
} from "../../types/chitu";
import { Card } from "../ui/card";
import { Badge } from "../ui/badge";
import { Button } from "../ui/button";
import { Progress } from "../ui/progress";
import { cn, formatCurrency } from "../../lib/utils";
import {
  Coins,
  Clock,
  Trophy,
  Users,
  CreditCard,
  UserPlus,
  AlertTriangle,
  Building2,
  Calendar,
  ArrowRight,
  Edit2,
  CheckCircle2,
} from "lucide-react";

export interface ChituPlanCardProps {
  plan: ChituPlanCardDto;
  onEdit?: () => void;
  onAssign?: () => void;
  onPayment?: () => void;
  onRaffle?: () => void;
}

export function ChituPlanCard({
  plan,
  onEdit,
  onAssign,
  onPayment,
  onRaffle,
}: ChituPlanCardProps) {
  const isGold =
    plan.redemptionType === ChituRedemptionType.Gold ||
    plan.redemptionTypeLabel === "Gold";
  const isPlanActive =
    plan.statusLabel === "Active" ||
    (plan.status as unknown) === 2 ||
    plan.status === "Active" ||
    plan.status === "2";
  const percentAssigned = Math.min(
    100,
    Math.round(
      (plan.assignedSlotsCount / Math.max(1, plan.totalSlotCapacity)) * 100
    )
  );

  return (
    <Card className="flex flex-col justify-between overflow-hidden border border-[#E8E5DD] hover:border-[#D8D3C5] hover:shadow-lg transition-all duration-300 bg-white group rounded-2xl">
      {/* Top Section */}
      <div>
        {/* Header Ribbon */}
        <div className="p-5 border-b border-[#E8E5DD] bg-gradient-to-b from-[#FAF9F6] to-white">
          {/* Metadata badges row */}
          <div className="flex items-center justify-between gap-2 mb-2.5">
            <div className="flex items-center gap-1.5 flex-wrap">
              <span className="font-mono text-[11px] font-bold px-2 py-0.5 rounded-md bg-[#0B3D0B] text-[#D4AF37] border border-[#C5A059]/30 shadow-2xs">
                {plan.code}
              </span>
              <span className={cn(
                "inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold border",
                isGold 
                  ? "bg-[#D4AF37]/15 text-[#8C6D2D] border-[#D4AF37]/35" 
                  : "bg-emerald-50 text-emerald-800 border-emerald-200"
              )}>
                <Coins className="w-3 h-3" />
                <span>{isGold ? "Gold Entitlement" : "Cash Scheme"}</span>
              </span>
            </div>

            <div className="flex items-center gap-1.5 flex-shrink-0">
              <span className={cn(
                "inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-semibold border",
                isPlanActive 
                  ? "bg-emerald-50 text-emerald-800 border-emerald-200"
                  : "bg-stone-100 text-stone-600 border-stone-200"
              )}>
                <span className={cn("w-1.5 h-1.5 rounded-full", isPlanActive ? "bg-emerald-500" : "bg-stone-400")} />
                {plan.statusLabel || (isPlanActive ? "Active" : "Inactive")}
              </span>
              {onEdit && (
                <button
                  type="button"
                  onClick={onEdit}
                  title="Edit plan details"
                  className="p-1.5 rounded-lg text-stone-400 hover:text-stone-700 hover:bg-stone-100 transition-colors cursor-pointer focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#0B3D0B]"
                  aria-label={`Edit ${plan.name}`}
                >
                  <Edit2 className="w-3.5 h-3.5" />
                </button>
              )}
            </div>
          </div>

          {/* Leading Plan Name */}
          <h3
            className="text-base sm:text-lg font-serif-luxury font-bold text-[#151A14] tracking-tight leading-snug line-clamp-1 group-hover:text-[#0B3D0B] transition-colors"
            title={plan.name}
          >
            {plan.name}
          </h3>

          {/* Branch Subtitle */}
          <div className="flex items-center gap-1.5 text-xs text-stone-500 mt-1">
            <Building2 className="w-3.5 h-3.5 text-[#C5A059] flex-shrink-0" />
            <span className="truncate">{plan.branchName}</span>
          </div>

          {/* Prominent Financial Value Banner */}
          <div className="mt-3.5 p-3 rounded-xl bg-gradient-to-br from-[#061E06] via-[#092B09] to-[#041604] border border-[#C5A059]/30 text-white shadow-sm">
            <div className="flex items-center justify-between">
              <div>
                <span className="text-[10px] font-bold uppercase tracking-[0.16em] text-[#C5A059] block">
                  Total Benefit Value
                </span>
                <span className="text-xl sm:text-2xl font-serif-luxury font-extrabold text-white block tabular-nums mt-0.5">
                  LKR {plan.chituBenefitValue.toLocaleString()}
                </span>
              </div>
              <div className="text-right">
                <span className="text-[10px] font-bold uppercase tracking-wider text-emerald-200/70 block">
                  Instalment
                </span>
                <span className="text-xs sm:text-sm font-bold text-[#D4AF37] block tabular-nums mt-0.5">
                  LKR {plan.monthlyInstalmentAmount.toLocaleString()} / mo
                </span>
                <span className="text-[10px] text-emerald-200/60 block">
                  {plan.durationMonths} Months
                </span>
              </div>
            </div>
          </div>
        </div>

        {/* Card Body: Round & Slot Matrix */}
        <div className="p-5 space-y-4">
          {/* Side-by-side Round and Paid/Unpaid Scanning */}
          <div className="grid grid-cols-2 gap-2.5">
            {/* Box 1: Current Round */}
            <div className="p-3 rounded-xl bg-[#FAF9F6] border border-[#E8E5DD] flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between mb-1">
                  <span className="text-[10px] font-bold uppercase tracking-wider text-stone-500">
                    Round
                  </span>
                  <span className="text-xs font-bold text-[#0B3D0B]">
                    {plan.currentRoundIndex || 1} of {plan.durationMonths}
                  </span>
                </div>
                <Progress
                  value={Math.round(
                    ((plan.currentRoundIndex || 1) / Math.max(1, plan.durationMonths)) * 100
                  )}
                  className="h-1.5 bg-stone-200"
                />
              </div>

              <div className="mt-2 flex items-center gap-1 text-[10.5px] text-stone-500 font-medium">
                <Calendar className="w-3 h-3 text-[#C5A059] flex-shrink-0" />
                <span className="truncate">
                  {plan.currentRoundDueDate
                    ? `Due ${new Date(plan.currentRoundDueDate).toLocaleDateString("en-GB", {
                        day: "numeric",
                        month: "short",
                      })}`
                    : "No Due Date"}
                </span>
              </div>
            </div>

            {/* Box 2: Paid vs Unpaid Slots Scanning */}
            <div className="p-3 rounded-xl bg-[#FAF9F6] border border-[#E8E5DD] flex flex-col justify-between">
              <span className="text-[10px] font-bold uppercase tracking-wider text-stone-500 block mb-1">
                Round Collection
              </span>
              <div className="space-y-1.5">
                <div className="flex items-center justify-between text-xs">
                  <span className="flex items-center gap-1 text-emerald-700 font-semibold text-[11px]">
                    <CheckCircle2 className="w-3 h-3" /> Paid
                  </span>
                  <span className="font-bold text-emerald-800 tabular-nums">
                    {plan.currentRoundPaidSlotsCount || 0}
                  </span>
                </div>
                <div className="flex items-center justify-between text-xs">
                  <span className="flex items-center gap-1 text-amber-700 font-semibold text-[11px]">
                    <Clock className="w-3 h-3" /> Unpaid
                  </span>
                  <span className="font-bold text-amber-800 tabular-nums">
                    {plan.currentRoundUnpaidSlotsCount || 0}
                  </span>
                </div>
              </div>
            </div>
          </div>

          {/* Overdue Raffle Alert Notice */}
          {plan.hasOverdueRaffle && (
            <div className="p-2.5 rounded-xl bg-amber-50 border border-amber-200/80 flex items-center justify-between gap-2 text-amber-900 animate-pulse">
              <div className="flex items-center gap-2 min-w-0">
                <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0" />
                <span className="text-xs font-semibold truncate">
                  Round {plan.overdueRaffleRoundIndex || plan.currentRoundIndex} raffle pending
                </span>
              </div>
              {onRaffle && (
                <button
                  type="button"
                  onClick={onRaffle}
                  className="px-2.5 py-1 rounded-md text-[11px] font-bold bg-amber-600 hover:bg-amber-700 text-white flex-shrink-0 cursor-pointer transition-colors shadow-2xs"
                >
                  Raffle Now
                </button>
              )}
            </div>
          )}

          {/* Slot Allocation Capacity Progress Bar */}
          <div>
            <div className="flex items-center justify-between text-xs font-medium mb-1.5">
              <span className="text-stone-600 flex items-center gap-1 text-[11px] font-semibold">
                <Users className="w-3.5 h-3.5 text-[#C5A059]" />
                <span>Assigned Capacity</span>
              </span>
              <span className="font-bold text-stone-800 tabular-nums text-[11px]">
                {plan.assignedSlotsCount} / {plan.totalSlotCapacity} Slots ({percentAssigned}%)
              </span>
            </div>
            <Progress
              value={percentAssigned}
              className="h-2 bg-stone-100"
              indicatorClassName={
                percentAssigned >= 100
                  ? "bg-emerald-600"
                  : "bg-gradient-to-r from-[#0B3D0B] to-[#C5A059]"
              }
            />
            <div className="flex justify-between items-center text-[10.5px] text-stone-400 mt-1 font-medium">
              <span>{plan.availableSlotsCount} available</span>
              <span>{plan.winningSlotsCount} lucky winners</span>
            </div>
          </div>
        </div>
      </div>

      {/* Footer Action Buttons */}
      <div className="p-4 bg-[#FAF9F6] border-t border-[#E8E5DD] flex flex-col gap-2">
        {/* Primary View Plan Roster Button */}
        <Link href={`/chitu-plans/${plan.id}`} className="w-full">
          <Button
            variant="default"
            size="default"
            className="w-full justify-center btn-primary-green text-xs font-bold tracking-wide py-2.5 cursor-pointer rounded-xl"
          >
            <span>View Plan Roster</span>
            <ArrowRight className="w-3.5 h-3.5 text-[#D4AF37]" />
          </Button>
        </Link>

        {/* Quick Operations Row */}
        {(onAssign || onPayment || onRaffle) && (
          <div className="grid grid-cols-3 gap-1.5 pt-1">
            {onAssign && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onAssign}
                disabled={!isPlanActive || plan.availableSlotsCount <= 0}
                className="text-[11px] font-semibold h-8 px-1 text-stone-700 hover:text-[#0B3D0B] hover:border-[#0B3D0B] hover:bg-white bg-white border-[#E8E5DD] rounded-lg"
              >
                <UserPlus className="w-3 h-3 text-[#C5A059]" />
                <span className="truncate">Assign</span>
              </Button>
            )}

            {onPayment && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onPayment}
                disabled={!isPlanActive}
                className="text-[11px] font-semibold h-8 px-1 text-stone-700 hover:text-[#0B3D0B] hover:border-[#0B3D0B] hover:bg-white bg-white border-[#E8E5DD] rounded-lg"
              >
                <CreditCard className="w-3 h-3 text-[#C5A059]" />
                <span className="truncate">Pay</span>
              </Button>
            )}

            {onRaffle && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onRaffle}
                disabled={!isPlanActive}
                className={cn(
                  "text-[11px] font-semibold h-8 px-1 rounded-lg",
                  plan.hasOverdueRaffle
                    ? "bg-amber-100/60 text-amber-900 border-amber-300 font-bold hover:bg-amber-100"
                    : "text-stone-700 hover:text-[#0B3D0B] hover:border-[#0B3D0B] hover:bg-white bg-white border-[#E8E5DD]"
                )}
              >
                <Trophy className="w-3 h-3 text-[#D4AF37]" />
                <span className="truncate">Raffle</span>
              </Button>
            )}
          </div>
        )}
      </div>
    </Card>
  );
}
