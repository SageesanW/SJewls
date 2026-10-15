"use client";

import React, { useState, useEffect, useCallback, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { AdminLayout } from "../../../components/AdminLayout";
import { adminApi, ApiError } from "../../../lib/api";
import { useAuth } from "../../../lib/auth-context";
import { JewelleryPlan, PlanCustomerEnrolment, PlanCustomersPagedResponse } from "../../../types/jewelleryPlan";
import { AssignPlanModal } from "../../../components/AssignPlanModal";
import { RecordPaymentModal } from "../../../components/RecordPaymentModal";
import {
  Users,
  Search,
  Filter,
  Plus,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  Eye,
  CreditCard,
  AlertCircle,
  Sparkles,
  ArrowRight,
  ShieldCheck,
  CheckCircle2,
} from "lucide-react";

function PlanCustomersContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user, isSuperAdmin } = useAuth();

  // Plans selector state
  const [plans, setPlans] = useState<JewelleryPlan[]>([]);
  const [selectedPlanId, setSelectedPlanId] = useState<string>(searchParams.get("planId") || "");
  const [plansLoading, setPlansLoading] = useState<boolean>(true);

  // Enrolment roster state
  const [rosterData, setRosterData] = useState<PlanCustomersPagedResponse | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Filter and search state
  const [search, setSearch] = useState<string>(searchParams.get("search") || "");
  const [debouncedSearch, setDebouncedSearch] = useState<string>(searchParams.get("search") || "");
  const [statusFilter, setStatusFilter] = useState<string>(searchParams.get("status") || "All");
  const [page, setPage] = useState<number>(parseInt(searchParams.get("page") || "1", 10));
  const [pageSize, setPageSize] = useState<number>(10);

  // Modals state
  const [showAssignModal, setShowAssignModal] = useState<boolean>(false);
  const [showPaymentModal, setShowPaymentModal] = useState<boolean>(false);
  const [paymentEnrolment, setPaymentEnrolment] = useState<PlanCustomerEnrolment | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  // Load plans for dropdown
  const loadPlans = useCallback(async () => {
    setPlansLoading(true);
    try {
      const res = await adminApi.getJewelleryPlans({ pageSize: 100 });
      setPlans(res.items);

      // Default to first active plan or first plan if not set
      if (!selectedPlanId && res.items.length > 0) {
        const defaultPlan = res.items.find((p) => p.isActive) || res.items[0];
        setSelectedPlanId(defaultPlan.id);
      }
    } catch (err: any) {
      console.error("Failed to load jewellery plans:", err);
      setError("Failed to load jewellery plans list.");
    } finally {
      setPlansLoading(false);
    }
  }, [selectedPlanId]);

  useEffect(() => {
    loadPlans();
  }, [loadPlans]);

  // Load customer enrolments for selected plan
  const loadPlanCustomers = useCallback(async () => {
    if (!selectedPlanId) return;

    setLoading(true);
    setError(null);

    try {
      const res = await adminApi.getJewelleryPlanCustomers(selectedPlanId, {
        page,
        pageSize,
        search: debouncedSearch.trim() || undefined,
        status: statusFilter !== "All" ? statusFilter : undefined,
      });
      setRosterData(res);
    } catch (err: any) {
      console.error("Failed to load plan customers:", err);
      setError(err.message || "Failed to load customer enrolments for this plan.");
      setRosterData(null);
    } finally {
      setLoading(false);
    }
  }, [selectedPlanId, page, pageSize, debouncedSearch, statusFilter]);

  useEffect(() => {
    if (selectedPlanId) {
      loadPlanCustomers();
    }
  }, [selectedPlanId, loadPlanCustomers]);

  // Update URL search params to preserve filters on back-navigation
  useEffect(() => {
    if (!selectedPlanId) return;
    const params = new URLSearchParams();
    params.set("planId", selectedPlanId);
    if (page > 1) params.set("page", page.toString());
    if (debouncedSearch) params.set("search", debouncedSearch);
    if (statusFilter !== "All") params.set("status", statusFilter);

    const newUrl = `/jewellery-plans/customers?${params.toString()}`;
    window.history.replaceState(null, "", newUrl);
  }, [selectedPlanId, page, debouncedSearch, statusFilter]);

  const handlePlanChange = (newPlanId: string) => {
    setSelectedPlanId(newPlanId);
    setPage(1); // Reset pagination on plan change as required
  };

  const handleOpenPayment = (enr: PlanCustomerEnrolment) => {
    setPaymentEnrolment(enr);
    setShowPaymentModal(true);
  };

  const selectedPlanObj = plans.find((p) => p.id === selectedPlanId);

  return (
    <AdminLayout title="Plan Customers">
      <div className="space-y-6 max-w-7xl mx-auto pb-12">
        {/* Toast Alert */}
        {toastMessage && (
          <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-900 text-sm flex items-center justify-between shadow-sm animate-in fade-in">
            <div className="flex items-center gap-2.5">
              <CheckCircle2 className="w-5 h-5 text-emerald-700 flex-shrink-0" />
              <span className="font-semibold">{toastMessage}</span>
            </div>
            <button
              onClick={() => setToastMessage(null)}
              className="text-emerald-700 hover:text-emerald-900 text-xs font-bold uppercase tracking-wider"
            >
              Dismiss
            </button>
          </div>
        )}

        {/* Top Header Section matching Screenshot 1 & 4 */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white p-6 rounded-2xl border border-gray-100 shadow-sm">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 rounded-2xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center shadow-md shadow-[#0B3D0B]/20">
              <Users className="w-6 h-6" />
            </div>
            <div>
              <h1 className="text-2xl font-black text-gray-900 tracking-tight">Plan Customers</h1>
              <p className="text-xs text-gray-500 mt-0.5">
                Inspect customer jewellery plan enrolments, progress, and financial contributions
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <button
              type="button"
              onClick={() => setShowAssignModal(true)}
              className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] text-[#FFD700] text-xs font-extrabold uppercase tracking-wider flex items-center gap-2 shadow-sm transition-all transform active:scale-95"
            >
              <Plus className="w-4 h-4 stroke-[3]" />
              Assign Plan
            </button>
          </div>
        </div>

        {/* Filter and Selection Card */}
        <div className="bg-white p-6 rounded-2xl border border-gray-100 shadow-sm space-y-5">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-4">
            {/* Plan Selector with Active Investment Title */}
            <div className="flex-1 max-w-lg">
              <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block mb-1">
                Active Investment Details
              </span>
              <h2 className="text-lg font-black text-gray-900 mb-2">
                {selectedPlanObj?.name || "Select Jewellery Plan"}
              </h2>

              <div className="relative">
                <select
                  value={selectedPlanId}
                  onChange={(e) => handlePlanChange(e.target.value)}
                  disabled={plansLoading}
                  className="w-full px-4 py-2.5 text-sm rounded-xl border border-gray-200 bg-white font-medium text-gray-800 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
                >
                  {plans.map((p) => {
                    const isDeactivated = !p.isActive || p.displayStatus === "Deactivated";
                    const isScheduled = p.displayStatus === "Scheduled";
                    return (
                      <option key={p.id} value={p.id} className="text-gray-900">
                        {p.name} ({p.categoryName || "Jewellery"})
                        {isDeactivated ? " • [Deactivated]" : isScheduled ? " • [Scheduled]" : ""}
                      </option>
                    );
                  })}
                </select>
              </div>
            </div>

            {/* Search & Filter Bar */}
            <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
              <div className="relative min-w-[280px]">
                <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
                <input
                  type="text"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search by name, phone, email..."
                  className="w-full pl-10 pr-4 py-2 text-xs rounded-xl border border-gray-200 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
                />
              </div>

              {/* Status Filter */}
              <div className="flex items-center gap-1.5 bg-gray-50 p-1 rounded-xl border border-gray-200">
                <Filter className="w-3.5 h-3.5 text-gray-400 ml-2" />
                <select
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(e.target.value);
                    setPage(1);
                  }}
                  className="bg-transparent text-xs font-semibold text-gray-700 px-2 py-1 focus:outline-none cursor-pointer"
                >
                  <option value="All">All Statuses</option>
                  <option value="Active">Active</option>
                  <option value="Completed">Completed</option>
                  <option value="Extended">Extended</option>
                  <option value="Cancelled">Cancelled</option>
                  <option value="DurationEnded">Duration Ended</option>
                </select>
              </div>

              <button
                type="button"
                onClick={loadPlanCustomers}
                disabled={loading}
                className="p-2 rounded-xl border border-gray-200 hover:bg-gray-50 text-gray-600 transition-colors"
                title="Refresh List"
              >
                <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin text-[#0B3D0B]" : ""}`} />
              </button>
            </div>
          </div>

          {/* Plan Summary Bar */}
          {rosterData && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 pt-4 border-t border-gray-100 text-xs">
              <div className="p-3 rounded-xl bg-[#FAF9F6] border border-gray-100">
                <span className="text-gray-400 block text-[10px] font-bold uppercase">Target Gold</span>
                <span className="text-sm font-extrabold text-gray-900">{rosterData.planTargetGrams} g</span>
              </div>
              <div className="p-3 rounded-xl bg-[#FAF9F6] border border-gray-100">
                <span className="text-gray-400 block text-[10px] font-bold uppercase">Total Collected</span>
                <span className="text-sm font-extrabold text-[#0B3D0B]">
                  {rosterData.currency}{" "}
                  {(rosterData.totalCollectedCurrency || 0).toLocaleString(undefined, {
                    minimumFractionDigits: 2,
                  })}
                </span>
              </div>
              <div className="p-3 rounded-xl bg-[#FAF9F6] border border-gray-100">
                <span className="text-gray-400 block text-[10px] font-bold uppercase">Overall Plan Progress</span>
                <span className="text-sm font-extrabold text-gray-900">
                  {rosterData.overallProgressPercentage}%
                </span>
              </div>
              <div className="p-3 rounded-xl bg-[#FAF9F6] border border-gray-100">
                <span className="text-gray-400 block text-[10px] font-bold uppercase">Enrolled Customers</span>
                <span className="text-sm font-extrabold text-gray-900">{rosterData.totalCount} Enrolment(s)</span>
              </div>
            </div>
          )}
        </div>

        {/* Error State */}
        {error && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-3">
            <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
            <div className="flex-1 font-medium">{error}</div>
            <button
              onClick={loadPlanCustomers}
              className="text-xs font-bold text-red-900 underline hover:no-underline"
            >
              Retry
            </button>
          </div>
        )}

        {/* Enrolment Roster Table */}
        <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="border-b border-gray-100 bg-[#FAF9F6] text-[11px] font-extrabold text-gray-500 uppercase tracking-wider">
                  <th className="py-3.5 px-4">Customer Name</th>
                  <th className="py-3.5 px-4">Phone</th>
                  <th className="py-3.5 px-4">Email</th>
                  <th className="py-3.5 px-4">Enrolment ID</th>
                  <th className="py-3.5 px-4">Target</th>
                  <th className="py-3.5 px-4">Duration</th>
                  <th className="py-3.5 px-4">Paid Amount</th>
                  <th className="py-3.5 px-4">Net Grams</th>
                  <th className="py-3.5 px-4">Remaining</th>
                  <th className="py-3.5 px-4">Progress</th>
                  <th className="py-3.5 px-4">Status</th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-50 text-xs">
                {loading ? (
                  <tr>
                    <td colSpan={12} className="py-12 text-center text-gray-400">
                      <div className="flex flex-col items-center justify-center gap-2">
                        <RefreshCw className="w-6 h-6 animate-spin text-[#0B3D0B]" />
                        <span>Loading plan customer enrolments...</span>
                      </div>
                    </td>
                  </tr>
                ) : rosterData?.items && rosterData.items.length > 0 ? (
                  rosterData.items.map((enr) => {
                    const initials = enr.customerName
                      .split(" ")
                      .map((n) => n[0])
                      .join("")
                      .substring(0, 2)
                      .toUpperCase();

                    return (
                      <tr key={enr.enrolmentId} className="hover:bg-gray-50/80 transition-colors">
                        {/* Customer Name */}
                        <td className="py-3 px-4">
                          <div className="flex items-center gap-2.5">
                            <div className="w-8 h-8 rounded-full bg-amber-100 text-amber-900 font-extrabold flex items-center justify-center text-[11px] flex-shrink-0">
                              {initials}
                            </div>
                            <div className="font-bold text-gray-900">{enr.customerName}</div>
                          </div>
                        </td>

                        {/* Phone */}
                        <td className="py-3 px-4 text-gray-600 font-mono text-[11px]">
                          {enr.phoneNumber || "—"}
                        </td>

                        {/* Email */}
                        <td className="py-3 px-4 text-gray-600 truncate max-w-[150px]">
                          {enr.email || "—"}
                        </td>

                        {/* Enrolment ID */}
                        <td className="py-3 px-4 font-mono font-bold text-gray-700 text-[11px]">
                          {enr.enrolmentReference || `ENR-${enr.enrolmentId.substring(0, 8).toUpperCase()}`}
                        </td>

                        {/* Target */}
                        <td className="py-3 px-4 font-bold text-gray-900">
                          {enr.enrolmentTargetGrams} g
                        </td>

                        {/* Duration */}
                        <td className="py-3 px-4 text-gray-600">
                          {enr.selectedDurationMonths} mos
                        </td>

                        {/* Paid Amount */}
                        <td className="py-3 px-4 font-extrabold text-[#0B3D0B]">
                          {enr.currency} {enr.totalConfirmedMoneyPaid.toLocaleString()}
                        </td>

                        {/* Net Credited Grams */}
                        <td className="py-3 px-4 font-bold text-emerald-800">
                          {enr.netAccumulatedGrams.toFixed(4)} g
                        </td>

                        {/* Remaining Grams */}
                        <td className="py-3 px-4 text-gray-500 font-medium">
                          {enr.remainingGrams.toFixed(4)} g
                        </td>

                        {/* Progress Bar */}
                        <td className="py-3 px-4 min-w-[110px]">
                          <div className="flex items-center gap-2">
                            <div className="flex-1 h-2 rounded-full bg-gray-100 overflow-hidden">
                              <div
                                className="h-full bg-gradient-to-r from-emerald-600 to-[#0B3D0B] rounded-full transition-all duration-300"
                                style={{ width: `${Math.min(100, enr.individualProgressPercentage)}%` }}
                              />
                            </div>
                            <span className="font-bold text-gray-800 text-[11px] w-9 text-right">
                              {enr.individualProgressPercentage}%
                            </span>
                          </div>
                        </td>

                        {/* Status Badge */}
                        <td className="py-3 px-4">
                          <span
                            className={`px-2.5 py-1 rounded-full text-[10px] font-bold ${
                              enr.enrolmentStatus === "Active"
                                ? "bg-emerald-100 text-emerald-800"
                                : enr.enrolmentStatus === "Completed"
                                ? "bg-[#FFD700]/30 text-amber-900 font-black border border-amber-300"
                                : "bg-gray-100 text-gray-600"
                            }`}
                          >
                            {enr.enrolmentStatus}
                          </span>
                        </td>

                        {/* Actions */}
                        <td className="py-3 px-4 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            {enr.enrolmentStatus === "Active" && (
                              <button
                                type="button"
                                onClick={() => handleOpenPayment(enr)}
                                className="p-1.5 rounded-lg text-emerald-800 hover:bg-emerald-50 transition-colors"
                                title="Record Today's Payment"
                              >
                                <CreditCard className="w-4 h-4" />
                              </button>
                            )}

                            <Link
                              href={`/customers/${enr.customerId}`}
                              className="px-2.5 py-1.5 rounded-lg bg-gray-100 hover:bg-gray-200 text-gray-700 font-bold text-[11px] flex items-center gap-1 transition-colors"
                            >
                              <Eye className="w-3.5 h-3.5" />
                              View Customer
                            </Link>
                          </div>
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={12} className="py-12 text-center text-gray-400">
                      <div className="flex flex-col items-center justify-center gap-2">
                        <Users className="w-8 h-8 text-gray-300" />
                        <span className="text-sm font-semibold text-gray-600">No customer enrolments found</span>
                        <p className="text-xs text-gray-400 max-w-sm">
                          There are no customers currently enrolled in this plan matching the search and status filter.
                        </p>
                      </div>
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {rosterData && rosterData.totalCount > 0 && (
            <div className="flex flex-col sm:flex-row items-center justify-between px-6 py-4 border-t border-gray-100 gap-4 text-xs text-gray-500">
              <div>
                Showing results{" "}
                <span className="font-bold text-gray-800">
                  {Math.min((page - 1) * pageSize + 1, rosterData.totalCount)}
                </span>{" "}
                to{" "}
                <span className="font-bold text-gray-800">
                  {Math.min(page * pageSize, rosterData.totalCount)}
                </span>{" "}
                of <span className="font-bold text-gray-800">{rosterData.totalCount}</span>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  disabled={page <= 1 || loading}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  <ChevronLeft className="w-4 h-4" />
                </button>

                <span className="font-semibold text-gray-700 px-2">
                  Page {page} of {rosterData.totalPages || 1}
                </span>

                <button
                  type="button"
                  disabled={page >= (rosterData.totalPages || 1) || loading}
                  onClick={() => setPage((p) => p + 1)}
                  className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                >
                  <ChevronRight className="w-4 h-4" />
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Assign Plan Modal */}
      <AssignPlanModal
        isOpen={showAssignModal}
        onClose={() => setShowAssignModal(false)}
        initialPlanId={selectedPlanId}
        onSuccess={(msg) => {
          setToastMessage(msg);
          loadPlanCustomers();
        }}
        onOpenCreateCustomer={() => {
          router.push("/customers?create=true");
        }}
      />

      {/* Record In-Store Payment Modal */}
      {paymentEnrolment && (
        <RecordPaymentModal
          isOpen={showPaymentModal}
          onClose={() => {
            setShowPaymentModal(false);
            setPaymentEnrolment(null);
          }}
          customerId={paymentEnrolment.customerId}
          customerName={paymentEnrolment.customerName}
          initialEnrolmentId={paymentEnrolment.enrolmentId}
          enrolments={[
            {
              id: paymentEnrolment.enrolmentId,
              enrolmentReference: paymentEnrolment.enrolmentReference || "",
              planId: selectedPlanId,
              planName: selectedPlanObj?.name || "Jewellery Plan",
              planCode: selectedPlanObj?.code || "JEW",
              categoryName: selectedPlanObj?.categoryName || "",
              targetWeightGrams: paymentEnrolment.enrolmentTargetGrams,
              totalSavedGrams: paymentEnrolment.netAccumulatedGrams,
              remainingGrams: paymentEnrolment.remainingGrams,
              totalContributedCurrency: paymentEnrolment.totalConfirmedMoneyPaid,
              currency: paymentEnrolment.currency,
              premiumPercentage: paymentEnrolment.premiumPercentage,
              selectedDurationMonths: paymentEnrolment.selectedDurationMonths,
              progressPercentage: paymentEnrolment.individualProgressPercentage,
              status: paymentEnrolment.enrolmentStatus,
              startDate: paymentEnrolment.joiningDate,
              targetEndDate: paymentEnrolment.deadline,
              createdAtUtc: "",
              contributions: paymentEnrolment.contributions,
            },
          ]}
          onSuccess={(msg) => {
            setToastMessage(msg);
            loadPlanCustomers();
          }}
        />
      )}
    </AdminLayout>
  );
}

export default function PlanCustomersPage() {
  return (
    <Suspense fallback={<div className="p-12 text-center text-gray-500">Loading Plan Customers...</div>}>
      <PlanCustomersContent />
    </Suspense>
  );
}
