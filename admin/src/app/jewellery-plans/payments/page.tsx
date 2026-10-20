"use client";

import React, { useState, useEffect, useCallback, useRef, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { AdminLayout } from "../../../components/AdminLayout";
import { adminApi, jewelleryEnrolmentsApi, ApiError } from "../../../lib/api";
import { useAuth } from "../../../lib/auth-context";
import {
  JewelleryPlan,
  AdminJewelleryPaymentItem,
  AdminJewelleryPaymentsPagedResponse,
  AdminIncompleteEnrolmentItem,
  AdminIncompleteEnrolmentsPagedResponse,
} from "../../../types/jewelleryPlan";
import {
  CreditCard,
  Search,
  Filter,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
  AlertCircle,
  CheckCircle2,
  Clock,
  Send,
  Calendar,
  DollarSign,
  TrendingUp,
  AlertTriangle,
  ArrowRight,
  ExternalLink,
  ShieldAlert,
  Info,
} from "lucide-react";

interface BranchOption {
  id: string;
  name: string;
}

function PaymentsContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user, isSuperAdmin } = useAuth();

  // Active Tab: "history" | "incomplete"
  const [activeTab, setActiveTab] = useState<"history" | "incomplete">(
    (searchParams.get("tab") as "history" | "incomplete") || "history"
  );

  // Filters state
  const [search, setSearch] = useState<string>(searchParams.get("search") || "");
  const [debouncedSearch, setDebouncedSearch] = useState<string>(searchParams.get("search") || "");
  const [selectedPlanId, setSelectedPlanId] = useState<string>(searchParams.get("planId") || "");
  const [statusFilter, setStatusFilter] = useState<string>(searchParams.get("status") || "All");
  const [fromDate, setFromDate] = useState<string>(searchParams.get("fromDate") || "");
  const [toDate, setToDate] = useState<string>(searchParams.get("toDate") || "");
  const [selectedBranchId, setSelectedBranchId] = useState<string>(searchParams.get("branchId") || "");

  // Pagination state
  const [page, setPage] = useState<number>(parseInt(searchParams.get("page") || "1", 10));
  const [pageSize, setPageSize] = useState<number>(10);

  // Reference data
  const [plans, setPlans] = useState<JewelleryPlan[]>([]);
  const [branches, setBranches] = useState<BranchOption[]>([]);

  // Payment History data state
  const [historyData, setHistoryData] = useState<AdminJewelleryPaymentsPagedResponse | null>(null);
  // Incomplete Plans data state
  const [incompleteData, setIncompleteData] = useState<AdminIncompleteEnrolmentsPagedResponse | null>(null);

  // Loading & refresh states
  const [initialLoading, setInitialLoading] = useState<boolean>(true);
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false);
  const [refreshError, setRefreshError] = useState<string | null>(null);
  const [initialError, setInitialError] = useState<string | null>(null);
  const [lastRefreshedAt, setLastRefreshedAt] = useState<Date>(new Date());

  // Prevent overlapping background fetches
  const isFetchingRef = useRef<boolean>(false);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 350);
    return () => clearTimeout(timer);
  }, [search]);

  // Sync tab change with URL
  const handleTabChange = (newTab: "history" | "incomplete") => {
    setActiveTab(newTab);
    setPage(1);
    const params = new URLSearchParams(window.location.search);
    params.set("tab", newTab);
    params.set("page", "1");
    router.replace(`/jewellery-plans/payments?${params.toString()}`);
  };

  // Load plans & branches reference data
  useEffect(() => {
    async function loadReferenceData() {
      try {
        const plansRes = await adminApi.getJewelleryPlans({ pageSize: 100 });
        setPlans(plansRes.items || []);

        if (isSuperAdmin) {
          const branchesRes = await adminApi.getBranches();
          setBranches(branchesRes.map((b) => ({ id: b.id, name: b.name })));
        } else if (user?.assignedBranches) {
          setBranches(
            user.assignedBranches.map((b) => ({ id: b.branchId, name: b.name }))
          );
        }
      } catch (err) {
        console.error("Failed to load reference metadata:", err);
      }
    }
    loadReferenceData();
  }, [isSuperAdmin, user]);

  // Main fetch function (handles initial load and background polling)
  const fetchData = useCallback(
    async (isBackground = false) => {
      if (isFetchingRef.current) return;
      isFetchingRef.current = true;

      if (!isBackground) {
        if (!historyData && !incompleteData) {
          setInitialLoading(true);
        }
        setInitialError(null);
      } else {
        setIsRefreshing(true);
      }

      setRefreshError(null);

      try {
        if (activeTab === "history") {
          const res = await jewelleryEnrolmentsApi.getPaymentsPaged({
            branchId: selectedBranchId || undefined,
            planId: selectedPlanId || undefined,
            status: statusFilter !== "All" ? statusFilter : undefined,
            search: debouncedSearch.trim() || undefined,
            fromDate: fromDate || undefined,
            toDate: toDate || undefined,
            page,
            pageSize,
          });

          setHistoryData(res);
          // If current page is beyond total pages after a filter change, snap to page 1
          if (res.totalPages > 0 && page > res.totalPages) {
            setPage(1);
          }
        } else {
          const res = await jewelleryEnrolmentsApi.getIncompleteEnrolmentsPaged({
            branchId: selectedBranchId || undefined,
            planId: selectedPlanId || undefined,
            search: debouncedSearch.trim() || undefined,
            page,
            pageSize,
          });

          setIncompleteData(res);
          if (res.totalPages > 0 && page > res.totalPages) {
            setPage(1);
          }
        }
        setLastRefreshedAt(new Date());
      } catch (err: any) {
        console.error("Failed to fetch payments data:", err);
        const msg = err?.message || "Failed to retrieve jewellery payment records.";
        if (isBackground) {
          setRefreshError(msg);
        } else {
          setInitialError(msg);
        }
      } finally {
        isFetchingRef.current = false;
        setInitialLoading(false);
        setIsRefreshing(false);
      }
    },
    [
      activeTab,
      selectedBranchId,
      selectedPlanId,
      statusFilter,
      debouncedSearch,
      fromDate,
      toDate,
      page,
      pageSize,
      historyData,
      incompleteData,
    ]
  );

  // Trigger fetch when parameters or active tab change
  useEffect(() => {
    fetchData(false);
  }, [fetchData]);

  // 15-second automatic background polling with tab visibility pausing
  useEffect(() => {
    const POLLING_INTERVAL_MS = 15000;

    const intervalId = setInterval(() => {
      // Pause polling if document/tab is not visible
      if (typeof document !== "undefined" && document.visibilityState === "hidden") {
        return;
      }
      fetchData(true);
    }, POLLING_INTERVAL_MS);

    // Refresh immediately when window gains focus/reconnects
    const handleFocus = () => {
      fetchData(true);
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        fetchData(true);
      }
    };

    window.addEventListener("focus", handleFocus);
    document.addEventListener("visibilitychange", handleVisibilityChange);

    return () => {
      clearInterval(intervalId);
      window.removeEventListener("focus", handleFocus);
      document.removeEventListener("visibilitychange", handleVisibilityChange);
    };
  }, [fetchData]);

  // Format helpers
  const formatDateTime = (isoUtc: string | null | undefined, tz = "Asia/Colombo") => {
    if (!isoUtc) return "—";
    try {
      const d = new Date(isoUtc);
      return new Intl.DateTimeFormat("en-GB", {
        timeZone: tz,
        year: "numeric",
        month: "short",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
        hour12: true,
      }).format(d);
    } catch {
      return isoUtc;
    }
  };

  const formatDateOnly = (dateOnlyStr: string | null | undefined) => {
    if (!dateOnlyStr) return "—";
    try {
      const [y, m, d] = dateOnlyStr.split("-");
      const date = new Date(parseInt(y, 10), parseInt(m, 10) - 1, parseInt(d, 10));
      return new Intl.DateTimeFormat("en-GB", {
        year: "numeric",
        month: "short",
        day: "2-digit",
      }).format(date);
    } catch {
      return dateOnlyStr;
    }
  };

  const formatCurrency = (amount: number, curr = "LKR") => {
    return `${curr} ${amount.toLocaleString("en-US", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;
  };

  const formatGrams = (grams: number) => {
    return `${grams.toFixed(4)} g`;
  };

  return (
    <AdminLayout>
      <div className="space-y-6 pb-16">
        {/* Header and Actions */}
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-neutral-100">
          <div>
            <div className="flex items-center gap-3">
              <div className="p-2.5 rounded-xl bg-[#0B3D0B]/10 text-[#0B3D0B]">
                <CreditCard className="w-6 h-6" />
              </div>
              <div>
                <h1 className="text-2xl font-bold text-neutral-900 tracking-tight">
                  Jewellery Plan Payments
                </h1>
                <p className="text-sm text-neutral-500">
                  Authoritative payments ledger, confirmed grams credit, and duration management.
                </p>
              </div>
            </div>
          </div>

          {/* Refresh controls & Last updated */}
          <div className="flex items-center gap-3 self-start md:self-auto">
            <div className="flex flex-col items-end text-xs text-neutral-500">
              <div className="flex items-center gap-1.5 font-medium text-neutral-700">
                <span className="relative flex h-2 w-2">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
                </span>
                Live 15s polling
              </div>
              <span>Refreshed: {lastRefreshedAt.toLocaleTimeString()}</span>
            </div>

            <button
              onClick={() => fetchData(false)}
              disabled={isRefreshing || initialLoading}
              className="inline-flex items-center gap-2 px-3.5 py-2 text-sm font-semibold rounded-xl bg-neutral-100 hover:bg-neutral-200 text-neutral-800 transition-colors disabled:opacity-50"
              title="Manual Data Refresh"
            >
              <RefreshCw
                className={`w-4 h-4 text-[#0B3D0B] ${isRefreshing ? "animate-spin" : ""}`}
              />
              <span>Refresh</span>
            </button>
          </div>
        </div>

        {/* Non-blocking background refresh warning */}
        {refreshError && (
          <div className="flex items-center gap-2.5 p-3.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs">
            <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0" />
            <span>
              Background refresh notice: {refreshError}. Preserving current displayed records.
            </span>
          </div>
        )}

        {/* Navigation Tabs */}
        <div className="flex items-center gap-2 border-b border-neutral-200">
          <button
            onClick={() => handleTabChange("history")}
            className={`flex items-center gap-2.5 px-5 py-3 font-semibold text-sm border-b-2 transition-all ${
              activeTab === "history"
                ? "border-[#0B3D0B] text-[#0B3D0B]"
                : "border-transparent text-neutral-500 hover:text-neutral-800"
            }`}
          >
            <CreditCard className="w-4 h-4" />
            <span>Payment History</span>
            {historyData && (
              <span className="px-2 py-0.5 text-xs rounded-full bg-[#0B3D0B]/10 text-[#0B3D0B] font-bold">
                {historyData.totalCount}
              </span>
            )}
          </button>

          <button
            onClick={() => handleTabChange("incomplete")}
            className={`flex items-center gap-2.5 px-5 py-3 font-semibold text-sm border-b-2 transition-all ${
              activeTab === "incomplete"
                ? "border-[#0B3D0B] text-[#0B3D0B]"
                : "border-transparent text-neutral-500 hover:text-neutral-800"
            }`}
          >
            <AlertCircle className="w-4 h-4 text-amber-600" />
            <span>Incomplete Plans</span>
            {incompleteData && (
              <span className="px-2 py-0.5 text-xs rounded-full bg-amber-100 text-amber-800 font-bold">
                {incompleteData.totalCount}
              </span>
            )}
          </button>
        </div>

        {/* Pre-pagination Summary KPI Cards */}
        {activeTab === "history" && historyData && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm flex flex-col justify-between">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Confirmed Collected</span>
                <div className="p-2 rounded-lg bg-emerald-50 text-emerald-700">
                  <DollarSign className="w-4 h-4" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-neutral-900">
                  {formatCurrency(historyData.totalConfirmedAmount, historyData.currency)}
                </div>
                <p className="text-xs text-neutral-500 mt-1">
                  Across {historyData.totalSuccessfulCount} successful payments
                </p>
              </div>
            </div>

            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm flex flex-col justify-between">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Confirmed Gold Credited</span>
                <div className="p-2 rounded-lg bg-amber-50 text-[#0B3D0B]">
                  <TrendingUp className="w-4 h-4 text-amber-600" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-neutral-900">
                  {formatGrams(historyData.totalConfirmedGrams)}
                </div>
                <p className="text-xs text-neutral-500 mt-1">
                  Net 24K gold credited authoritatively
                </p>
              </div>
            </div>

            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm flex flex-col justify-between">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Pending Attempts</span>
                <div className="p-2 rounded-lg bg-amber-100 text-amber-800">
                  <Clock className="w-4 h-4" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-amber-900">
                  {formatCurrency(historyData.totalPendingAmount, historyData.currency)}
                </div>
                <p className="text-xs text-amber-700 mt-1">
                  {historyData.totalPendingCount} pending attempt(s) (0g credited)
                </p>
              </div>
            </div>

            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm flex flex-col justify-between">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Scope & Currency</span>
                <div className="p-2 rounded-lg bg-blue-50 text-blue-700">
                  <Info className="w-4 h-4" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-lg font-bold text-neutral-900">
                  {selectedBranchId
                    ? branches.find((b) => b.id === selectedBranchId)?.name || "Selected Branch"
                    : "Permitted Branches"}
                </div>
                <p className="text-xs text-neutral-500 mt-1">Currency: {historyData.currency}</p>
              </div>
            </div>
          </div>
        )}

        {activeTab === "incomplete" && incompleteData && (
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Incomplete Enrolments</span>
                <div className="p-2 rounded-lg bg-red-50 text-red-700">
                  <AlertCircle className="w-4 h-4" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-neutral-900">
                  {incompleteData.totalIncompleteEnrolments}
                </div>
                <p className="text-xs text-neutral-500 mt-1">
                  Duration ended below agreed target snapshot
                </p>
              </div>
            </div>

            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Total Remaining Target</span>
                <div className="p-2 rounded-lg bg-amber-50 text-amber-700">
                  <TrendingUp className="w-4 h-4" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-neutral-900">
                  {formatGrams(incompleteData.totalRemainingGrams)}
                </div>
                <p className="text-xs text-neutral-500 mt-1">
                  Unfinished target balance across expired plans
                </p>
              </div>
            </div>

            <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm">
              <div className="flex items-center justify-between text-neutral-500 text-xs font-semibold uppercase tracking-wider">
                <span>Customer Money Paid</span>
                <div className="p-2 rounded-lg bg-emerald-50 text-emerald-700">
                  <DollarSign className="w-4 h-4" />
                </div>
              </div>
              <div className="mt-3">
                <div className="text-2xl font-bold text-neutral-900">
                  {formatCurrency(
                    incompleteData.totalConfirmedMoneyPaid,
                    incompleteData.currency
                  )}
                </div>
                <p className="text-xs text-neutral-500 mt-1">
                  Preserved safely under settlement rules
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Filter Toolbar */}
        <div className="bg-white p-4 rounded-2xl border border-neutral-100 shadow-sm space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
            {/* Search */}
            <div className="relative">
              <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-neutral-400" />
              <input
                type="text"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search customer, phone, reference..."
                className="w-full pl-9 pr-3 py-2 text-sm border border-neutral-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:border-transparent"
              />
            </div>

            {/* Plan filter */}
            <div>
              <select
                value={selectedPlanId}
                onChange={(e) => {
                  setSelectedPlanId(e.target.value);
                  setPage(1);
                }}
                className="w-full px-3 py-2 text-sm border border-neutral-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] bg-white text-neutral-700"
              >
                <option value="">All Plans</option>
                {plans.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name} ({p.code})
                  </option>
                ))}
              </select>
            </div>

            {/* Status filter (Only for History tab) */}
            {activeTab === "history" && (
              <div>
                <select
                  value={statusFilter}
                  onChange={(e) => {
                    setStatusFilter(e.target.value);
                    setPage(1);
                  }}
                  className="w-full px-3 py-2 text-sm border border-neutral-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] bg-white text-neutral-700"
                >
                  <option value="All">All Statuses</option>
                  <option value="Successful">Successful Only</option>
                  <option value="Pending">Pending Only</option>
                </select>
              </div>
            )}

            {/* Branch selector (if staff has multi-branch access) */}
            {(isSuperAdmin || branches.length > 1) && (
              <div>
                <select
                  value={selectedBranchId}
                  onChange={(e) => {
                    setSelectedBranchId(e.target.value);
                    setPage(1);
                  }}
                  className="w-full px-3 py-2 text-sm border border-neutral-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] bg-white text-neutral-700"
                >
                  <option value="">All Permitted Branches</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* Date range row (for history tab) */}
          {activeTab === "history" && (
            <div className="flex flex-wrap items-center gap-3 pt-2 border-t border-neutral-100 text-xs text-neutral-500">
              <span className="font-semibold text-neutral-700 flex items-center gap-1.5">
                <Calendar className="w-3.5 h-3.5 text-neutral-400" />
                Date Range (Branch Timezone):
              </span>
              <div className="flex items-center gap-2">
                <label className="text-neutral-500">From:</label>
                <input
                  type="date"
                  value={fromDate}
                  onChange={(e) => {
                    setFromDate(e.target.value);
                    setPage(1);
                  }}
                  className="px-2.5 py-1 text-xs border border-neutral-200 rounded-lg focus:outline-none focus:ring-1 focus:ring-[#0B3D0B]"
                />
              </div>
              <div className="flex items-center gap-2">
                <label className="text-neutral-500">To:</label>
                <input
                  type="date"
                  value={toDate}
                  onChange={(e) => {
                    setToDate(e.target.value);
                    setPage(1);
                  }}
                  className="px-2.5 py-1 text-xs border border-neutral-200 rounded-lg focus:outline-none focus:ring-1 focus:ring-[#0B3D0B]"
                />
              </div>

              {(search || selectedPlanId || statusFilter !== "All" || fromDate || toDate || selectedBranchId) && (
                <button
                  onClick={() => {
                    setSearch("");
                    setSelectedPlanId("");
                    setStatusFilter("All");
                    setFromDate("");
                    setToDate("");
                    setSelectedBranchId("");
                    setPage(1);
                  }}
                  className="ml-auto text-xs font-semibold text-red-600 hover:text-red-700"
                >
                  Clear Filters
                </button>
              )}
            </div>
          )}
        </div>

        {/* Error banner on initial failure */}
        {initialError && (
          <div className="p-6 rounded-2xl bg-red-50 border border-red-200 text-red-800 flex flex-col items-center justify-center text-center gap-3">
            <AlertCircle className="w-8 h-8 text-red-600" />
            <div>
              <h3 className="font-bold text-base">Failed to load data</h3>
              <p className="text-sm text-red-600 mt-1">{initialError}</p>
            </div>
            <button
              onClick={() => fetchData(false)}
              className="px-4 py-2 bg-red-600 text-white font-semibold text-sm rounded-xl hover:bg-red-700 transition-colors"
            >
              Try Again
            </button>
          </div>
        )}

        {/* Loading skeleton */}
        {initialLoading && (
          <div className="bg-white rounded-2xl border border-neutral-100 p-8 flex flex-col items-center justify-center min-h-[300px] text-neutral-400">
            <RefreshCw className="w-8 h-8 animate-spin text-[#0B3D0B] mb-3" />
            <p className="text-sm font-medium">Loading jewellery plan payments records...</p>
          </div>
        )}

        {/* Payment History Table */}
        {!initialLoading && !initialError && activeTab === "history" && historyData && (
          <div className="bg-white rounded-2xl border border-neutral-100 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-sm">
                <thead>
                  <tr className="bg-neutral-50/75 border-b border-neutral-200 text-xs font-bold text-neutral-600 uppercase tracking-wider">
                    <th className="py-3.5 px-4">Instalment</th>
                    <th className="py-3.5 px-4">Customer</th>
                    <th className="py-3.5 px-4">Plan & Enrolment</th>
                    <th className="py-3.5 px-4">Amount</th>
                    <th className="py-3.5 px-4">Gold Rate</th>
                    <th className="py-3.5 px-4">Gold Credit</th>
                    <th className="py-3.5 px-4">Status</th>
                    <th className="py-3.5 px-4">
                      Due Date
                      <span
                        className="ml-1 text-neutral-400 font-normal lowercase cursor-help"
                        title="The enrolment target deadline date (not a monthly bill date)"
                      >
                        (deadline)
                      </span>
                    </th>
                    <th className="py-3.5 px-4">Paid At</th>
                    <th className="py-3.5 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-neutral-100">
                  {historyData.items.length === 0 ? (
                    <tr>
                      <td colSpan={10} className="py-12 text-center text-neutral-400">
                        <CreditCard className="w-10 h-10 mx-auto text-neutral-300 mb-2" />
                        <p className="font-semibold text-neutral-700">No payment records found</p>
                        <p className="text-xs text-neutral-400 mt-1">
                          Try adjusting search keywords, date range, or status filters.
                        </p>
                      </td>
                    </tr>
                  ) : (
                    historyData.items.map((item) => {
                      const isSuccess = item.status === "Successful";

                      return (
                        <tr
                          key={item.paymentId}
                          className="hover:bg-neutral-50/60 transition-colors"
                        >
                          {/* Instalment */}
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            {isSuccess ? (
                              <span className="inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-bold bg-[#0B3D0B]/10 text-[#0B3D0B]">
                                {item.instalmentLabel}
                              </span>
                            ) : (
                              <span className="inline-flex items-center px-2.5 py-1 rounded-lg text-xs font-semibold bg-amber-100 text-amber-800">
                                {item.instalmentLabel}
                              </span>
                            )}
                          </td>

                          {/* Customer */}
                          <td className="py-3.5 px-4">
                            <Link
                              href={`/customers/${item.customerId}`}
                              className="font-bold text-neutral-900 hover:text-[#0B3D0B] hover:underline flex items-center gap-1 group"
                            >
                              <span>{item.customerName}</span>
                              <ExternalLink className="w-3 h-3 text-neutral-400 group-hover:text-[#0B3D0B]" />
                            </Link>
                            <div className="text-xs text-neutral-400 mt-0.5">
                              {item.customerPhone || item.customerEmail || "No contact info"}
                            </div>
                          </td>

                          {/* Plan */}
                          <td className="py-3.5 px-4">
                            <div className="font-medium text-neutral-800">{item.planName}</div>
                            <span className="inline-block mt-0.5 px-2 py-0.5 text-[11px] font-mono bg-neutral-100 text-neutral-600 rounded">
                              {item.enrolmentReference}
                            </span>
                          </td>

                          {/* Amount */}
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            <div className="font-bold text-neutral-900">
                              {formatCurrency(item.amount, item.currency)}
                            </div>
                            <div className="text-xs text-neutral-400">
                              Method: {item.paymentMethod}
                            </div>
                          </td>

                          {/* Gold Rate */}
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            <div className="text-xs font-medium text-neutral-700">
                              {item.goldRate > 0
                                ? `${item.currency} ${item.goldRate.toLocaleString()}/g`
                                : "—"}
                            </div>
                          </td>

                          {/* Gold Credit */}
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            {isSuccess ? (
                              <span className="font-bold text-[#0B3D0B]">
                                +{formatGrams(item.goldCredit)}
                              </span>
                            ) : (
                              <span className="text-xs text-neutral-400 font-mono">
                                0.0000 g
                                {item.estimatedPendingGoldCredit ? (
                                  <span className="block text-[10px] text-amber-600 font-normal">
                                    (~{formatGrams(item.estimatedPendingGoldCredit)} est)
                                  </span>
                                ) : null}
                              </span>
                            )}
                          </td>

                          {/* Status */}
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            {isSuccess ? (
                              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800">
                                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
                                Successful
                              </span>
                            ) : (
                              <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-bold bg-amber-100 text-amber-800">
                                <Clock className="w-3.5 h-3.5 text-amber-600" />
                                Pending
                              </span>
                            )}
                          </td>

                          {/* Due Date (Enrolment Deadline) */}
                          <td className="py-3.5 px-4 whitespace-nowrap text-xs text-neutral-600">
                            {formatDateOnly(item.dueDate)}
                          </td>

                          {/* Paid At */}
                          <td className="py-3.5 px-4 whitespace-nowrap text-xs text-neutral-600">
                            {isSuccess
                              ? formatDateTime(item.paidAtUtc, item.branchTimezone)
                              : "—"}
                          </td>

                          {/* Actions */}
                          <td className="py-3.5 px-4 text-right whitespace-nowrap">
                            <div className="flex items-center justify-end gap-2">
                              {item.canSendReminder ? (
                                <button
                                  onClick={() => {
                                    router.push(
                                      `/notifications?enrolmentId=${item.enrolmentId}&customerId=${item.customerId}&reason=${item.reminderReason || "NoPaymentForThreeMonths"}`
                                    );
                                  }}
                                  className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-amber-500 hover:bg-amber-600 text-white transition-colors"
                                  title="Inactivity threshold reached. Open reminder draft composer."
                                >
                                  <Send className="w-3 h-3" />
                                  <span>Send Reminder</span>
                                </button>
                              ) : null}

                              <Link
                                href={`/customers/${item.customerId}`}
                                className="p-1.5 text-neutral-400 hover:text-neutral-700 hover:bg-neutral-100 rounded-lg transition-colors"
                                title="View Customer Details"
                              >
                                <ExternalLink className="w-4 h-4" />
                              </Link>
                            </div>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination footer */}
            {historyData.totalPages > 1 && (
              <div className="px-6 py-4 bg-neutral-50/70 border-t border-neutral-200 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-neutral-600">
                <div>
                  Showing page <span className="font-bold text-neutral-900">{historyData.page}</span>{" "}
                  of <span className="font-bold text-neutral-900">{historyData.totalPages}</span> (
                  {historyData.totalCount} total entries)
                </div>

                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={historyData.page <= 1}
                    className="p-1.5 rounded-lg border border-neutral-300 bg-white hover:bg-neutral-50 disabled:opacity-40 disabled:pointer-events-none"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>

                  <span className="font-semibold text-neutral-700 px-2">
                    {historyData.page} / {historyData.totalPages}
                  </span>

                  <button
                    onClick={() => setPage((p) => Math.min(historyData.totalPages, p + 1))}
                    disabled={historyData.page >= historyData.totalPages}
                    className="p-1.5 rounded-lg border border-neutral-300 bg-white hover:bg-neutral-50 disabled:opacity-40 disabled:pointer-events-none"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Incomplete Plans Table */}
        {!initialLoading && !initialError && activeTab === "incomplete" && incompleteData && (
          <div className="bg-white rounded-2xl border border-neutral-100 shadow-sm overflow-hidden">
            <div className="p-4 bg-amber-50/60 border-b border-amber-100 flex items-start gap-3">
              <Info className="w-5 h-5 text-amber-600 flex-shrink-0 mt-0.5" />
              <div className="text-xs text-amber-900">
                <span className="font-bold">Incomplete Enrolments:</span> These jewellery plan enrolments
                reached their agreed duration end date without meeting the target gold grams. Note that
                customers do not owe a currency debt; remind customers to visit the branch or review
                their plan under existing settlement guidelines.
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-sm">
                <thead>
                  <tr className="bg-neutral-50/75 border-b border-neutral-200 text-xs font-bold text-neutral-600 uppercase tracking-wider">
                    <th className="py-3.5 px-4">Customer</th>
                    <th className="py-3.5 px-4">Plan & Identifier</th>
                    <th className="py-3.5 px-4">Joining Date</th>
                    <th className="py-3.5 px-4">Duration</th>
                    <th className="py-3.5 px-4">Deadline</th>
                    <th className="py-3.5 px-4">Target</th>
                    <th className="py-3.5 px-4">Saved</th>
                    <th className="py-3.5 px-4">Remaining</th>
                    <th className="py-3.5 px-4">Money Paid</th>
                    <th className="py-3.5 px-4">Progress</th>
                    <th className="py-3.5 px-4">Status</th>
                    <th className="py-3.5 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-neutral-100">
                  {incompleteData.items.length === 0 ? (
                    <tr>
                      <td colSpan={12} className="py-12 text-center text-neutral-400">
                        <CheckCircle2 className="w-10 h-10 mx-auto text-emerald-400 mb-2" />
                        <p className="font-semibold text-neutral-700">No incomplete enrolments</p>
                        <p className="text-xs text-neutral-400 mt-1">
                          All ended enrolments in scope have achieved their target or have been settled.
                        </p>
                      </td>
                    </tr>
                  ) : (
                    incompleteData.items.map((item) => (
                      <tr key={item.enrolmentId} className="hover:bg-neutral-50/60 transition-colors">
                        {/* Customer */}
                        <td className="py-3.5 px-4">
                          <Link
                            href={`/customers/${item.customerId}`}
                            className="font-bold text-neutral-900 hover:text-[#0B3D0B] hover:underline flex items-center gap-1 group"
                          >
                            <span>{item.customerName}</span>
                            <ExternalLink className="w-3 h-3 text-neutral-400 group-hover:text-[#0B3D0B]" />
                          </Link>
                          <div className="text-xs text-neutral-400 mt-0.5">
                            {item.customerPhone || item.customerEmail || "—"}
                          </div>
                        </td>

                        {/* Plan */}
                        <td className="py-3.5 px-4">
                          <div className="font-medium text-neutral-800">{item.planName}</div>
                          <span className="inline-block mt-0.5 px-2 py-0.5 text-[11px] font-mono bg-neutral-100 text-neutral-600 rounded">
                            {item.enrolmentReference}
                          </span>
                        </td>

                        {/* Joining Date */}
                        <td className="py-3.5 px-4 whitespace-nowrap text-xs text-neutral-600">
                          {formatDateOnly(item.joiningDate)}
                        </td>

                        {/* Duration */}
                        <td className="py-3.5 px-4 whitespace-nowrap text-xs text-neutral-700 font-medium">
                          {item.selectedDurationMonths} mos
                        </td>

                        {/* Deadline */}
                        <td className="py-3.5 px-4 whitespace-nowrap text-xs font-semibold text-amber-700">
                          {formatDateOnly(item.deadline)}
                        </td>

                        {/* Target */}
                        <td className="py-3.5 px-4 whitespace-nowrap font-medium text-neutral-700">
                          {formatGrams(item.targetGrams)}
                        </td>

                        {/* Net Saved */}
                        <td className="py-3.5 px-4 whitespace-nowrap font-semibold text-[#0B3D0B]">
                          {formatGrams(item.netSavedGrams)}
                        </td>

                        {/* Remaining */}
                        <td className="py-3.5 px-4 whitespace-nowrap font-bold text-red-600">
                          {formatGrams(item.remainingGrams)}
                        </td>

                        {/* Total Money Paid */}
                        <td className="py-3.5 px-4 whitespace-nowrap font-semibold text-neutral-900">
                          {formatCurrency(item.totalConfirmedMoneyPaid, item.currency)}
                        </td>

                        {/* Progress */}
                        <td className="py-3.5 px-4 whitespace-nowrap">
                          <div className="w-24">
                            <div className="flex items-center justify-between text-[11px] font-bold text-neutral-600 mb-1">
                              <span>{item.progressPercentage.toFixed(1)}%</span>
                            </div>
                            <div className="w-full bg-neutral-100 rounded-full h-1.5 overflow-hidden">
                              <div
                                className="bg-[#0B3D0B] h-1.5 rounded-full"
                                style={{ width: `${Math.min(100, item.progressPercentage)}%` }}
                              />
                            </div>
                          </div>
                        </td>

                        {/* Status */}
                        <td className="py-3.5 px-4 whitespace-nowrap">
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-amber-100 text-amber-800">
                            Duration Ended
                          </span>
                        </td>

                        {/* Actions */}
                        <td className="py-3.5 px-4 text-right whitespace-nowrap">
                          <div className="flex items-center justify-end gap-2">
                            <button
                              onClick={() => {
                                router.push(
                                  `/notifications?enrolmentId=${item.enrolmentId}&customerId=${item.customerId}&reason=PlanIncomplete`
                                );
                              }}
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-semibold rounded-lg bg-[#0B3D0B] hover:bg-[#0B3D0B]/90 text-white transition-colors"
                              title="Open Reminder Composer for incomplete plan"
                            >
                              <Send className="w-3 h-3 text-[#FFD700]" />
                              <span>Send Reminder</span>
                            </button>

                            <Link
                              href={`/customers/${item.customerId}`}
                              className="p-1.5 text-neutral-400 hover:text-neutral-700 hover:bg-neutral-100 rounded-lg transition-colors"
                              title="View Customer"
                            >
                              <ExternalLink className="w-4 h-4" />
                            </Link>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination footer */}
            {incompleteData.totalPages > 1 && (
              <div className="px-6 py-4 bg-neutral-50/70 border-t border-neutral-200 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-neutral-600">
                <div>
                  Showing page <span className="font-bold text-neutral-900">{incompleteData.page}</span>{" "}
                  of <span className="font-bold text-neutral-900">{incompleteData.totalPages}</span> (
                  {incompleteData.totalCount} total entries)
                </div>

                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={incompleteData.page <= 1}
                    className="p-1.5 rounded-lg border border-neutral-300 bg-white hover:bg-neutral-50 disabled:opacity-40 disabled:pointer-events-none"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>

                  <span className="font-semibold text-neutral-700 px-2">
                    {incompleteData.page} / {incompleteData.totalPages}
                  </span>

                  <button
                    onClick={() => setPage((p) => Math.min(incompleteData.totalPages, p + 1))}
                    disabled={incompleteData.page >= incompleteData.totalPages}
                    className="p-1.5 rounded-lg border border-neutral-300 bg-white hover:bg-neutral-50 disabled:opacity-40 disabled:pointer-events-none"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </AdminLayout>
  );
}

export default function JewelleryPaymentsPage() {
  return (
    <Suspense
      fallback={
        <AdminLayout>
          <div className="p-8 flex items-center justify-center min-h-[400px]">
            <RefreshCw className="w-8 h-8 animate-spin text-[#0B3D0B]" />
          </div>
        </AdminLayout>
      }
    >
      <PaymentsContent />
    </Suspense>
  );
}
