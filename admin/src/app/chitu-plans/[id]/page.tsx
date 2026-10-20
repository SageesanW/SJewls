"use client";

import React, { useEffect, useState, useCallback, use, Suspense } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AdminLayout } from "../../../components/AdminLayout";
import { adminApi } from "../../../lib/api";
import {
  ChituPlanDetailDto,
  ChituRosterResponse,
  ChituRosterRowDto,
  ChituRedemptionType,
} from "../../../types/chitu";
import {
  Coins,
  ArrowLeft,
  Users,
  Search,
  Filter,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Trophy,
  Clock,
  Building2,
  Calendar,
  CreditCard,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Award,
  AlertTriangle,
} from "lucide-react";

interface PlanDetailPageProps {
  params: Promise<{ id: string }>;
}

export default function ChituPlanDetailPage({ params }: PlanDetailPageProps) {
  return (
    <Suspense
      fallback={
        <AdminLayout>
          <div className="flex flex-col items-center justify-center min-h-[400px]">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#0B3D0B]" />
            <p className="mt-4 text-xs font-bold text-gray-500 uppercase tracking-widest">
              Loading plan details...
            </p>
          </div>
        </AdminLayout>
      }
    >
      <ChituPlanDetailInner params={params} />
    </Suspense>
  );
}

