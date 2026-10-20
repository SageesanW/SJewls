"use client";

import React, { useEffect, useState, useMemo, useCallback } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, ApiError } from "../../lib/api";
import { useAuth } from "../../lib/auth-context";
import {
  ChituPlanCardDto,
  ChituPlanListResponse,
  ChituRedemptionType,
  CreateChituPlanRequest,
  UpdateChituPlanRequest,
  ChituSlotAssignmentPreviewResponse,
  AssignChituSlotsRequest,
  CustomerPayableSlotsResponse,
  ChituPaymentPreviewResponse,
  RecordChituPaymentRequest,
  ChituEligibleSlotsResponse,
  RecordChituRaffleRequest,
  ChituSlotAllocationInputDto,
} from "../../types/chitu";
import { Branch } from "../../types/auth";
import { CustomerListItem } from "../../types/customer";
import {
  Coins,
  Plus,
  Search,
  Filter,
  RefreshCw,
  Building2,
  Calendar,
  CheckCircle2,
  AlertCircle,
  AlertTriangle,
  Clock,
  Trophy,
  Users,
  CreditCard,
  UserPlus,
  X,
  ArrowRight,
  ShieldAlert,
  ChevronRight,
  Check,
  Award,
  Sparkles,
} from "lucide-react";

export default function ChituPlansPage() {
  const router = useRouter();
  const { user, isSuperAdmin } = useAuth();

  // State
  const [plans, setPlans] = useState<ChituPlanCardDto[]>([]);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);

  // Filters
  const [activeTab, setActiveTab] = useState<"All" | "Active" | "Inactive">("All");
  const [typeFilter, setTypeFilter] = useState<"All" | "Cash" | "Gold">("All");
  const [selectedBranchId, setSelectedBranchId] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState<string>("");

  // Counts
  const [totalCount, setTotalCount] = useState<number>(0);
  const [activeCount, setActiveCount] = useState<number>(0);
  const [inactiveCount, setInactiveCount] = useState<number>(0);

  // Modals state
  const [showAddModal, setShowAddModal] = useState<boolean>(false);
  const [showEditModal, setShowEditModal] = useState<boolean>(false);
  const [selectedPlanForEdit, setSelectedPlanForEdit] = useState<ChituPlanCardDto | null>(null);

  const [showAssignModal, setShowAssignModal] = useState<boolean>(false);
  const [selectedPlanForAssign, setSelectedPlanForAssign] = useState<ChituPlanCardDto | null>(null);

  const [showPaymentModal, setShowPaymentModal] = useState<boolean>(false);
  const [selectedPlanForPayment, setSelectedPlanForPayment] = useState<ChituPlanCardDto | null>(null);

  const [showRaffleModal, setShowRaffleModal] = useState<boolean>(false);
  const [selectedPlanForRaffle, setSelectedPlanForRaffle] = useState<ChituPlanCardDto | null>(null);

  // Load branches
  useEffect(() => {
    async function loadBranches() {
      try {
        const branchList = await adminApi.getBranches();
        setBranches(branchList);
      } catch (err) {
        console.error("Failed to load branches", err);
      }
    }
    loadBranches();
  }, []);

  // Load plans
  const loadPlans = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const redemptionTypeVal =
        typeFilter === "Cash"
          ? ChituRedemptionType.Cash
          : typeFilter === "Gold"
          ? ChituRedemptionType.Gold
          : undefined;

      const res = await adminApi.getChituPlans({
        branchId: selectedBranchId || undefined,
        search: searchQuery.trim() || undefined,
        status: activeTab === "All" ? undefined : activeTab,
        redemptionType: redemptionTypeVal,
      });

      setPlans(res.items || []);
      setTotalCount(res.totalCount || 0);
      setActiveCount(res.activeCount || 0);
      setInactiveCount(res.inactiveCount || 0);
    } catch (err: any) {
      setError(err?.message || "Failed to load Chitu plans.");
    } finally {
      setLoading(false);
    }
  }, [selectedBranchId, searchQuery, activeTab, typeFilter]);

  useEffect(() => {
    loadPlans();
  }, [loadPlans]);

  return (
    <AdminLayout title="Chitu Plans">
      <div className="space-y-6 max-w-7xl mx-auto pb-16">
        {/* Header Section */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl border border-gray-100 shadow-xs">
          <div>
            <div className="flex items-center gap-2.5">
              <div className="w-10 h-10 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center shadow-xs">
                <Coins className="w-5 h-5" />
              </div>
              <div>
                <h1 className="text-2xl font-black text-gray-900 tracking-tight">Chitu Savings Plans</h1>
                <p className="text-xs text-gray-500 font-medium mt-0.5">
                  Manage fixed-round monthly raffles, sequential customer slots, payments, and physical draws
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={() => setShowAddModal(true)}
              className="px-4 py-2.5 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-2 shadow-sm transition-all cursor-pointer"
            >
              <Plus className="w-4 h-4 stroke-[3]" />
              Add Chitu Plan
            </button>
          </div>
        </div>

        {/* Success Toast */}
        {successToast && (
          <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-900 text-sm flex items-center justify-between shadow-xs">
            <div className="flex items-center gap-2.5">
              <CheckCircle2 className="w-5 h-5 text-emerald-700 flex-shrink-0" />
              <span className="font-semibold">{successToast}</span>
            </div>
            <button
              onClick={() => setSuccessToast(null)}
              className="text-emerald-700 hover:text-emerald-900 text-xs font-bold uppercase tracking-wider cursor-pointer"
            >
              Dismiss
            </button>
          </div>
        )}

        {/* Error Alert */}
        {error && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
            <button
              onClick={loadPlans}
              className="px-3 py-1 bg-red-100 hover:bg-red-200 font-bold rounded-lg transition-colors cursor-pointer"
            >
              Retry
            </button>
          </div>
        )}

        {/* Filter Bar */}
        <div className="bg-[#FAF9F6] p-4 rounded-2xl border border-gray-200 space-y-3">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
            {/* Tabs: All / Active / Inactive */}
            <div className="flex items-center gap-1.5 bg-gray-200/70 p-1 rounded-xl">
              <button
                type="button"
                onClick={() => setActiveTab("All")}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                  activeTab === "All"
                    ? "bg-[#0B3D0B] text-[#FFD700] shadow-xs"
                    : "text-gray-600 hover:text-gray-900"
                }`}
              >
                All ({totalCount})
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("Active")}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                  activeTab === "Active"
                    ? "bg-[#0B3D0B] text-[#FFD700] shadow-xs"
                    : "text-gray-600 hover:text-gray-900"
                }`}
              >
                Active ({activeCount})
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("Inactive")}
                className={`px-3.5 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer ${
                  activeTab === "Inactive"
                    ? "bg-[#0B3D0B] text-[#FFD700] shadow-xs"
                    : "text-gray-600 hover:text-gray-900"
                }`}
              >
                Inactive ({inactiveCount})
              </button>
            </div>

            {/* Type selector: All / Cash / Gold */}
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold text-gray-500 uppercase tracking-wider">Type:</span>
              <div className="flex items-center gap-1 bg-white p-1 rounded-xl border border-gray-200">
                {(["All", "Cash", "Gold"] as const).map((t) => (
                  <button
                    key={t}
                    type="button"
                    onClick={() => setTypeFilter(t)}
                    className={`px-3 py-1 rounded-lg text-xs font-semibold transition-colors cursor-pointer ${
                      typeFilter === t
                        ? "bg-[#0B3D0B] text-[#FFD700] font-bold"
                        : "text-gray-600 hover:text-gray-900"
                    }`}
                  >
                    {t}
                  </button>
                ))}
              </div>
            </div>

            {/* Branch selector (if multiple branches) */}
            {branches.length > 1 && (
              <div className="flex items-center gap-2">
                <Building2 className="w-4 h-4 text-gray-400" />
                <select
                  value={selectedBranchId}
                  onChange={(e) => setSelectedBranchId(e.target.value)}
                  className="px-3 py-1.5 rounded-xl border border-gray-300 bg-white text-xs font-semibold text-gray-800 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                >
                  <option value="">All Branches</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name} ({b.code})
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Search bar */}
          <div className="flex items-center gap-2 pt-1">
            <div className="relative flex-1">
              <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                placeholder="Search plans by name or plan code..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-10 pr-4 py-2 bg-white rounded-xl border border-gray-300 text-xs text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
            <button
              type="button"
              onClick={loadPlans}
              className="p-2 bg-white border border-gray-300 hover:bg-gray-50 rounded-xl text-gray-600 transition-colors cursor-pointer"
              title="Refresh plans"
            >
              <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin" : ""}`} />
            </button>
          </div>
        </div>

        {/* Plans Grid */}
        {loading && plans.length === 0 ? (
          <div className="p-16 text-center text-gray-400 bg-white rounded-2xl border border-gray-100 flex flex-col items-center justify-center gap-2">
            <RefreshCw className="w-8 h-8 animate-spin text-[#0B3D0B]" />
            <span className="text-xs font-bold uppercase tracking-wider text-gray-500">Loading Chitu plans...</span>
          </div>
        ) : plans.length === 0 ? (
          <div className="p-16 text-center text-gray-500 bg-white rounded-2xl border border-dashed border-gray-300 flex flex-col items-center justify-center gap-3">
            <div className="w-14 h-14 rounded-2xl bg-amber-50 text-amber-700 flex items-center justify-center">
              <Coins className="w-7 h-7" />
            </div>
            <h3 className="text-base font-bold text-gray-900">No Chitu Plans Found</h3>
            <p className="text-xs text-gray-500 max-w-md">
              No Chitu plans match your search or filter criteria. Create a new plan to get started.
            </p>
            <button
              onClick={() => setShowAddModal(true)}
              className="mt-2 px-4 py-2 rounded-xl bg-[#0B3D0B] text-[#FFD700] text-xs font-bold uppercase tracking-wider"
            >
              Add First Chitu Plan
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {plans.map((plan) => (
              <ChituPlanCard
                key={plan.id}
                plan={plan}
                onEdit={() => {
                  setSelectedPlanForEdit(plan);
                  setShowEditModal(true);
                }}
                onAssign={() => {
                  setSelectedPlanForAssign(plan);
                  setShowAssignModal(true);
                }}
                onPayment={() => {
                  setSelectedPlanForPayment(plan);
                  setShowPaymentModal(true);
                }}
                onRaffle={() => {
                  setSelectedPlanForRaffle(plan);
                  setShowRaffleModal(true);
                }}
              />
            ))}
          </div>
        )}
      </div>

      {/* Modals */}
      {showAddModal && (
        <AddChituPlanModal
          branches={branches}
          onClose={() => setShowAddModal(false)}
          onSuccess={(newPlan) => {
            setShowAddModal(false);
            setSuccessToast(`Chitu Plan '${newPlan.name}' (${newPlan.code}) created successfully.`);
            loadPlans();
          }}
        />
      )}

      {showEditModal && selectedPlanForEdit && (
        <EditChituPlanModal
          plan={selectedPlanForEdit}
          branches={branches}
          onClose={() => {
            setShowEditModal(false);
            setSelectedPlanForEdit(null);
          }}
          onSuccess={(updated) => {
            setShowEditModal(false);
            setSelectedPlanForEdit(null);
            setSuccessToast(`Plan '${updated.name}' updated successfully.`);
            loadPlans();
          }}
        />
      )}

      {showAssignModal && selectedPlanForAssign && (
        <AssignChituSlotsModal
          plan={selectedPlanForAssign}
          onClose={() => {
            setShowAssignModal(false);
            setSelectedPlanForAssign(null);
          }}
          onSuccess={(msg) => {
            setShowAssignModal(false);
            setSelectedPlanForAssign(null);
            setSuccessToast(msg);
            loadPlans();
          }}
        />
      )}

      {showPaymentModal && selectedPlanForPayment && (
        <RecordChituPaymentModal
          plan={selectedPlanForPayment}
          onClose={() => {
            setShowPaymentModal(false);
            setSelectedPlanForPayment(null);
          }}
          onSuccess={(msg) => {
            setShowPaymentModal(false);
            setSelectedPlanForPayment(null);
            setSuccessToast(msg);
            loadPlans();
          }}
        />
      )}

      {showRaffleModal && selectedPlanForRaffle && (
        <RecordChituRaffleModal
          plan={selectedPlanForRaffle}
          onClose={() => {
            setShowRaffleModal(false);
            setSelectedPlanForRaffle(null);
          }}
          onSuccess={(msg) => {
            setShowRaffleModal(false);
            setSelectedPlanForRaffle(null);
            setSuccessToast(msg);
            loadPlans();
          }}
        />
      )}
    </AdminLayout>
  );
}

// ==================== PLAN CARD COMPONENT ====================
function ChituPlanCard({
  plan,
  onEdit,
  onAssign,
  onPayment,
  onRaffle,
}: {
  plan: ChituPlanCardDto;
  onEdit: () => void;
  onAssign: () => void;
  onPayment: () => void;
  onRaffle: () => void;
}) {
  const isGold = plan.redemptionType === ChituRedemptionType.Gold || plan.redemptionTypeLabel === "Gold";
  const isPlanActive = plan.statusLabel === "Active" || (plan.status as unknown) === 2 || plan.status === "Active" || plan.status === "2";
  const isUpcoming = plan.isUpcoming;
  const percentAssigned = Math.min(100, Math.round((plan.assignedSlotsCount / Math.max(1, plan.totalSlotCapacity)) * 100));

  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-xs hover:shadow-md transition-all flex flex-col justify-between overflow-hidden">
      {/* Top Header */}
      <div>
        <div className="p-5 border-b border-gray-100 bg-[#FAF9F6]">
          <div className="flex items-start justify-between gap-2">
            <div>
              <div className="flex items-center gap-2">
                <span className="font-mono text-xs font-black px-2 py-0.5 rounded-md bg-[#0B3D0B] text-[#FFD700]">
                  {plan.code}
                </span>
                <span
                  className={`text-[10px] font-black uppercase tracking-wider px-2 py-0.5 rounded-full ${
                    isGold
                      ? "bg-amber-100 text-amber-900 border border-amber-300"
                      : "bg-emerald-100 text-emerald-900 border border-emerald-300"
                  }`}
                >
                  {isGold ? "🪙 Gold Plan" : "💵 Cash Plan"}
                </span>
              </div>
              <h3 className="text-base font-black text-gray-900 mt-1.5 tracking-tight line-clamp-1">
                {plan.name}
              </h3>
              <p className="text-[11px] text-gray-500 font-medium">
                Branch: {plan.branchName}
              </p>
            </div>

            <div className="flex flex-col items-end gap-1">
              <span
                className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                  isPlanActive
                    ? "bg-emerald-100 text-emerald-800"
                    : "bg-gray-100 text-gray-600"
                }`}
              >
                {plan.statusLabel || (isPlanActive ? "Active" : "Inactive")}
              </span>
              <button
                type="button"
                onClick={onEdit}
                className="text-[10px] text-gray-400 hover:text-gray-700 underline font-semibold cursor-pointer"
              >
                Edit
              </button>
            </div>
          </div>
        </div>

        {/* Card Body Metrics */}
        <div className="p-5 space-y-4">
          {/* Overdue Raffle Warning Banner */}
          {plan.hasOverdueRaffle && (
            <div className="p-2.5 rounded-xl bg-amber-50 border border-amber-300 text-amber-900 text-xs flex items-center justify-between animate-pulse">
              <div className="flex items-center gap-1.5 font-bold">
                <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0" />
                <span>Raffle #{plan.overdueRaffleRoundIndex} Overdue</span>
              </div>
              <button
                type="button"
                onClick={onRaffle}
                className="px-2 py-1 bg-[#0B3D0B] text-[#FFD700] rounded-md text-[10px] font-bold uppercase tracking-wider cursor-pointer"
              >
                Record Draw
              </button>
            </div>
          )}

          {/* Pricing & Duration */}
          <div className="grid grid-cols-2 gap-3 p-3 rounded-xl bg-gray-50 border border-gray-100">
            <div>
              <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block">
                Monthly / Slot
              </span>
              <span className="text-sm font-black text-[#0B3D0B]">
                LKR {plan.monthlyInstalmentAmount.toLocaleString()}
              </span>
              <span className="text-[10px] text-gray-500 block">
                {plan.durationMonths} rounds
              </span>
            </div>
            <div>
              <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block">
                Winner Entitlement
              </span>
              <span className="text-sm font-black text-amber-900">
                LKR {plan.chituBenefitValue.toLocaleString()}
              </span>
              <span className="text-[10px] text-gray-500 block">
                Fixed value
              </span>
            </div>
          </div>

          {/* Capacity Progress */}
          <div>
            <div className="flex items-center justify-between text-xs mb-1">
              <span className="text-gray-500 font-semibold">Assigned Slots</span>
              <span className="font-bold text-gray-900">
                {plan.assignedSlotsCount} / {plan.totalSlotCapacity}{" "}
                <span className="text-gray-400 text-[10px]">({plan.availableSlotsCount} left)</span>
              </span>
            </div>
            <div className="w-full bg-gray-100 h-2 rounded-full overflow-hidden">
              <div
                className="bg-[#0B3D0B] h-2 rounded-full transition-all duration-300"
                style={{ width: `${percentAssigned}%` }}
              />
            </div>
          </div>

          {/* Current Period / Round Coverage */}
          <div className="p-3 rounded-xl border border-gray-200 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-gray-700 flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5 text-gray-400" />
                {isUpcoming ? (
                  <span className="text-amber-700">Upcoming (Round 1 Coverage)</span>
                ) : (
                  <span>Round {plan.currentRoundIndex} of {plan.durationMonths}</span>
                )}
              </span>
              <span className="text-[11px] text-gray-500 font-mono">
                Due: {plan.currentRoundDueDate}
              </span>
            </div>

            {/* Per-slot Paid / Unpaid badges */}
            <div className="grid grid-cols-2 gap-2 text-xs">
              <div className="p-2 rounded-lg bg-emerald-50 border border-emerald-200 flex items-center justify-between">
                <span className="text-emerald-800 font-semibold text-[11px]">Paid Slots:</span>
                <span className="font-black text-emerald-900 text-sm">
                  {plan.currentRoundPaidSlotsCount}
                </span>
              </div>
              <div className="p-2 rounded-lg bg-red-50 border border-red-200 flex items-center justify-between">
                <span className="text-red-800 font-semibold text-[11px]">Unpaid Slots:</span>
                <span className="font-black text-red-900 text-sm">
                  {plan.currentRoundUnpaidSlotsCount}
                </span>
              </div>
            </div>

            {/* Winner / Closed stats */}
            {(plan.winningSlotsCount > 0 || plan.closedSlotsCount > 0) && (
              <div className="flex items-center justify-between text-[11px] text-gray-500 pt-1 border-t border-gray-100">
                <span className="flex items-center gap-1 text-amber-800 font-medium">
                  <Trophy className="w-3 h-3 text-amber-600" />
                  {plan.winningSlotsCount} Winner{plan.winningSlotsCount !== 1 ? "s" : ""}
                </span>
                {plan.closedSlotsCount > 0 && (
                  <span>{plan.closedSlotsCount} Closed</span>
                )}
              </div>
            )}
          </div>

          {/* Dates */}
          <div className="flex items-center justify-between text-[11px] text-gray-400">
            <span>First Raffle: <strong className="text-gray-700 font-mono">{plan.startDate}</strong></span>
            <span>Final: <strong className="text-gray-700 font-mono">{plan.endDate}</strong></span>
          </div>
        </div>
      </div>

      {/* Card Action Buttons */}
      <div className="p-4 bg-gray-50 border-t border-gray-100 space-y-2">
        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={onAssign}
            disabled={plan.availableSlotsCount <= 0 || !isPlanActive}
            title={!isPlanActive ? "Plan must be Active to assign slots" : plan.availableSlotsCount <= 0 ? "No available slots" : "Assign customer slots"}
            className="px-3 py-2 rounded-xl bg-white border border-[#0B3D0B] text-[#0B3D0B] hover:bg-emerald-50 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-bold flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
          >
            <UserPlus className="w-3.5 h-3.5" />
            Assign Slots
          </button>

          <button
            type="button"
            onClick={onPayment}
            disabled={plan.assignedSlotsCount === 0 || !isPlanActive}
            title={plan.assignedSlotsCount === 0 ? "No slots assigned yet" : !isPlanActive ? "Plan is not active" : "Make instalment payment"}
            className="px-3 py-2 rounded-xl bg-white border border-[#0B3D0B] text-[#0B3D0B] hover:bg-emerald-50 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-bold flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
          >
            <CreditCard className="w-3.5 h-3.5" />
            Make Payment
          </button>
        </div>

        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={onRaffle}
            className="px-3 py-2 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] text-[#FFD700] text-xs font-bold flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
          >
            <Trophy className="w-3.5 h-3.5 text-[#FFD700]" />
            Record Raffle
          </button>

          <Link
            href={`/chitu-plans/${plan.id}`}
            className="px-3 py-2 rounded-xl bg-gray-200 hover:bg-gray-300 text-gray-800 text-xs font-bold flex items-center justify-center gap-1.5 transition-colors text-center"
          >
            <Users className="w-3.5 h-3.5" />
            View Roster
          </Link>
        </div>
      </div>
    </div>
  );
}

// ==================== ADD PLAN MODAL ====================
function AddChituPlanModal({
  branches,
  onClose,
  onSuccess,
}: {
  branches: Branch[];
  onClose: () => void;
  onSuccess: (plan: ChituPlanCardDto) => void;
}) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [redemptionType, setRedemptionType] = useState<ChituRedemptionType>(ChituRedemptionType.Gold);
  const [durationMonths, setDurationMonths] = useState<number>(12);
  const [startDate, setStartDate] = useState<string>(() => {
    const d = new Date();
    d.setDate(d.getDate() + 1); // Tomorrow
    return d.toISOString().split("T")[0];
  });
  const [monthlyInstalmentAmount, setMonthlyInstalmentAmount] = useState<number>(10000);
  const [totalSlotCapacity, setTotalSlotCapacity] = useState<number>(50);
  const [branchId, setBranchId] = useState<string>(branches[0]?.id || "");
  const [description, setDescription] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Calculated End Date
  const calculatedEndDate = useMemo(() => {
    if (!startDate || durationMonths < 1) return "";
    try {
      const parts = startDate.split("-").map(Number);
      const originalYear = parts[0];
      const originalMonth = parts[1]; // 1-indexed
      const originalDay = parts[2];

      const targetTotalMonths = originalMonth - 1 + (durationMonths - 1);
      const targetYear = originalYear + Math.floor(targetTotalMonths / 12);
      const targetMonth = (targetTotalMonths % 12) + 1;
      const daysInTargetMonth = new Date(targetYear, targetMonth, 0).getDate();
      const clampedDay = Math.min(originalDay, daysInTargetMonth);

      const mm = targetMonth.toString().padStart(2, "0");
      const dd = clampedDay.toString().padStart(2, "0");
      return `${targetYear}-${mm}-${dd}`;
    } catch {
      return "";
    }
  }, [startDate, durationMonths]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!name.trim()) {
      setFormError("Plan Name is required.");
      return;
    }
    if (!code.trim()) {
      setFormError("Plan Code is required.");
      return;
    }
    if (durationMonths < 1) {
      setFormError("Duration must be at least 1 month.");
      return;
    }
    if (monthlyInstalmentAmount <= 0) {
      setFormError("Monthly Slot Price must be greater than 0.");
      return;
    }
    if (totalSlotCapacity <= 0) {
      setFormError("Max Slots must be greater than 0.");
      return;
    }
    if (!branchId) {
      setFormError("Please select a branch.");
      return;
    }

    setSubmitting(true);
    try {
      const created = await adminApi.createChituPlan({
        name: name.trim(),
        code: code.trim().toUpperCase(),
        redemptionType,
        durationMonths,
        startDate,
        monthlyInstalmentAmount,
        totalSlotCapacity,
        branchId,
        description: description.trim() || undefined,
      });
      onSuccess(created);
    } catch (err: any) {
      setFormError(err?.message || "Failed to create plan.");
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-xl w-full shadow-2xl border border-gray-100 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 bg-[#FAF9F6] flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center">
              <Plus className="w-5 h-5 stroke-[3]" />
            </div>
            <div>
              <h2 className="text-base font-black text-gray-900">Create New Chitu Plan</h2>
              <p className="text-[11px] text-gray-500 font-medium">Fixed monthly rounds with physical raffle</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-gray-200 text-gray-400 hover:text-gray-700 transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
          {formError && (
            <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{formError}</span>
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Chitu Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                placeholder="e.g. Gold Sovereign 2026"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Chitu Code <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                placeholder="e.g. GS2026"
                value={code}
                onChange={(e) => setCode(e.target.value.toUpperCase())}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono uppercase focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Redemption Type <span className="text-red-500">*</span>
              </label>
              <select
                value={redemptionType}
                onChange={(e) => setRedemptionType(Number(e.target.value) as ChituRedemptionType)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              >
                <option value={ChituRedemptionType.Gold}>Gold (Redemption in Jewellery value)</option>
                <option value={ChituRedemptionType.Cash}>Cash (Cash only)</option>
              </select>
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Branch <span className="text-red-500">*</span>
              </label>
              <select
                value={branchId}
                onChange={(e) => setBranchId(e.target.value)}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              >
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name} ({b.code})
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Duration (Months) <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min={1}
                max={120}
                value={durationMonths}
                onChange={(e) => setDurationMonths(Math.max(1, parseInt(e.target.value) || 1))}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Monthly Price (LKR) <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min={100}
                step={100}
                value={monthlyInstalmentAmount}
                onChange={(e) => setMonthlyInstalmentAmount(Math.max(100, parseFloat(e.target.value) || 100))}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Max Slots <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min={1}
                max={5000}
                value={totalSlotCapacity}
                onChange={(e) => setTotalSlotCapacity(Math.max(1, parseInt(e.target.value) || 1))}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
          </div>

          {/* Schedule Dates */}
          <div className="p-3.5 rounded-2xl bg-[#FAF9F6] border border-gray-200 space-y-2">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className="text-xs font-bold text-gray-700 block mb-1">
                  First Raffle Date (Start) <span className="text-red-500">*</span>
                </label>
                <input
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  required
                  className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                />
              </div>

              <div>
                <label className="text-xs font-bold text-gray-500 block mb-1">
                  Final Raffle / End Date (Calculated)
                </label>
                <input
                  type="text"
                  value={calculatedEndDate}
                  disabled
                  className="w-full px-3 py-2 rounded-xl border border-gray-200 bg-gray-100 text-xs font-mono font-bold text-gray-700 cursor-not-allowed"
                />
              </div>
            </div>

            <div className="flex items-center justify-between text-xs pt-1 text-gray-600">
              <span>Winning Entitlement:</span>
              <strong className="text-[#0B3D0B] font-mono text-sm">
                LKR {(monthlyInstalmentAmount * durationMonths).toLocaleString()}
              </strong>
            </div>
          </div>

          <div>
            <label className="text-xs font-bold text-gray-700 block mb-1">
              Description (Optional)
            </label>
            <textarea
              rows={2}
              placeholder="Terms and details about this physical raffle plan..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
            />
          </div>

          {/* Footer */}
          <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 transition-colors cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] disabled:opacity-50 text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-2 cursor-pointer shadow-sm"
            >
              {submitting ? "Creating..." : "Create Plan"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ==================== EDIT PLAN MODAL ====================
function EditChituPlanModal({
  plan,
  branches,
  onClose,
  onSuccess,
}: {
  plan: ChituPlanCardDto;
  branches: Branch[];
  onClose: () => void;
  onSuccess: (plan: ChituPlanCardDto) => void;
}) {
  const hasAssignedSlots = (plan.assignedSlotsCount || 0) > 0;

  const [name, setName] = useState(plan.name);
  const [code, setCode] = useState(plan.code);
  const [redemptionType, setRedemptionType] = useState<ChituRedemptionType>(
    plan.redemptionType || (plan.redemptionTypeLabel === "Gold" ? ChituRedemptionType.Gold : ChituRedemptionType.Cash)
  );
  const [branchId, setBranchId] = useState<string>(plan.branchId || branches[0]?.id || "");
  const [durationMonths, setDurationMonths] = useState<number>(plan.durationMonths || 12);
  const [monthlyInstalmentAmount, setMonthlyInstalmentAmount] = useState<number>(plan.monthlyInstalmentAmount || 10000);
  const [totalSlotCapacity, setTotalSlotCapacity] = useState<number>(plan.totalSlotCapacity || 50);
  const [startDate, setStartDate] = useState<string>(plan.startDate || "");
  const isPlanActive = plan.statusLabel === "Active" || (plan.status as unknown) === 2 || plan.status === "Active" || plan.status === "2";
  const [status, setStatus] = useState<string>(isPlanActive ? "Active" : "Inactive");
  const [description, setDescription] = useState(plan.description || "");

  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Calculated End Date
  const calculatedEndDate = useMemo(() => {
    if (!startDate || durationMonths < 1) return plan.endDate || "";
    try {
      const parts = startDate.split("-").map(Number);
      const originalYear = parts[0];
      const originalMonth = parts[1]; // 1-indexed
      const originalDay = parts[2];

      const targetTotalMonths = originalMonth - 1 + (durationMonths - 1);
      const targetYear = originalYear + Math.floor(targetTotalMonths / 12);
      const targetMonth = (targetTotalMonths % 12) + 1;
      const daysInTargetMonth = new Date(targetYear, targetMonth, 0).getDate();
      const clampedDay = Math.min(originalDay, daysInTargetMonth);

      const mm = targetMonth.toString().padStart(2, "0");
      const dd = clampedDay.toString().padStart(2, "0");
      return `${targetYear}-${mm}-${dd}`;
    } catch {
      return plan.endDate || "";
    }
  }, [startDate, durationMonths, plan.endDate]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!name.trim()) {
      setFormError("Plan Name is required.");
      return;
    }
    if (totalSlotCapacity < (plan.assignedSlotsCount || 0)) {
      setFormError(`Capacity cannot be reduced below the assigned slots count (${plan.assignedSlotsCount}).`);
      return;
    }

    setSubmitting(true);
    try {
      const payload: UpdateChituPlanRequest = {
        name: name.trim(),
        description: description.trim(),
        status,
        totalSlotCapacity,
      };

      if (!hasAssignedSlots) {
        payload.code = code.trim().toUpperCase();
        payload.redemptionType = redemptionType;
        payload.durationMonths = durationMonths;
        payload.monthlyInstalmentAmount = monthlyInstalmentAmount;
        payload.startDate = startDate;
        payload.branchId = branchId;
      }

      const updated = await adminApi.updateChituPlan(plan.id, payload);
      onSuccess(updated);
    } catch (err: any) {
      setFormError(err?.message || "Failed to update plan.");
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-xl w-full shadow-2xl border border-gray-100 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 bg-[#FAF9F6] flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center">
              <Coins className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-black text-gray-900">Edit Chitu Plan: {plan.code}</h2>
              <p className="text-[11px] text-gray-500 font-medium">Update plan configuration, capacity, and details</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-gray-200 text-gray-400 hover:text-gray-700 transition-colors cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
          {formError && (
            <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{formError}</span>
            </div>
          )}

          {hasAssignedSlots && (
            <div className="p-3.5 rounded-2xl bg-amber-50 border border-amber-200 text-amber-900 text-xs flex items-start gap-2.5">
              <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5" />
              <div>
                <strong className="font-bold block">Assigned Slots Active ({plan.assignedSlotsCount} slots)</strong>
                <span className="text-[11px] text-amber-800">
                  Core contractual terms (Code, Branch, Redemption Type, Duration, Monthly Price, and First Raffle Date) are locked to maintain agreement integrity with enrolled customers. You can adjust the Plan Name, Status, Description, and expand Total Slot Capacity.
                </span>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Chitu Name <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                placeholder="e.g. Gold Sovereign 2026"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-bold text-gray-700 block">
                  Chitu Code <span className="text-red-500">*</span>
                </label>
                {hasAssignedSlots && (
                  <span className="text-[10px] font-semibold text-gray-400">Locked</span>
                )}
              </div>
              <input
                type="text"
                value={code}
                onChange={(e) => setCode(e.target.value.toUpperCase())}
                disabled={hasAssignedSlots}
                required
                className={`w-full px-3 py-2 rounded-xl border text-xs font-mono uppercase focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                  hasAssignedSlots
                    ? "border-gray-200 bg-gray-100 text-gray-600 cursor-not-allowed"
                    : "border-gray-300"
                }`}
              />
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-bold text-gray-700 block">
                  Redemption Type <span className="text-red-500">*</span>
                </label>
                {hasAssignedSlots && (
                  <span className="text-[10px] font-semibold text-gray-400">Locked</span>
                )}
              </div>
              <select
                value={redemptionType}
                onChange={(e) => setRedemptionType(Number(e.target.value) as ChituRedemptionType)}
                disabled={hasAssignedSlots}
                className={`w-full px-3 py-2 rounded-xl border text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                  hasAssignedSlots
                    ? "border-gray-200 bg-gray-100 text-gray-600 cursor-not-allowed"
                    : "border-gray-300"
                }`}
              >
                <option value={ChituRedemptionType.Gold}>Gold (Redemption in Jewellery value)</option>
                <option value={ChituRedemptionType.Cash}>Cash (Cash only)</option>
              </select>
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-bold text-gray-700 block">
                  Branch <span className="text-red-500">*</span>
                </label>
                {hasAssignedSlots && (
                  <span className="text-[10px] font-semibold text-gray-400">Locked</span>
                )}
              </div>
              <select
                value={branchId}
                onChange={(e) => setBranchId(e.target.value)}
                disabled={hasAssignedSlots}
                required
                className={`w-full px-3 py-2 rounded-xl border text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                  hasAssignedSlots
                    ? "border-gray-200 bg-gray-100 text-gray-600 cursor-not-allowed"
                    : "border-gray-300"
                }`}
              >
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name} ({b.code})
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-bold text-gray-700 block">
                  Duration (Months) <span className="text-red-500">*</span>
                </label>
                {hasAssignedSlots && (
                  <span className="text-[10px] font-semibold text-gray-400">Locked</span>
                )}
              </div>
              <input
                type="number"
                min={1}
                max={120}
                value={durationMonths}
                onChange={(e) => setDurationMonths(Math.max(1, parseInt(e.target.value) || 1))}
                disabled={hasAssignedSlots}
                required
                className={`w-full px-3 py-2 rounded-xl border text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                  hasAssignedSlots
                    ? "border-gray-200 bg-gray-100 text-gray-600 cursor-not-allowed"
                    : "border-gray-300"
                }`}
              />
            </div>

            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="text-xs font-bold text-gray-700 block">
                  Monthly Price (LKR) <span className="text-red-500">*</span>
                </label>
                {hasAssignedSlots && (
                  <span className="text-[10px] font-semibold text-gray-400">Locked</span>
                )}
              </div>
              <input
                type="number"
                min={100}
                step={100}
                value={monthlyInstalmentAmount}
                onChange={(e) => setMonthlyInstalmentAmount(Math.max(100, parseFloat(e.target.value) || 100))}
                disabled={hasAssignedSlots}
                required
                className={`w-full px-3 py-2 rounded-xl border text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                  hasAssignedSlots
                    ? "border-gray-200 bg-gray-100 text-gray-600 cursor-not-allowed"
                    : "border-gray-300"
                }`}
              />
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Max Slots <span className="text-red-500">*</span>
              </label>
              <input
                type="number"
                min={Math.max(1, plan.assignedSlotsCount || 0)}
                max={5000}
                value={totalSlotCapacity}
                onChange={(e) => setTotalSlotCapacity(Math.max(Math.max(1, plan.assignedSlotsCount || 0), parseInt(e.target.value) || 1))}
                required
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
              <span className="text-[10px] text-gray-400 block mt-0.5">
                {plan.assignedSlotsCount} slots already assigned
              </span>
            </div>
          </div>

          {/* Schedule Dates */}
          <div className="p-3.5 rounded-2xl bg-[#FAF9F6] border border-gray-200 space-y-2">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <div className="flex items-center justify-between mb-1">
                  <label className="text-xs font-bold text-gray-700 block">
                    First Raffle Date (Start) <span className="text-red-500">*</span>
                  </label>
                  {hasAssignedSlots && (
                    <span className="text-[10px] font-semibold text-gray-400">Locked</span>
                  )}
                </div>
                <input
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                  disabled={hasAssignedSlots}
                  required
                  className={`w-full px-3 py-2 rounded-xl border text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                    hasAssignedSlots
                      ? "border-gray-200 bg-gray-100 text-gray-600 cursor-not-allowed"
                      : "border-gray-300"
                  }`}
                />
              </div>

              <div>
                <label className="text-xs font-bold text-gray-500 block mb-1">
                  Final Raffle / End Date (Calculated)
                </label>
                <input
                  type="text"
                  value={calculatedEndDate}
                  disabled
                  className="w-full px-3 py-2 rounded-xl border border-gray-200 bg-gray-100 text-xs font-mono font-bold text-gray-700 cursor-not-allowed"
                />
              </div>
            </div>

            <div className="flex items-center justify-between text-xs pt-1 text-gray-600">
              <span>Winning Entitlement:</span>
              <strong className="text-[#0B3D0B] font-mono text-sm">
                LKR {(monthlyInstalmentAmount * durationMonths).toLocaleString()}
              </strong>
            </div>
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">Status</label>
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              >
                <option value="Active">Active (Accepting payments & draws)</option>
                <option value="Inactive">Inactive (Suspended)</option>
                <option value="Completed">Completed (All rounds finished)</option>
              </select>
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Description (Optional)
              </label>
              <input
                type="text"
                placeholder="Terms and details about this plan..."
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
          </div>

          {/* Footer */}
          <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 transition-colors cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] disabled:opacity-50 text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-2 cursor-pointer shadow-sm"
            >
              {submitting ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ==================== ASSIGN SLOTS MODAL ====================
function AssignChituSlotsModal({
  plan,
  onClose,
  onSuccess,
}: {
  plan: ChituPlanCardDto;
  onClose: () => void;
  onSuccess: (msg: string) => void;
}) {
  const [searchCustomer, setSearchCustomer] = useState("");
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerListItem | null>(null);
  const [slotsCount, setSlotsCount] = useState<number>(1);
  const [additionalMonths, setAdditionalMonths] = useState<number>(0);
  const [paymentMethod, setPaymentMethod] = useState<"Cash" | "Card" | "BankTransfer">("Cash");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [notes, setNotes] = useState("");

  const [preview, setPreview] = useState<ChituSlotAssignmentPreviewResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [searchingCustomer, setSearchingCustomer] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load initial active customers on mount so the list is immediately populated
  useEffect(() => {
    let active = true;
    async function loadInitial() {
      setSearchingCustomer(true);
      try {
        const res = await adminApi.getCustomers({
          pageSize: 20,
        });
        if (active) {
          setCustomers(res.items || []);
        }
      } catch (err) {
        console.error("Failed to load initial customers", err);
      } finally {
        if (active) setSearchingCustomer(false);
      }
    }
    loadInitial();
    return () => {
      active = false;
    };
  }, []);

  // Search customers on debounce
  useEffect(() => {
    const timer = setTimeout(async () => {
      setSearchingCustomer(true);
      try {
        const res = await adminApi.getCustomers({
          search: searchCustomer.trim() || undefined,
          pageSize: 20,
        });
        setCustomers(res.items || []);
      } catch (err) {
        console.error("Failed to search customers", err);
      } finally {
        setSearchingCustomer(false);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [searchCustomer]);

  // Load live assignment preview
  const fetchPreview = useCallback(async () => {
    if (!selectedCustomer) {
      setPreview(null);
      return;
    }
    setPreviewLoading(true);
    setError(null);
    try {
      const res = await adminApi.previewChituSlotAssignment({
        planId: plan.id,
        customerId: selectedCustomer.id,
        slotsCount,
        additionalPrepaidMonths: additionalMonths,
      });
      setPreview(res);
    } catch (err: any) {
      setError(err?.message || "Failed to calculate preview.");
      setPreview(null);
    } finally {
      setPreviewLoading(false);
    }
  }, [plan.id, selectedCustomer, slotsCount, additionalMonths]);

  useEffect(() => {
    fetchPreview();
  }, [fetchPreview]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCustomer) {
      setError("Please search and select a customer.");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const res = await adminApi.assignChituSlots({
        planId: plan.id,
        customerId: selectedCustomer.id,
        slotsCount,
        additionalPrepaidMonths: additionalMonths,
        paymentMethod,
        referenceNumber: referenceNumber.trim() || undefined,
        notes: notes.trim() || undefined,
      });

      onSuccess(`Successfully assigned ${res.createdSlots.length} slot(s) to ${selectedCustomer.fullName}. Total collected: LKR ${res.totalAmountPaid.toLocaleString()}`);
    } catch (err: any) {
      setError(err?.message || "Failed to assign slots.");
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-xl w-full shadow-2xl border border-gray-100 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 bg-[#FAF9F6] flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center">
              <UserPlus className="w-5 h-5 stroke-[2.5]" />
            </div>
            <div>
              <h2 className="text-base font-black text-gray-900">Assign Slots: {plan.name}</h2>
              <p className="text-[11px] text-gray-500 font-medium">Sequential slot allocation & atomic initial payment</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-gray-200 text-gray-400 hover:text-gray-700 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
          {error && (
            <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Customer Selection */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="text-xs font-bold text-gray-700 block">
                Select Customer <span className="text-red-500">*</span>
              </label>
              <a
                href="/customers"
                target="_blank"
                rel="noopener noreferrer"
                className="text-[11px] font-bold text-[#0B3D0B] hover:underline flex items-center gap-1"
                title="Opens customer registry in new tab"
              >
                <Plus className="w-3 h-3" /> Add New Customer
              </a>
            </div>
            {selectedCustomer ? (
              <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-300 flex items-center justify-between">
                <div>
                  <span className="font-bold text-gray-900 text-xs block">
                    {selectedCustomer.fullName} {selectedCustomer.primaryBranchCode ? `(${selectedCustomer.primaryBranchCode})` : ""}
                  </span>
                  <span className="text-[11px] text-gray-500">
                    Phone: {selectedCustomer.phoneNumber || "N/A"} | NIC: {selectedCustomer.nic || "N/A"}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    setSelectedCustomer(null);
                    setSearchCustomer("");
                  }}
                  className="px-2.5 py-1 bg-white border border-gray-200 rounded-lg text-xs font-semibold text-gray-600 hover:bg-gray-50 cursor-pointer"
                >
                  Change
                </button>
              </div>
            ) : (
              <div className="space-y-1.5">
                <div className="relative">
                  <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
                  <input
                    type="text"
                    placeholder="Search by name, phone, code, or NIC..."
                    value={searchCustomer}
                    onChange={(e) => setSearchCustomer(e.target.value)}
                    className="w-full pl-9 pr-4 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  />
                  {searchingCustomer && (
                    <RefreshCw className="w-3.5 h-3.5 animate-spin text-gray-400 absolute right-3 top-1/2 -translate-y-1/2" />
                  )}
                </div>

                {customers.length > 0 ? (
                  <div className="bg-white rounded-xl border border-gray-200 shadow-xs max-h-48 overflow-y-auto divide-y divide-gray-100">
                    {customers.map((c) => (
                      <button
                        key={c.id}
                        type="button"
                        onClick={() => setSelectedCustomer(c)}
                        className="w-full text-left px-3 py-2 text-xs hover:bg-emerald-50/70 flex items-center justify-between cursor-pointer transition-colors"
                      >
                        <div>
                          <span className="font-bold text-gray-900 block">
                            {c.fullName} {c.primaryBranchCode ? <span className="font-mono text-gray-400 font-normal">({c.primaryBranchCode})</span> : null}
                          </span>
                          <span className="text-gray-500 text-[10px]">
                            {c.phoneNumber || "No phone"} {c.nic ? `• NIC: ${c.nic}` : ""}
                          </span>
                        </div>
                        <span className="text-[10px] px-2.5 py-0.5 rounded-full bg-emerald-100 text-emerald-800 font-bold">
                          Select
                        </span>
                      </button>
                    ))}
                  </div>
                ) : !searchingCustomer ? (
                  <div className="p-3 rounded-xl bg-gray-50 border border-gray-200 text-center text-xs text-gray-500">
                    No customers found matching &quot;{searchCustomer}&quot;. Use the link above to add a new customer.
                  </div>
                ) : null}
              </div>
            )}
          </div>

          {/* Slot Count & Prepaid */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Number of Slots
              </label>
              <input
                type="number"
                min={1}
                max={Math.min(50, plan.availableSlotsCount)}
                value={slotsCount}
                onChange={(e) => setSlotsCount(Math.max(1, parseInt(e.target.value) || 1))}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono font-bold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
              <span className="text-[10px] text-gray-400 block mt-0.5">
                Available capacity: {plan.availableSlotsCount}
              </span>
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Additional Advance Months
              </label>
              <input
                type="number"
                min={0}
                max={plan.durationMonths - 1}
                value={additionalMonths}
                onChange={(e) => setAdditionalMonths(Math.max(0, parseInt(e.target.value) || 0))}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
              <span className="text-[10px] text-gray-400 block mt-0.5">
                Optional advance prepayments
              </span>
            </div>
          </div>

          {/* Live Assignment Preview */}
          {previewLoading ? (
            <div className="p-4 rounded-xl bg-gray-50 text-center text-xs text-gray-400">
              Calculating sequential slot numbers and payment schedule...
            </div>
          ) : preview ? (
            <div className="p-4 rounded-2xl bg-[#FAF9F6] border border-gray-200 space-y-3">
              <div className="flex items-center justify-between text-xs">
                <span className="font-bold text-gray-700">Assigned Slot Codes:</span>
                <div className="flex flex-wrap gap-1 justify-end">
                  {preview.assignedSlotDisplayCodes.map((c) => (
                    <span key={c} className="font-mono text-[11px] font-black px-2 py-0.5 rounded bg-[#0B3D0B] text-[#FFD700]">
                      {c}
                    </span>
                  ))}
                </div>
              </div>

              <div className="text-xs space-y-1 pt-2 border-t border-gray-200">
                <div className="flex items-center justify-between text-gray-600">
                  <span>Required Months Coverage:</span>
                  <span className="font-bold text-gray-900">{preview.requiredMonthsPerSlot} month(s) per slot</span>
                </div>
                {preview.additionalPrepaidMonths > 0 && (
                  <div className="flex items-center justify-between text-gray-600">
                    <span>Additional Prepaid Months:</span>
                    <span className="font-bold text-gray-900">+{preview.additionalPrepaidMonths} month(s)</span>
                  </div>
                )}
                <div className="flex items-center justify-between text-gray-900 font-bold text-sm pt-1 border-t border-gray-200">
                  <span>Total Initial Payment Required:</span>
                  <span className="font-mono text-[#0B3D0B]">
                    LKR {preview.totalPayableAmount.toLocaleString()}
                  </span>
                </div>
              </div>
            </div>
          ) : null}

          {/* Payment Method & Reference */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Payment Method <span className="text-red-500">*</span>
              </label>
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value as any)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              >
                <option value="Cash">Cash (Physically Received)</option>
                <option value="Card">Card (Card Terminal POS)</option>
                <option value="BankTransfer">Bank Transfer (Verified Receipt)</option>
              </select>
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Reference Number
              </label>
              <input
                type="text"
                placeholder="Terminal slip or transfer ref"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
          </div>

          <div>
            <label className="text-xs font-bold text-gray-700 block mb-1">
              Staff Notes (Optional)
            </label>
            <input
              type="text"
              placeholder="Internal receipt remarks..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
            />
          </div>

          {/* Footer */}
          <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting || !selectedCustomer || !preview}
              className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] disabled:opacity-50 text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-2 cursor-pointer shadow-sm"
            >
              {submitting ? "Assigning & Posting..." : "Confirm & Receipt Money"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ==================== MAKE PAYMENT MODAL ====================
function RecordChituPaymentModal({
  plan,
  onClose,
  onSuccess,
}: {
  plan: ChituPlanCardDto;
  onClose: () => void;
  onSuccess: (msg: string) => void;
}) {
  const [searchCustomer, setSearchCustomer] = useState("");
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerListItem | null>(null);
  const [searchingCustomer, setSearchingCustomer] = useState(false);

  const [payableData, setPayableData] = useState<CustomerPayableSlotsResponse | null>(null);
  const [loadingPayable, setLoadingPayable] = useState(false);

  // Per-slot allocations: map slotId -> amount
  const [slotAmounts, setSlotAmounts] = useState<Record<string, number>>({});
  const [paymentMethod, setPaymentMethod] = useState<"Cash" | "Card" | "BankTransfer">("Cash");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [notes, setNotes] = useState("");

  const [preview, setPreview] = useState<ChituPaymentPreviewResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Customer search debounce
  useEffect(() => {
    if (!searchCustomer.trim() || searchCustomer.trim().length < 2) {
      setCustomers([]);
      return;
    }
    const timer = setTimeout(async () => {
      setSearchingCustomer(true);
      try {
        const res = await adminApi.getCustomers({
          search: searchCustomer.trim(),
          pageSize: 5,
        });
        setCustomers(res.items || []);
      } catch (err) {
        console.error("Failed to search customer", err);
      } finally {
        setSearchingCustomer(false);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [searchCustomer]);

  // Load payable slots when customer selected
  useEffect(() => {
    if (!selectedCustomer) {
      setPayableData(null);
      setSlotAmounts({});
      return;
    }
    async function loadSlots() {
      setLoadingPayable(true);
      setError(null);
      try {
        const res = await adminApi.getCustomerPayableChituSlots(plan.id, selectedCustomer!.id);
        setPayableData(res);
        // Initialize 0 amount for each payable slot
        const initAmounts: Record<string, number> = {};
        for (const s of res.slots) {
          initAmounts[s.slotId] = 0;
        }
        setSlotAmounts(initAmounts);
      } catch (err: any) {
        setError(err?.message || "Failed to load payable slots.");
        setPayableData(null);
      } finally {
        setLoadingPayable(false);
      }
    }
    loadSlots();
  }, [plan.id, selectedCustomer]);

  // Update preview when allocations change
  const activeAllocations = useMemo(() => {
    return Object.entries(slotAmounts)
      .filter(([_, amt]) => amt > 0)
      .map(([slotId, amount]) => ({ slotId, amount }));
  }, [slotAmounts]);

  const totalPaymentSum = useMemo(() => {
    return activeAllocations.reduce((sum, item) => sum + item.amount, 0);
  }, [activeAllocations]);

  useEffect(() => {
    if (!selectedCustomer || activeAllocations.length === 0) {
      setPreview(null);
      return;
    }

    const timer = setTimeout(async () => {
      setPreviewLoading(true);
      setError(null);
      try {
        const res = await adminApi.previewChituPayment({
          planId: plan.id,
          customerId: selectedCustomer.id,
          slotAllocations: activeAllocations,
        });
        setPreview(res);
      } catch (err: any) {
        setError(err?.message || "Invalid allocation amount.");
        setPreview(null);
      } finally {
        setPreviewLoading(false);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [plan.id, selectedCustomer, activeAllocations]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCustomer || activeAllocations.length === 0) {
      setError("Please specify payment amounts for at least one slot.");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const res = await adminApi.recordChituPayment({
        planId: plan.id,
        customerId: selectedCustomer.id,
        slotAllocations: activeAllocations,
        paymentMethod,
        referenceNumber: referenceNumber.trim() || undefined,
        notes: notes.trim() || undefined,
      });

      onSuccess(`Payment of LKR ${res.totalAmount.toLocaleString()} recorded successfully.`);
    } catch (err: any) {
      setError(err?.message || "Failed to record payment.");
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-xl w-full shadow-2xl border border-gray-100 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 bg-[#FAF9F6] flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center">
              <CreditCard className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-black text-gray-900">Record Chitu Payment: {plan.name}</h2>
              <p className="text-[11px] text-gray-500 font-medium">Multi-slot explicit allocation & oldest-unpaid-first clearing</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-gray-200 text-gray-400 hover:text-gray-700 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
          {error && (
            <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Customer Selection */}
          <div>
            <label className="text-xs font-bold text-gray-700 block mb-1">Customer</label>
            {selectedCustomer ? (
              <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-300 flex items-center justify-between">
                <div>
                  <span className="font-bold text-gray-900 text-xs block">{selectedCustomer.fullName}</span>
                  <span className="text-[11px] text-gray-500">{selectedCustomer.phoneNumber}</span>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    setSelectedCustomer(null);
                    setSearchCustomer("");
                  }}
                  className="px-2.5 py-1 bg-white border border-gray-200 rounded-lg text-xs font-semibold text-gray-600 cursor-pointer"
                >
                  Change
                </button>
              </div>
            ) : (
              <div className="relative">
                <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  placeholder="Search customer by name or phone..."
                  value={searchCustomer}
                  onChange={(e) => setSearchCustomer(e.target.value)}
                  className="w-full pl-9 pr-4 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                />
                {searchingCustomer && (
                  <RefreshCw className="w-3.5 h-3.5 animate-spin text-gray-400 absolute right-3 top-1/2 -translate-y-1/2" />
                )}
                {customers.length > 0 && (
                  <div className="absolute top-full left-0 right-0 mt-1 bg-white rounded-xl border border-gray-200 shadow-lg z-20 max-h-48 overflow-y-auto">
                    {customers.map((c) => (
                      <button
                        key={c.id}
                        type="button"
                        onClick={() => setSelectedCustomer(c)}
                        className="w-full text-left px-3 py-2 text-xs hover:bg-gray-50 border-b border-gray-100 last:border-0 flex items-center justify-between cursor-pointer"
                      >
                        <span className="font-bold text-gray-900">{c.fullName}</span>
                        <span className="text-gray-500 text-[10px]">{c.phoneNumber}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          {/* Payable Slots */}
          {loadingPayable ? (
            <div className="p-4 rounded-xl bg-gray-50 text-center text-xs text-gray-400">
              Loading payable slots...
            </div>
          ) : payableData && payableData.slots.length === 0 ? (
            <div className="p-4 rounded-xl border border-dashed border-gray-300 text-center text-xs text-gray-500">
              This customer owns no open slots in this plan.
            </div>
          ) : payableData ? (
            <div className="space-y-3">
              <label className="text-xs font-bold text-gray-700 block">
                Enter Amount Per Slot (Multiples of LKR {plan.monthlyInstalmentAmount.toLocaleString()})
              </label>

              {payableData.slots.map((s) => (
                <div key={s.slotId} className="p-3.5 rounded-xl border border-gray-200 bg-gray-50/50 space-y-2">
                  <div className="flex items-center justify-between text-xs">
                    <div>
                      <span className="font-mono font-black text-gray-900 px-2 py-0.5 rounded bg-gray-200 mr-2">
                        {s.slotDisplayCode}
                      </span>
                      <span className="text-gray-500 font-medium">
                        {s.instalmentsPaidCount} / {plan.durationMonths} months paid
                      </span>
                    </div>
                    <span
                      className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                        s.isPaidForCurrentRound
                          ? "bg-emerald-100 text-emerald-800"
                          : "bg-red-100 text-red-800"
                      }`}
                    >
                      {s.isPaidForCurrentRound ? "Paid Current Round" : "Due / Unpaid"}
                    </span>
                  </div>

                  <div className="flex items-center gap-3 pt-1">
                    <div className="flex-1">
                      <div className="relative">
                        <span className="absolute left-3 top-1/2 -translate-y-1/2 text-xs font-bold text-gray-400">
                          LKR
                        </span>
                        <input
                          type="number"
                          step={plan.monthlyInstalmentAmount}
                          min={0}
                          max={s.totalUnpaidMonthsRemaining * plan.monthlyInstalmentAmount}
                          value={slotAmounts[s.slotId] || 0}
                          onChange={(e) => {
                            const val = Math.max(0, parseFloat(e.target.value) || 0);
                            setSlotAmounts((prev) => ({ ...prev, [s.slotId]: val }));
                          }}
                          className="w-full pl-12 pr-3 py-1.5 rounded-lg border border-gray-300 text-xs font-mono font-bold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                        />
                      </div>
                    </div>

                    <div className="flex items-center gap-1">
                      {[1, 2, 3].map((mult) => {
                        const amt = mult * plan.monthlyInstalmentAmount;
                        if (mult > s.totalUnpaidMonthsRemaining) return null;
                        return (
                          <button
                            key={mult}
                            type="button"
                            onClick={() => setSlotAmounts((prev) => ({ ...prev, [s.slotId]: amt }))}
                            className="px-2 py-1 rounded bg-white border border-gray-200 text-[10px] font-bold text-gray-700 hover:bg-gray-100 cursor-pointer"
                          >
                            +{mult}m
                          </button>
                        );
                      })}
                    </div>
                  </div>
                </div>
              ))}

              {/* Live Preview */}
              {previewLoading ? (
                <div className="p-3 text-center text-xs text-gray-400">Updating preview...</div>
              ) : preview ? (
                <div className="p-3.5 rounded-xl bg-emerald-50/60 border border-emerald-200 space-y-1.5 text-xs">
                  <div className="font-bold text-emerald-950 flex items-center justify-between">
                    <span>Payment Preview:</span>
                    <span className="font-mono text-sm">LKR {preview.totalAmount.toLocaleString()}</span>
                  </div>
                  {preview.slots.map((sp) => (
                    <div key={sp.slotId} className="flex items-center justify-between text-[11px] text-emerald-900">
                      <span>{sp.slotDisplayCode}</span>
                      <span>
                        LKR {sp.amount.toLocaleString()} ({sp.monthsCovered} month{sp.monthsCovered !== 1 ? "s" : ""} cleared)
                      </span>
                    </div>
                  ))}
                </div>
              ) : null}
            </div>
          ) : null}

          {/* Payment Method & Reference */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">Payment Method</label>
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value as any)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs font-semibold focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              >
                <option value="Cash">Cash (Physically Received)</option>
                <option value="Card">Card (Card Terminal POS)</option>
                <option value="BankTransfer">Bank Transfer (Verified Receipt)</option>
              </select>
            </div>

            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">Reference Number</label>
              <input
                type="text"
                placeholder="POS slip / transfer ref"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
          </div>

          <div>
            <label className="text-xs font-bold text-gray-700 block mb-1">Staff Notes</label>
            <input
              type="text"
              placeholder="Optional notes..."
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
            />
          </div>

          {/* Footer */}
          <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting || totalPaymentSum <= 0 || !selectedCustomer}
              className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] disabled:opacity-50 text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-2 cursor-pointer shadow-sm"
            >
              {submitting ? "Posting..." : `Confirm LKR ${totalPaymentSum.toLocaleString()}`}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ==================== RECORD PHYSICAL RAFFLE MODAL ====================