function ChituPlanDetailInner({ params }: PlanDetailPageProps) {
  const router = useRouter();
  const resolvedParams = use(params);
  const planId = resolvedParams.id;

  // Plan detail state
  const [plan, setPlan] = useState<ChituPlanDetailDto | null>(null);
  const [loadingPlan, setLoadingPlan] = useState<boolean>(true);
  const [planError, setPlanError] = useState<string | null>(null);

  // Roster state
  const [roster, setRoster] = useState<ChituRosterResponse | null>(null);
  const [loadingRoster, setLoadingRoster] = useState<boolean>(true);
  const [rosterError, setRosterError] = useState<string | null>(null);

  // Filter state
  const [selectedRound, setSelectedRound] = useState<number>(1);
  const [search, setSearch] = useState<string>("");
  const [paymentFilter, setPaymentFilter] = useState<"All" | "Paid" | "Unpaid">("All");
  const [statusFilter, setStatusFilter] = useState<"All" | "Active" | "Won" | "Closed">("All");
  const [page, setPage] = useState<number>(1);
  const pageSize = 20;

  // Load plan details
  const loadPlanDetail = useCallback(async () => {
    setLoadingPlan(true);
    setPlanError(null);
    try {
      const data = await adminApi.getChituPlanById(planId);
      setPlan(data);
      // Default to current round or overdue round
      const defaultRound = data.overdueRaffleRoundIndex || data.currentRoundIndex || 1;
      setSelectedRound(defaultRound);
    } catch (err: any) {
      setPlanError(err?.message || "Failed to load plan details.");
    } finally {
      setLoadingPlan(false);
    }
  }, [planId]);

  useEffect(() => {
    loadPlanDetail();
  }, [loadPlanDetail]);

  // Load roster
  const loadRoster = useCallback(async () => {
    if (!planId) return;
    setLoadingRoster(true);
    setRosterError(null);
    try {
      const data = await adminApi.getChituRoster(planId, {
        roundIndex: selectedRound,
        search: search.trim() || undefined,
        paymentFilter: paymentFilter === "All" ? undefined : paymentFilter,
        statusFilter: statusFilter === "All" ? undefined : statusFilter,
        page,
        pageSize,
      });
      setRoster(data);
    } catch (err: any) {
      setRosterError(err?.message || "Failed to load customer/slot roster.");
    } finally {
      setLoadingRoster(false);
    }
  }, [planId, selectedRound, search, paymentFilter, statusFilter, page]);

  useEffect(() => {
    loadRoster();
  }, [loadRoster]);

  const totalPages = roster ? Math.max(1, Math.ceil(roster.totalCount / pageSize)) : 1;
  const isGold = plan?.redemptionType === ChituRedemptionType.Gold || plan?.redemptionTypeLabel === "Gold";
  const isPlanActive = plan?.statusLabel === "Active" || (plan?.status as unknown) === 2 || plan?.status === "Active" || plan?.status === "2";

  return (
    <AdminLayout title={plan ? `Chitu Plan: ${plan.name}` : "Chitu Plan Roster"}>
      <div className="space-y-6 max-w-7xl mx-auto pb-16">
        {/* Top Back Navigation */}
        <div className="flex items-center justify-between">
          <Link
            href="/chitu-plans"
            className="text-xs font-bold text-gray-500 hover:text-gray-900 flex items-center gap-1.5 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            Back to Chitu Plans
          </Link>
        </div>

        {/* Plan Header Card */}
        {loadingPlan && !plan ? (
          <div className="p-8 text-center text-gray-400 bg-white rounded-2xl border border-gray-100 flex items-center justify-center gap-2">
            <RefreshCw className="w-5 h-5 animate-spin text-[#0B3D0B]" />
            <span className="text-xs font-bold uppercase tracking-wider">Loading plan summary...</span>
          </div>
        ) : plan ? (
          <div className="bg-white rounded-2xl border border-gray-200 shadow-xs overflow-hidden">
            <div className="p-6 bg-[#FAF9F6] border-b border-gray-100 flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-mono text-xs font-black px-2.5 py-0.5 rounded-md bg-[#0B3D0B] text-[#FFD700]">
                    {plan.code}
                  </span>
                  <span
                    className={`text-xs font-black uppercase tracking-wider px-2.5 py-0.5 rounded-full ${
                      isGold
                        ? "bg-amber-100 text-amber-900 border border-amber-300"
                        : "bg-emerald-100 text-emerald-900 border border-emerald-300"
                    }`}
                  >
                    {isGold ? "🪙 Gold Plan" : "💵 Cash Plan"}
                  </span>
                  <span
                    className={`text-xs font-bold px-2.5 py-0.5 rounded-full ${
                      isPlanActive
                        ? "bg-emerald-100 text-emerald-800"
                        : "bg-gray-100 text-gray-600"
                    }`}
                  >
                    {plan.statusLabel || (isPlanActive ? "Active" : "Inactive")}
                  </span>
                </div>
                <h1 className="text-2xl font-black text-gray-900 mt-2 tracking-tight">
                  {plan.name}
                </h1>
                <p className="text-xs text-gray-500 font-medium mt-0.5">
                  Branch: <strong>{plan.branchName}</strong> | First Raffle: <span className="font-mono font-bold text-gray-700">{plan.startDate}</span> | Final Raffle: <span className="font-mono font-bold text-gray-700">{plan.endDate}</span>
                </p>
              </div>

              {/* Header KPI Stats */}
              <div className="flex items-center gap-4">
                <div className="p-3 bg-white rounded-xl border border-gray-200 text-center min-w-[100px]">
                  <span className="text-[10px] font-bold text-gray-400 uppercase tracking-wider block">Monthly / Slot</span>
                  <span className="text-sm font-black text-[#0B3D0B]">
                    LKR {plan.monthlyInstalmentAmount.toLocaleString()}
                  </span>
                </div>
                <div className="p-3 bg-white rounded-xl border border-gray-200 text-center min-w-[100px]">
                  <span className="text-[10px] font-bold text-gray-400 uppercase tracking-wider block">Duration</span>
                  <span className="text-sm font-black text-gray-900">
                    {plan.durationMonths} Months
                  </span>
                </div>
                <div className="p-3 bg-white rounded-xl border border-gray-200 text-center min-w-[110px]">
                  <span className="text-[10px] font-bold text-gray-400 uppercase tracking-wider block">Winner Entitlement</span>
                  <span className="text-sm font-black text-amber-900">
                    LKR {plan.chituBenefitValue.toLocaleString()}
                  </span>
                </div>
              </div>
            </div>

            {/* Capacity & Winner Bar */}
            <div className="px-6 py-4 grid grid-cols-2 sm:grid-cols-5 gap-4 text-xs border-b border-gray-100">
              <div>
                <span className="text-gray-400 font-bold block">Capacity</span>
                <span className="font-bold text-gray-900 text-sm">{plan.totalSlotCapacity} Slots</span>
              </div>
              <div>
                <span className="text-gray-400 font-bold block">Assigned Slots</span>
                <span className="font-bold text-emerald-800 text-sm">{plan.assignedSlotsCount} Slots</span>
              </div>
              <div>
                <span className="text-gray-400 font-bold block">Available Capacity</span>
                <span className="font-bold text-gray-600 text-sm">{plan.availableSlotsCount} Slots</span>
              </div>
              <div>
                <span className="text-gray-400 font-bold block">Recorded Winners</span>
                <span className="font-bold text-amber-700 text-sm">{plan.winningSlotsCount} Slots</span>
              </div>
              <div>
                <span className="text-gray-400 font-bold block">Closed Slots</span>
                <span className="font-bold text-gray-500 text-sm">{plan.closedSlotsCount} Slots</span>
              </div>
            </div>

            {/* Overdue Warning */}
            {plan.hasOverdueRaffle && (
              <div className="px-6 py-2.5 bg-amber-50 border-b border-amber-200 text-amber-900 text-xs flex items-center justify-between">
                <span className="font-bold flex items-center gap-1.5">
                  <AlertTriangle className="w-4 h-4 text-amber-600" />
                  Raffle Round #{plan.overdueRaffleRoundIndex} is overdue and awaiting physical draw recording.
                </span>
                <Link
                  href="/chitu-plans"
                  className="px-2.5 py-1 bg-[#0B3D0B] text-[#FFD700] rounded-lg text-[10px] font-bold uppercase tracking-wider"
                >
                  Go to Record Draw
                </Link>
              </div>
            )}
          </div>
        ) : null}

        {/* Selected Round Summary & Roster Filter Bar */}
        <div className="bg-white rounded-2xl border border-gray-200 p-5 shadow-xs space-y-4">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            {/* Round Selector */}
            <div className="flex items-center gap-3">
              <span className="text-xs font-bold text-gray-700 uppercase tracking-wider flex items-center gap-1.5">
                <Clock className="w-4 h-4 text-[#0B3D0B]" />
                Selected Round:
              </span>
              <select
                value={selectedRound}
                onChange={(e) => {
                  setSelectedRound(Number(e.target.value));
                  setPage(1);
                }}
                className="px-3.5 py-1.5 rounded-xl border border-gray-300 bg-white text-xs font-bold font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              >
                {Array.from({ length: plan?.durationMonths || 12 }, (_, i) => i + 1).map((r) => (
                  <option key={r} value={r}>
                    Round {r} of {plan?.durationMonths || 12}
                  </option>
                ))}
              </select>
            </div>

            {/* Round Paid / Unpaid Slot Counts */}
            {roster && (
              <div className="flex items-center gap-3 text-xs">
                <div className="px-3 py-1.5 rounded-xl bg-emerald-50 border border-emerald-200 flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 text-emerald-700" />
                  <span className="text-emerald-800 font-semibold">Round {selectedRound} Paid:</span>
                  <span className="font-black text-emerald-950 text-sm font-mono">
                    {roster.roundPaidSlotsCount} Slots
                  </span>
                </div>

                <div className="px-3 py-1.5 rounded-xl bg-red-50 border border-red-200 flex items-center gap-2">
                  <XCircle className="w-4 h-4 text-red-700" />
                  <span className="text-red-800 font-semibold">Round {selectedRound} Unpaid:</span>
                  <span className="font-black text-red-950 text-sm font-mono">
                    {roster.roundUnpaidSlotsCount} Slots
                  </span>
                </div>
              </div>
            )}
          </div>

          {/* Search & Filter Row */}
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 pt-2 border-t border-gray-100">
            {/* Search */}
            <div className="relative flex-1">
              <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                placeholder="Search slot code (e.g. GS2026-001), customer name or phone..."
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
                className="w-full pl-10 pr-4 py-2 bg-gray-50 rounded-xl border border-gray-200 text-xs text-gray-800 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>

            {/* Payment Filter */}
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold text-gray-500 uppercase tracking-wider">Payment:</span>
              <div className="flex items-center gap-1 bg-gray-100 p-1 rounded-xl">
                {(["All", "Paid", "Unpaid"] as const).map((pf) => (
                  <button
                    key={pf}
                    type="button"
                    onClick={() => {
                      setPaymentFilter(pf);
                      setPage(1);
                    }}
                    className={`px-3 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                      paymentFilter === pf
                        ? "bg-[#0B3D0B] text-[#FFD700] font-bold"
                        : "text-gray-600 hover:text-gray-900"
                    }`}
                  >
                    {pf}
                  </button>
                ))}
              </div>
            </div>

            {/* Status Filter */}
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold text-gray-500 uppercase tracking-wider">Status:</span>
              <div className="flex items-center gap-1 bg-gray-100 p-1 rounded-xl">
                {(["All", "Active", "Won", "Closed"] as const).map((sf) => (
                  <button
                    key={sf}
                    type="button"
                    onClick={() => {
                      setStatusFilter(sf);
                      setPage(1);
                    }}
                    className={`px-3 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                      statusFilter === sf
                        ? "bg-[#0B3D0B] text-[#FFD700] font-bold"
                        : "text-gray-600 hover:text-gray-900"
                    }`}
                  >
                    {sf}
                  </button>
                ))}
              </div>
            </div>

            <button
              type="button"
              onClick={loadRoster}
              className="p-2 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl text-gray-600 cursor-pointer"
              title="Refresh roster"
            >
              <RefreshCw className={`w-4 h-4 ${loadingRoster ? "animate-spin" : ""}`} />
            </button>
          </div>
        </div>

        {/* Error Alert */}
        {rosterError && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <span>{rosterError}</span>
            </div>
            <button
              onClick={loadRoster}
              className="px-3 py-1 bg-red-100 hover:bg-red-200 font-bold rounded-lg transition-colors cursor-pointer"
            >
              Retry
            </button>
          </div>
        )}

        {/* Roster Table */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-xs overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-[#FAF9F6] border-b border-gray-200 text-gray-500 font-bold uppercase tracking-wider">
                <tr>
                  <th className="px-5 py-3.5">Slot Code</th>
                  <th className="px-5 py-3.5">Customer</th>
                  <th className="px-5 py-3.5">Round {selectedRound} Status</th>
                  <th className="px-5 py-3.5">Months Paid</th>
                  <th className="px-5 py-3.5">Outstanding Due</th>
                  <th className="px-5 py-3.5">Total Confirmed Paid</th>
                  <th className="px-5 py-3.5">Winner State</th>
                  <th className="px-5 py-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {loadingRoster && (!roster || roster.rows.length === 0) ? (
                  <tr>
                    <td colSpan={8} className="p-12 text-center text-gray-400">
                      <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-[#0B3D0B]" />
                      <span>Loading slot roster...</span>
                    </td>
                  </tr>
                ) : !roster || roster.rows.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="p-12 text-center text-gray-400">
                      No slots found matching the selected filters.
                    </td>
                  </tr>
                ) : (
                  roster.rows.map((row) => (
                    <tr key={row.slotId} className="hover:bg-gray-50/70 transition-colors">
                      {/* Slot Display Code */}
                      <td className="px-5 py-3.5 font-mono font-black text-gray-900">
                        <span className="px-2.5 py-1 rounded-md bg-[#0B3D0B] text-[#FFD700]">
                          {row.slotDisplayCode}
                        </span>
                      </td>

                      {/* Customer Info (Links to shared customer profile) */}
                      <td className="px-5 py-3.5">
                        <Link
                          href={`/customers/${row.customerId}`}
                          className="font-bold text-gray-900 hover:text-[#0B3D0B] hover:underline flex items-center gap-1 group"
                        >
                          <span>{row.customerName}</span>
                          <ExternalLink className="w-3 h-3 text-gray-400 group-hover:text-[#0B3D0B]" />
                        </Link>
                        <span className="text-[11px] text-gray-400 block font-mono">
                          {row.customerPhone || "No phone"}
                        </span>
                      </td>

                      {/* Round Payment Coverage */}
                      <td className="px-5 py-3.5">
                        {row.isPaidForSelectedRound ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-100 text-emerald-800">
                            <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                            Paid Round {selectedRound}
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-red-100 text-red-800">
                            <XCircle className="w-3.5 h-3.5 text-red-600" />
                            Unpaid for Round {selectedRound}
                          </span>
                        )}
                      </td>

                      {/* Months Paid & Paid Through */}
                      <td className="px-5 py-3.5">
                        <span className="font-bold text-gray-900">
                          {row.instalmentsPaidCount} of {roster.durationMonths}
                        </span>
                        {row.paidThroughMonthIndex && (
                          <span className="text-[10px] text-gray-400 block font-mono">
                            Paid through Month {row.paidThroughMonthIndex}
                          </span>
                        )}
                      </td>

                      {/* Outstanding Due */}
                      <td className="px-5 py-3.5">
                        {row.outstandingDueMonthsCount > 0 ? (
                          <span className="font-bold text-red-700">
                            {row.outstandingDueMonthsCount} month(s) arrears
                          </span>
                        ) : (
                          <span className="text-gray-400 font-medium">None</span>
                        )}
                      </td>

                      {/* Total Paid */}
                      <td className="px-5 py-3.5 font-mono font-bold text-emerald-900">
                        LKR {row.totalConfirmedPaid.toLocaleString()}
                      </td>

                      {/* Winner State */}
                      <td className="px-5 py-3.5">
                        {row.isWinner ? (
                          <div className="space-y-0.5">
                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-black bg-amber-100 text-amber-900 border border-amber-300">
                              <Trophy className="w-3 h-3 text-amber-600" />
                              Won in Round {row.winningRoundIndex}
                            </span>
                            {row.advanceExcessAmount && row.advanceExcessAmount > 0 ? (
                              <span className="text-[10px] text-amber-800 block font-bold">
                                Excess Prepaid: LKR {row.advanceExcessAmount.toLocaleString()} (Pending Decision)
                              </span>
                            ) : null}
                          </div>
                        ) : (
                          <span className="text-gray-400 text-xs font-medium">Active Participant</span>
                        )}
                      </td>

                      {/* Actions */}
                      <td className="px-5 py-3.5 text-right">
                        <Link
                          href={`/customers/${row.customerId}`}
                          className="px-2.5 py-1 rounded-lg bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold text-[11px] inline-flex items-center gap-1"
                        >
                          Customer Details
                        </Link>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Footer */}
          {roster && roster.totalCount > pageSize && (
            <div className="p-4 bg-[#FAF9F6] border-t border-gray-100 flex items-center justify-between text-xs text-gray-500">
              <span>
                Showing {(page - 1) * pageSize + 1} to{" "}
                {Math.min(page * pageSize, roster.totalCount)} of {roster.totalCount} slots
              </span>
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="px-3 py-1 rounded-lg bg-white border border-gray-300 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed font-semibold cursor-pointer"
                >
                  Previous
                </button>
                <span className="font-mono font-bold text-gray-800">
                  {page} / {totalPages}
                </span>
                <button
                  type="button"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  className="px-3 py-1 rounded-lg bg-white border border-gray-300 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed font-semibold cursor-pointer"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </AdminLayout>
  );
}