function RecordChituRaffleModal({
  plan,
  onClose,
  onSuccess,
}: {
  plan: ChituPlanCardDto;
  onClose: () => void;
  onSuccess: (msg: string) => void;
}) {
  const defaultRound = plan.overdueRaffleRoundIndex || plan.currentRoundIndex || 1;
  const [roundIndex, setRoundIndex] = useState<number>(defaultRound);
  const [eligibleData, setEligibleData] = useState<ChituEligibleSlotsResponse | null>(null);
  const [selectedSlotId, setSelectedSlotId] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState("");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [remarks, setRemarks] = useState("");

  const [loadingEligible, setLoadingEligible] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load eligible slots for round
  useEffect(() => {
    async function loadEligible() {
      setLoadingEligible(true);
      setError(null);
      setSelectedSlotId("");
      setSearchQuery("");
      try {
        const res = await adminApi.getEligibleChituRaffleSlots(plan.id, roundIndex);
        setEligibleData(res);
      } catch (err: any) {
        setError(err?.message || "Failed to load eligible slots.");
        setEligibleData(null);
      } finally {
        setLoadingEligible(false);
      }
    }
    loadEligible();
  }, [plan.id, roundIndex]);

  // Selected winner slot object
  const selectedSlot = useMemo(() => {
    return eligibleData?.eligibleSlots.find((s) => s.slotId === selectedSlotId) || null;
  }, [eligibleData?.eligibleSlots, selectedSlotId]);

  // Instant real-time search filtering
  const filteredSlots = useMemo(() => {
    if (!eligibleData?.eligibleSlots) return [];
    const q = searchQuery.trim().toLowerCase();
    if (!q) return eligibleData.eligibleSlots;

    const cleanNumStr = q.replace(/^(slot|#)\s*/i, "").trim();
    const parsedNum = parseInt(cleanNumStr, 10);
    const isNumeric = !isNaN(parsedNum) && /^\d+$/.test(cleanNumStr);

    return eligibleData.eligibleSlots.filter((s) => {
      const name = (s.customerName || "").toLowerCase();
      const code = (s.slotDisplayCode || "").toLowerCase();
      const phone = (s.customerPhone || "").toLowerCase();
      const nic = (s.customerNic || "").toLowerCase();

      // Customer name match
      if (name.includes(q)) return true;

      if (isNumeric) {
        // Slot number match (exact number e.g. "1", "5", "005", "slot 1")
        if (s.slotNumber === parsedNum) return true;

        // Only search code, phone, or NIC if query has 4+ digits
        if (cleanNumStr.length >= 4) {
          if (code.includes(q)) return true;
          if (phone.includes(q)) return true;
          if (nic.includes(q)) return true;
        }
      } else {
        // Non-numeric queries (e.g. "CHITU", phone string, etc.)
        if (code.includes(q)) return true;
        if (phone.includes(q)) return true;
        if (nic.includes(q)) return true;
      }

      return false;
    });
  }, [eligibleData?.eligibleSlots, searchQuery]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedSlotId) {
      setError("Please select the winning slot physically drawn.");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const res = await adminApi.recordChituRaffleResult(plan.id, roundIndex, {
        winningSlotId: selectedSlotId,
        referenceNumber: referenceNumber.trim() || undefined,
        remarks: remarks.trim() || undefined,
      });

      let msg = `Round ${res.roundIndex} winner recorded: Slot ${res.winningSlotDisplayCode} (${res.customerName}). Entitlement: LKR ${res.winningEntitlementValue.toLocaleString()}.`;
      if (res.advanceExcessSettlementDecisionRequired) {
        msg += ` Note: Advance prepaid excess of LKR ${res.advanceExcessPrepaidAmount.toLocaleString()} flagged for admin settlement decision.`;
      }
      onSuccess(msg);
    } catch (err: any) {
      setError(err?.message || "Failed to record raffle result.");
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-in fade-in">
      <div className="bg-white rounded-3xl max-w-xl w-full shadow-2xl border border-gray-100 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-100 bg-[#FAF9F6] flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-amber-500 text-amber-950 flex items-center justify-center">
              <Trophy className="w-5 h-5 stroke-[2.5]" />
            </div>
            <div>
              <h2 className="text-base font-black text-gray-900">Record Physical Raffle Draw</h2>
              <p className="text-[11px] text-gray-500 font-medium">Plan: {plan.name} ({plan.code})</p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-xl hover:bg-gray-200 text-gray-400 hover:text-gray-700 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4 overflow-y-auto flex-1">
          {error && (
            <div className="p-3 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {/* Round Selector */}
          <div className="flex items-center justify-between p-3.5 rounded-xl bg-gray-50 border border-gray-200">
            <div>
              <span className="text-xs font-bold text-gray-700 block">Raffle Round</span>
              <span className="text-[11px] text-gray-500">
                Scheduled Date: {eligibleData?.scheduledRaffleDate || "..."}
              </span>
            </div>
            <select
              value={roundIndex}
              onChange={(e) => setRoundIndex(Number(e.target.value))}
              className="px-3 py-1.5 rounded-lg border border-gray-300 bg-white text-xs font-bold font-mono focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
            >
              {Array.from({ length: plan.durationMonths }, (_, i) => i + 1).map((r) => (
                <option key={r} value={r}>
                  Round {r}
                </option>
              ))}
            </select>
          </div>

          {/* Selected Winner Card */}
          {selectedSlot && (
            <div className="p-3.5 rounded-2xl bg-amber-50/80 border-2 border-amber-400/80 shadow-xs flex items-center justify-between gap-3 animate-in fade-in">
              <div className="flex items-center gap-3 min-w-0">
                <div className="w-9 h-9 rounded-xl bg-amber-500 text-amber-950 flex items-center justify-center shrink-0 shadow-xs">
                  <Trophy className="w-5 h-5 stroke-[2.5]" />
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-[10px] font-black uppercase tracking-wider px-2 py-0.5 rounded-md bg-amber-200/90 text-amber-950">
                      Selected Winner
                    </span>
                    <span className="font-mono font-bold text-xs text-amber-950">
                      Slot #{selectedSlot.slotNumber} ({selectedSlot.slotDisplayCode})
                    </span>
                  </div>
                  <p className="text-xs font-bold text-gray-900 truncate mt-0.5">
                    {selectedSlot.customerName}
                  </p>
                  <p className="text-[10px] text-gray-600 truncate">
                    {selectedSlot.customerPhone || "No phone"}
                    {selectedSlot.customerNic ? ` • NIC: ${selectedSlot.customerNic}` : ""}
                    {" • "}{selectedSlot.monthsPaidCount} months covered
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setSelectedSlotId("")}
                className="px-2.5 py-1 text-[11px] font-bold text-amber-900 hover:bg-amber-200/60 rounded-lg transition-colors cursor-pointer shrink-0"
              >
                Change
              </button>
            </div>
          )}

          {/* Eligible Slots List with Search */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-xs font-bold text-gray-700">
                Eligible Slots Pool ({eligibleData?.eligibleCount || 0})
              </label>
              <span className="text-[10px] text-gray-400 font-medium">
                Must be paid-through Round {roundIndex} & not a prior winner
              </span>
            </div>

            {loadingEligible ? (
              <div className="p-8 text-center text-xs text-gray-400">
                Verifying eligible slots pool...
              </div>
            ) : eligibleData && eligibleData.eligibleSlots.length === 0 ? (
              <div className="p-6 rounded-xl border border-dashed border-red-200 bg-red-50 text-center text-xs text-red-800 space-y-1">
                <AlertTriangle className="w-5 h-5 text-red-600 mx-auto" />
                <p className="font-bold">No Eligible Slots Available</p>
                <p className="text-[11px] text-red-600">
                  Either all active slots have unpaid obligations for Round {roundIndex}, or this round was already recorded.
                </p>
              </div>
            ) : eligibleData ? (
              <div className="space-y-2">
                {/* Search Bar */}
                <div className="relative">
                  <Search className="w-3.5 h-3.5 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none" />
                  <input
                    type="text"
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    placeholder="Search by customer name, slot # (e.g. 5, 005), code, phone, or NIC..."
                    className="w-full pl-8.5 pr-8 py-2 rounded-xl border border-gray-200 text-xs bg-white placeholder:text-gray-400 focus:outline-none focus:ring-2 focus:ring-amber-500/40 focus:border-amber-500 shadow-2xs"
                  />
                  {searchQuery && (
                    <button
                      type="button"
                      onClick={() => setSearchQuery("")}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 p-0.5 rounded cursor-pointer"
                      title="Clear search"
                    >
                      <X className="w-3.5 h-3.5" />
                    </button>
                  )}
                </div>

                {/* Search filter feedback */}
                {searchQuery.trim() && (
                  <div className="flex items-center justify-between text-[11px] text-gray-500 px-1">
                    <span>
                      Found <strong className="text-gray-800">{filteredSlots.length}</strong> of {eligibleData.eligibleSlots.length} eligible slots
                    </span>
                    <button
                      type="button"
                      onClick={() => setSearchQuery("")}
                      className="text-amber-800 hover:underline font-semibold cursor-pointer"
                    >
                      Clear search
                    </button>
                  </div>
                )}

                {/* Slots List or Empty Match State */}
                {filteredSlots.length === 0 ? (
                  <div className="p-6 rounded-xl border border-dashed border-gray-200 bg-gray-50 text-center text-xs text-gray-600 space-y-2">
                    <Search className="w-5 h-5 text-gray-400 mx-auto" />
                    <p className="font-bold text-gray-700">No matching slots found</p>
                    <p className="text-[11px] text-gray-500">
                      No eligible slots match &ldquo;{searchQuery}&rdquo;. Try checking the customer name or slot number.
                    </p>
                    <button
                      type="button"
                      onClick={() => setSearchQuery("")}
                      className="px-3 py-1 rounded-lg bg-gray-200 hover:bg-gray-300 text-[11px] font-bold text-gray-700 cursor-pointer"
                    >
                      Clear Search
                    </button>
                  </div>
                ) : (
                  <div className="space-y-1.5 max-h-56 overflow-y-auto border border-gray-200 rounded-xl p-2 bg-gray-50/50">
                    {filteredSlots.map((s) => {
                      const isSelected = selectedSlotId === s.slotId;
                      return (
                        <button
                          key={s.slotId}
                          type="button"
                          onClick={() => setSelectedSlotId(s.slotId)}
                          className={`w-full text-left p-2.5 rounded-lg border text-xs flex items-center justify-between transition-all cursor-pointer ${
                            isSelected
                              ? "bg-amber-50 border-amber-400 ring-2 ring-amber-400/50 shadow-xs"
                              : "bg-white border-gray-200 hover:bg-gray-50"
                          }`}
                        >
                          <div className="flex items-center gap-3 min-w-0">
                            <div
                              className={`w-5 h-5 rounded-full border flex items-center justify-center shrink-0 ${
                                isSelected
                                  ? "border-amber-600 bg-amber-500 text-white"
                                  : "border-gray-300"
                              }`}
                            >
                              {isSelected && <Check className="w-3 h-3 stroke-[3]" />}
                            </div>
                            <div className="min-w-0">
                              <div className="flex items-center gap-1.5 flex-wrap">
                                <span className="font-mono font-bold text-gray-900">
                                  {s.slotDisplayCode}
                                </span>
                                <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-gray-100 text-gray-600 font-mono">
                                  #{s.slotNumber}
                                </span>
                                <span className="font-semibold text-gray-900 truncate">
                                  - {s.customerName}
                                </span>
                              </div>
                              <div className="text-[10px] text-gray-500 truncate flex items-center gap-1.5 mt-0.5">
                                <span>{s.customerPhone || "No phone"}</span>
                                {s.customerNic && (
                                  <>
                                    <span>•</span>
                                    <span>NIC: {s.customerNic}</span>
                                  </>
                                )}
                                <span>•</span>
                                <span>{s.monthsPaidCount} mos covered</span>
                              </div>
                            </div>
                          </div>

                          <span className="text-[11px] font-mono font-bold text-emerald-800 shrink-0 ml-2">
                            Paid: LKR {s.totalConfirmedPaid.toLocaleString()}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            ) : null}
          </div>

          {/* Entitlement Summary */}
          <div className="p-3.5 rounded-xl bg-amber-50/70 border border-amber-200 text-xs space-y-1">
            <div className="flex items-center justify-between text-amber-950 font-bold">
              <span>Winning Entitlement Amount:</span>
              <span className="font-mono text-sm">
                LKR {(plan.monthlyInstalmentAmount * plan.durationMonths).toLocaleString()}
              </span>
            </div>
            <p className="text-[10px] text-amber-800 leading-relaxed">
              Recording this winner immediately stops future payment obligations for this slot and excludes it from all later raffles. Other slots owned by this customer remain active.
            </p>
          </div>

          {/* Reference & Remarks */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Token / Draw Reference
              </label>
              <input
                type="text"
                placeholder="e.g. Physical Token #42"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
            <div>
              <label className="text-xs font-bold text-gray-700 block mb-1">
                Witness / Venue Remarks
              </label>
              <input
                type="text"
                placeholder="e.g. Drawn at Colombo showroom"
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
              />
            </div>
          </div>

          {/* Footer */}
          <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting || !selectedSlotId}
              className="px-5 py-2.5 rounded-xl bg-amber-500 hover:bg-amber-600 disabled:opacity-50 text-amber-950 text-xs font-black uppercase tracking-wider flex items-center gap-2 cursor-pointer shadow-sm"
            >
              {submitting ? "Recording Winner..." : "Confirm Physical Winner"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
