"use client";

import React, { useState, useEffect, useCallback } from "react";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, ApiError } from "../../lib/api";
import { useAuth } from "../../lib/auth-context";
import {
  CustomerListItem,
  CustomerDetail,
  CustomerStatistics,
  CreateCustomerRequest,
} from "../../types/customer";
import { Branch } from "../../types/auth";
import {
  Search,
  Filter,
  RefreshCw,
  Eye,
  CheckCircle2,
  XCircle,
  AlertCircle,
  Building2,
  Users,
  UserCheck,
  UserX,
  UserPlus,
  ChevronLeft,
  ChevronRight,
  X,
  Shield,
  Calendar,
  Phone,
  Mail,
  CreditCard,
  Gem,
  Info,
  Power,
  RotateCcw,
} from "lucide-react";

export default function CustomersPage() {
  const { user, isSuperAdmin, isBranchAdmin } = useAuth();

  // State: Customer Data & Statistics
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [statistics, setStatistics] = useState<CustomerStatistics>({
    totalCustomers: 0,
    activeCustomers: 0,
    inactiveCustomers: 0,
  });
  const [branches, setBranches] = useState<Branch[]>([]);

  // State: Filters & Pagination
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedBranch, setSelectedBranch] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<string>("ALL"); // 'ALL', 'Active', 'Inactive'
  const [page, setPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);
  const [totalPages, setTotalPages] = useState<number>(1);
  const [totalCount, setTotalCount] = useState<number>(0);

  // State: Status & Feedback
  const [loading, setLoading] = useState<boolean>(true);
  const [statsLoading, setStatsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // State: Customer Detail Modal
  const [detailCustomer, setDetailCustomer] = useState<CustomerDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState<boolean>(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  // State: Create Customer Modal
  const [showCreateModal, setShowCreateModal] = useState<boolean>(false);
  const [createForm, setCreateForm] = useState<CreateCustomerRequest>({
    fullName: "",
    phoneNumber: "",
    email: "",
    nic: "",
    branchId: "",
  });
  const [createErrors, setCreateErrors] = useState<Record<string, string>>({});
  const [createSubmitLoading, setCreateSubmitLoading] = useState<boolean>(false);
  const [createSubmitError, setCreateSubmitError] = useState<string | null>(null);

  // State: Activate / Deactivate Confirmation Modal
  const [statusActionCustomer, setStatusActionCustomer] = useState<CustomerListItem | null>(null);
  const [statusActionReason, setStatusActionReason] = useState<string>("");
  const [statusActionReasonError, setStatusActionReasonError] = useState<string | null>(null);
  const [statusActionLoading, setStatusActionLoading] = useState<boolean>(false);
  const [statusActionError, setStatusActionError] = useState<string | null>(null);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, 350);
    return () => clearTimeout(timer);
  }, [search]);

  // Load branches metadata
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

  // Fetch summary statistics
  // Consistent branch & search filters applied across matching records before pagination.
  // Active and Inactive counts remain separate and independent of the table's statusFilter.
  const fetchStatistics = useCallback(async () => {
    setStatsLoading(true);
    try {
      const data = await adminApi.getCustomerStatistics({
        search: debouncedSearch.trim() || undefined,
        branchId: selectedBranch || undefined,
      });
      setStatistics(data);
    } catch (err) {
      console.error("Failed to fetch customer statistics", err);
    } finally {
      setStatsLoading(false);
    }
  }, [debouncedSearch, selectedBranch]);

  // Fetch paginated customer list
  const fetchCustomers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getCustomers({
        page,
        pageSize,
        search: debouncedSearch.trim() || undefined,
        branchId: selectedBranch || undefined,
        status: statusFilter === "ALL" ? undefined : statusFilter,
      });

      setCustomers(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages || 1);
    } catch (err: any) {
      const message =
        err instanceof ApiError
          ? err.message
          : "Unable to load customer list. Please check your connection and try again.";
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, debouncedSearch, selectedBranch, statusFilter]);

  useEffect(() => {
    fetchStatistics();
  }, [fetchStatistics]);

  useEffect(() => {
    fetchCustomers();
  }, [fetchCustomers]);

  // Check if current user is permitted to manage status for a customer
  const canManageCustomerStatus = (c: CustomerListItem): boolean => {
    if (isSuperAdmin) return true;
    if (isBranchAdmin || user?.assignedBranches?.length) {
      return (
        user?.assignedBranches?.some((b) => b.branchId === c.primaryBranchId) ?? false
      );
    }
    return false;
  };

  // Open detail view (fetches full unmasked NIC and profile)
  const handleOpenDetail = async (customerId: string) => {
    setDetailLoading(true);
    setDetailError(null);
    setDetailCustomer(null);
    try {
      const detail = await adminApi.getCustomerDetail(customerId);
      setDetailCustomer(detail);
    } catch (err: any) {
      setDetailError(
        err?.message || "Failed to load customer profile details."
      );
    } finally {
      setDetailLoading(false);
    }
  };

  const handleCloseDetail = () => {
    setDetailCustomer(null);
    setDetailError(null);
  };

  // Handle open create customer modal
  const handleOpenCreateModal = () => {
    setCreateErrors({});
    setCreateSubmitError(null);
    // Pre-select user branch if not super admin
    const defaultBranchId =
      !isSuperAdmin && user?.assignedBranches?.[0]
        ? user.assignedBranches[0].branchId
        : "";
    setCreateForm((prev) => ({
      ...prev,
      branchId: prev.branchId || defaultBranchId,
    }));
    setShowCreateModal(true);
  };

  // Validate create customer form client-side
  const validateCreateForm = (): boolean => {
    const errors: Record<string, string> = {};
    if (!createForm.fullName.trim() || createForm.fullName.trim().length < 2) {
      errors.fullName = "Full name must be at least 2 characters.";
    }
    if (!createForm.phoneNumber.trim()) {
      errors.phoneNumber = "Phone number is required.";
    } else if (createForm.phoneNumber.trim().length < 8) {
      errors.phoneNumber = "Please enter a valid phone number.";
    }
    if (!createForm.email.trim()) {
      errors.email = "Email address is required.";
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(createForm.email.trim())) {
      errors.email = "Please enter a valid email address.";
    }
    if (!createForm.nic.trim()) {
      errors.nic = "National Identity Card (NIC) is required.";
    } else if (createForm.nic.trim().length < 5) {
      errors.nic = "NIC must be at least 5 characters.";
    }
    if (isSuperAdmin && !createForm.branchId) {
      errors.branchId = "Please select a branch.";
    }

    setCreateErrors(errors);
    return Object.keys(errors).length === 0;
  };

  // Submit Create Customer
  const handleCreateCustomerSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateCreateForm()) return;

    setCreateSubmitLoading(true);
    setCreateSubmitError(null);

    try {
      await adminApi.createCustomer({
        fullName: createForm.fullName.trim(),
        phoneNumber: createForm.phoneNumber.trim(),
        email: createForm.email.trim().toLowerCase(),
        nic: createForm.nic.trim().toUpperCase(),
        branchId: createForm.branchId || undefined,
      });

      // Reset form and close modal
      setCreateForm({
        fullName: "",
        phoneNumber: "",
        email: "",
        nic: "",
        branchId: "",
      });
      setShowCreateModal(false);
      setSuccessMessage("Customer profile created successfully! Contact is awaiting customer OTP verification.");
      setTimeout(() => setSuccessMessage(null), 6000);

      // Refresh list and statistics
      fetchStatistics();
      fetchCustomers();
    } catch (err: any) {
      // Preserve form values on error!
      const errorMsg =
        err instanceof ApiError
          ? err.message
          : err?.message || "Failed to create customer. Please check field values.";
      setCreateSubmitError(errorMsg);
    } finally {
      setCreateSubmitLoading(false);
    }
  };

  // Open Status Action Modal (Activate / Deactivate)
  const handleOpenStatusAction = (customer: CustomerListItem) => {
    setStatusActionCustomer(customer);
    setStatusActionReason("");
    setStatusActionReasonError(null);
    setStatusActionError(null);
  };

  // Confirm Status Action Submit
  const handleStatusActionSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!statusActionCustomer) return;

    const isDeactivating = statusActionCustomer.isActive;

    // Require reason when deactivating
    if (isDeactivating && !statusActionReason.trim()) {
      setStatusActionReasonError("A deactivation reason is required to maintain audit records.");
      return;
    }

    setStatusActionLoading(true);
    setStatusActionError(null);

    try {
      await adminApi.updateCustomerStatus(statusActionCustomer.id, {
        isActive: !isDeactivating,
        reason: statusActionReason.trim() || undefined,
      });

      const customerName = statusActionCustomer.fullName;
      setStatusActionCustomer(null);
      setSuccessMessage(
        isDeactivating
          ? `Customer "${customerName}" has been deactivated. All active sessions and tokens have been revoked.`
          : `Customer "${customerName}" has been reactivated successfully.`
      );
      setTimeout(() => setSuccessMessage(null), 6000);

      // Refresh list and statistics
      fetchStatistics();
      fetchCustomers();
    } catch (err: any) {
      const msg =
        err instanceof ApiError
          ? err.message
          : err?.message || "Failed to update customer status.";
      setStatusActionError(msg);
    } finally {
      setStatusActionLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-6">
        {/* ================= PAGE HEADER & ACTIONS ================= */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-2 border-b border-gray-200">
          <div>
            <h1 className="text-2xl sm:text-3xl font-extrabold text-[#0B3D0B] tracking-tight">
              Customers Directory
            </h1>
            <p className="text-sm text-gray-600 mt-1">
              Customer identity management, branch assignments, and account lifecycle control.
            </p>
          </div>

          <div className="flex items-center gap-3">
            {/* Quick Refresh */}
            <button
              onClick={() => {
                fetchStatistics();
                fetchCustomers();
              }}
              disabled={loading || statsLoading}
              className="flex items-center gap-2 px-3.5 py-2.5 text-xs font-semibold rounded-xl bg-white border border-gray-300 text-gray-700 hover:bg-gray-50 hover:border-gray-400 transition-all shadow-2xs cursor-pointer disabled:opacity-50"
              title="Refresh customer data"
            >
              <RefreshCw
                className={`w-3.5 h-3.5 text-[#0B3D0B] ${
                  loading || statsLoading ? "animate-spin" : ""
                }`}
              />
              <span className="hidden sm:inline">Refresh</span>
            </button>

            {/* Create Customer Button */}
            <button
              onClick={handleOpenCreateModal}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl btn-primary-green text-xs font-bold cursor-pointer shadow-sm"
            >
              <UserPlus className="w-4 h-4 text-[#FFD700]" />
              <span>Create Customer</span>
            </button>
          </div>
        </div>

        {/* Success Alert Banner */}
        {successMessage && (
          <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 flex items-start gap-3 transition-all animate-fadeIn">
            <CheckCircle2 className="w-5 h-5 text-emerald-600 flex-shrink-0 mt-0.5" />
            <div className="flex-1 text-xs font-semibold text-emerald-900">
              {successMessage}
            </div>
            <button
              onClick={() => setSuccessMessage(null)}
              className="text-emerald-500 hover:text-emerald-700 text-xs cursor-pointer"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* ================= SUMMARY STATISTIC CARDS ================= */}
        {/*
          Database-backed summary cards: All Customers, Active Customers and Inactive Customers.
          Customer account status (IsActive) determines active/inactive, not plan or contact status.
          Calculated across matching records before pagination.
          Keep Active and Inactive cards as separate counts so a status filter does not make the other count misleading.
        */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
          {/* Card 1: All Customers */}
          <div className="bg-white rounded-2xl p-5 border border-gray-200/80 shadow-xs flex flex-col justify-between hover:border-[#0B3D0B]/30 transition-all">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-gray-500 uppercase tracking-wider">
                All Customers
              </span>
              <div className="w-10 h-10 rounded-xl bg-[#0B3D0B]/10 text-[#0B3D0B] flex items-center justify-center">
                <Users className="w-5 h-5" />
              </div>
            </div>
            <div className="mt-4">
              <span className="text-3xl font-black text-gray-900 tracking-tight">
                {statsLoading ? "—" : statistics.totalCustomers}
              </span>
              <span className="text-[11px] text-gray-500 block mt-1">
                Matching search & branch scope
              </span>
            </div>
          </div>

          {/* Card 2: Active Customers */}
          <div className="bg-white rounded-2xl p-5 border border-gray-200/80 shadow-xs flex flex-col justify-between hover:border-emerald-500/40 transition-all">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-emerald-700 uppercase tracking-wider">
                Active Customers
              </span>
              <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-700 flex items-center justify-center">
                <UserCheck className="w-5 h-5" />
              </div>
            </div>
            <div className="mt-4">
              <span className="text-3xl font-black text-emerald-700 tracking-tight">
                {statsLoading ? "—" : statistics.activeCustomers}
              </span>
              <span className="text-[11px] text-gray-500 block mt-1">
                Account status Active
              </span>
            </div>
          </div>

          {/* Card 3: Inactive Customers */}
          <div className="bg-white rounded-2xl p-5 border border-gray-200/80 shadow-xs flex flex-col justify-between hover:border-red-400/40 transition-all">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-red-700 uppercase tracking-wider">
                Inactive Customers
              </span>
              <div className="w-10 h-10 rounded-xl bg-red-50 text-red-700 flex items-center justify-center">
                <UserX className="w-5 h-5" />
              </div>
            </div>
            <div className="mt-4">
              <span className="text-3xl font-black text-red-700 tracking-tight">
                {statsLoading ? "—" : statistics.inactiveCustomers}
              </span>
              <span className="text-[11px] text-gray-500 block mt-1">
                Suspended or closed accounts
              </span>
            </div>
          </div>
        </div>

        {/* Documentation / Info note on statistics behavior */}
        <div className="flex items-center gap-2 px-3 py-2 rounded-xl bg-gray-100/70 border border-gray-200 text-[11px] text-gray-600">
          <Info className="w-3.5 h-3.5 text-[#0B3D0B] flex-shrink-0" />
          <span>
            <strong>Statistics Behavior:</strong> Summary cards reflect total, active, and inactive counts matching the current search and branch filters before pagination. Active and Inactive cards remain separate counts so applying a table status filter does not make the opposite card count misleading.
          </span>
        </div>

        {/* ================= SEARCH & FILTER CONTROLS ================= */}
        <div className="bg-white rounded-xl p-4 border border-gray-200 shadow-xs flex flex-col md:flex-row items-center justify-between gap-4">
          {/* Search Input */}
          <div className="relative w-full md:max-w-md">
            <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search by name, phone, email, or NIC..."
              className="w-full pl-10 pr-12 py-2.5 rounded-xl border border-gray-300 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:border-transparent transition-all"
            />
            {search && (
              <button
                onClick={() => setSearch("")}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 text-xs px-1.5 py-0.5 rounded cursor-pointer"
              >
                Clear
              </button>
            )}
          </div>

          {/* Status Filter Tabs & Branch Filter */}
          <div className="flex flex-wrap items-center gap-3 w-full md:w-auto">
            {/* Status Segment Pills */}
            <div className="inline-flex items-center p-1 bg-gray-100 rounded-xl border border-gray-200">
              {[
                { id: "ALL", label: "All Customers" },
                { id: "Active", label: "Active" },
                { id: "Inactive", label: "Inactive" },
              ].map((tab) => {
                const isActive = statusFilter === tab.id;
                return (
                  <button
                    key={tab.id}
                    onClick={() => {
                      setStatusFilter(tab.id);
                      setPage(1);
                    }}
                    className={`px-3 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer ${
                      isActive
                        ? "bg-[#0B3D0B] text-white shadow-2xs"
                        : "text-gray-600 hover:text-gray-900"
                    }`}
                  >
                    {tab.label}
                  </button>
                );
              })}
            </div>

            {/* Branch Filter */}
            {isSuperAdmin ? (
              <div className="flex items-center gap-1.5">
                <Building2 className="w-4 h-4 text-gray-400 hidden sm:block" />
                <select
                  value={selectedBranch}
                  onChange={(e) => {
                    setSelectedBranch(e.target.value);
                    setPage(1);
                  }}
                  className="px-3 py-2 rounded-xl border border-gray-300 text-xs text-gray-800 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] cursor-pointer"
                >
                  <option value="">All Branches</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name} ({b.code})
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              user?.assignedBranches?.[0] && (
                <div className="flex items-center gap-1.5 px-3 py-2 rounded-xl bg-gray-100 border border-gray-200 text-xs font-semibold text-gray-700">
                  <Building2 className="w-3.5 h-3.5 text-[#0B3D0B]" />
                  <span>{user.assignedBranches[0].name}</span>
                </div>
              )
            )}
          </div>
        </div>

        {/* ================= PAGINATED DATA TABLE ================= */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-xs overflow-hidden">
          {/* Error Alert */}
          {error && (
            <div className="p-6 bg-red-50/70 border-b border-red-200 flex items-start gap-3">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <h3 className="text-sm font-bold text-red-900">
                  Failed to retrieve customer records
                </h3>
                <p className="text-xs text-red-700 mt-0.5">{error}</p>
                <button
                  onClick={() => fetchCustomers()}
                  className="mt-3 px-3 py-1.5 rounded-lg bg-red-600 text-white text-xs font-semibold hover:bg-red-700 transition-colors cursor-pointer"
                >
                  Retry Connection
                </button>
              </div>
            </div>
          )}

          {/* Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-gray-50/80 border-b border-gray-200 text-[11px] font-bold text-gray-500 uppercase tracking-wider">
                  <th className="py-3.5 px-4 sm:px-6">Customer</th>
                  <th className="py-3.5 px-4">Phone</th>
                  <th className="py-3.5 px-4">Email</th>
                  <th className="py-3.5 px-4">NIC</th>
                  <th className="py-3.5 px-4">Status</th>
                  <th className="py-3.5 px-4">Branch</th>
                  <th className="py-3.5 px-4">Registered</th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>

              <tbody className="divide-y divide-gray-100 text-sm">
                {/* LOADING SKELETON */}
                {loading && (
                  <>
                    {[1, 2, 3, 4, 5].map((i) => (
                      <tr key={i} className="animate-pulse">
                        <td className="py-4 px-4 sm:px-6">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-full bg-gray-200" />
                            <div className="space-y-1.5">
                              <div className="w-28 h-3.5 bg-gray-200 rounded" />
                              <div className="w-20 h-2.5 bg-gray-100 rounded" />
                            </div>
                          </div>
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-24 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-32 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-24 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-16 h-5 bg-gray-200 rounded-full" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-20 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-20 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4 text-right">
                          <div className="w-24 h-7 bg-gray-200 rounded-lg ml-auto" />
                        </td>
                      </tr>
                    ))}
                  </>
                )}

                {/* EMPTY STATE */}
                {!loading && customers.length === 0 && !error && (
                  <tr>
                    <td colSpan={8} className="py-14 px-4 text-center">
                      <div className="max-w-sm mx-auto flex flex-col items-center">
                        <div className="w-12 h-12 rounded-full bg-gray-100 flex items-center justify-center text-gray-400 mb-3">
                          <Users className="w-6 h-6" />
                        </div>
                        <h3 className="text-base font-bold text-gray-900">
                          No customers found
                        </h3>
                        <p className="text-xs text-gray-500 mt-1">
                          {debouncedSearch
                            ? `No records matched your search "${debouncedSearch}".`
                            : "There are no customer records matching the active filters."}
                        </p>
                        {(debouncedSearch || statusFilter !== "ALL" || selectedBranch) && (
                          <button
                            onClick={() => {
                              setSearch("");
                              setStatusFilter("ALL");
                              setSelectedBranch("");
                            }}
                            className="mt-4 px-3.5 py-1.5 rounded-lg bg-[#0B3D0B] text-white text-xs font-semibold hover:bg-[#124e12] transition-colors cursor-pointer"
                          >
                            Reset All Filters
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                )}

                {/* CUSTOMER ROWS */}
                {!loading &&
                  customers.map((c) => {
                    const initials = c.fullName
                      ? c.fullName
                      .split(" ")
                      .map((n) => n[0])
                      .join("")
                      .substring(0, 2)
                      .toUpperCase()
                      : "CU";

                    const registrationDate = c.createdAtUtc
                      ? new Date(c.createdAtUtc).toLocaleDateString("en-US", {
                          month: "short",
                          day: "numeric",
                          year: "numeric",
                        })
                      : "—";

                    const canManage = canManageCustomerStatus(c);

                    return (
                      <tr
                        key={c.id}
                        className="hover:bg-emerald-50/20 transition-colors group"
                      >
                        {/* Name & Initials */}
                        <td className="py-3.5 px-4 sm:px-6">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-full bg-[#0B3D0B] text-[#FFD700] font-bold text-xs flex items-center justify-center flex-shrink-0 shadow-2xs">
                              {initials}
                            </div>
                            <div className="flex flex-col min-w-0">
                              <span className="font-bold text-gray-900 group-hover:text-[#0B3D0B] transition-colors truncate">
                                {c.fullName}
                              </span>
                              <span className="text-[11px] text-gray-500">
                                {c.activePlansCount > 0
                                  ? `${c.activePlansCount} Active ${c.activePlansCount === 1 ? "Plan" : "Plans"}`
                                  : "No Active Plans"}
                              </span>
                            </div>
                          </div>
                        </td>

                        {/* Phone */}
                        <td className="py-3.5 px-4 font-mono text-xs text-gray-700">
                          <div className="flex items-center gap-1.5">
                            <span>{c.phoneNumber || "—"}</span>
                            {c.isPhoneVerified && (
                              <span title="Verified phone number">
                                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 flex-shrink-0" />
                              </span>
                            )}
                          </div>
                        </td>

                        {/* Email */}
                        <td className="py-3.5 px-4 text-xs text-gray-600 truncate max-w-[170px]">
                          <div className="flex items-center gap-1.5">
                            <span className="truncate">{c.email || "—"}</span>
                            {c.isEmailVerified && (
                              <span title="Verified email address">
                                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600 flex-shrink-0" />
                              </span>
                            )}
                          </div>
                        </td>

                        {/* NIC */}
                        <td className="py-3.5 px-4 font-mono text-xs text-gray-600">
                          {c.nic ? (
                            <span className="bg-gray-100 px-2 py-0.5 rounded text-[11px]">
                              {c.nic}
                            </span>
                          ) : (
                            "—"
                          )}
                        </td>

                        {/* Status (Determined strictly by Customer.IsActive) */}
                        <td className="py-3.5 px-4">
                          {c.isActive ? (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-green-100 text-green-800 border border-green-200">
                              <span className="w-1.5 h-1.5 rounded-full bg-green-600" />
                              Active
                            </span>
                          ) : (
                            <span
                              className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-red-100 text-red-800 border border-red-200"
                              title={
                                c.closureReason
                                  ? `Closed: ${c.closureReason}`
                                  : c.deactivationReason
                                  ? `Deactivated: ${c.deactivationReason}`
                                  : "Account Inactive"
                              }
                            >
                              <span className="w-1.5 h-1.5 rounded-full bg-red-600" />
                              Inactive
                            </span>
                          )}
                        </td>

                        {/* Branch */}
                        <td className="py-3.5 px-4">
                          <span className="inline-flex items-center gap-1 text-xs text-gray-700 font-medium">
                            <Building2 className="w-3 h-3 text-[#0B3D0B]" />
                            <span className="truncate max-w-[120px]">
                              {c.primaryBranchName}
                            </span>
                          </span>
                        </td>

                        {/* Registration Date */}
                        <td className="py-3.5 px-4 text-xs text-gray-500">
                          {registrationDate}
                        </td>

                        {/* Actions */}
                        <td className="py-3.5 px-4 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            {/* View Detail Button */}
                            <button
                              onClick={() => handleOpenDetail(c.id)}
                              className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-bold text-[#0B3D0B] bg-emerald-50 hover:bg-[#0B3D0B] hover:text-white transition-all cursor-pointer border border-[#0B3D0B]/20"
                              title="View full customer profile and verification details"
                            >
                              <Eye className="w-3.5 h-3.5" />
                              <span>View</span>
                            </button>

                            {/* Activate / Deactivate Button (Permission Controlled) */}
                            {canManage && (
                              <button
                                onClick={() => handleOpenStatusAction(c)}
                                className={`inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer border ${
                                  c.isActive
                                    ? "text-red-700 bg-red-50 hover:bg-red-600 hover:text-white border-red-200"
                                    : "text-emerald-700 bg-emerald-50 hover:bg-emerald-700 hover:text-white border-emerald-200"
                                }`}
                                title={
                                  c.isActive
                                    ? "Deactivate customer account"
                                    : "Reactivate customer account"
                                }
                              >
                                <Power className="w-3 h-3" />
                                <span>{c.isActive ? "Deactivate" : "Activate"}</span>
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>

          {/* ================= PAGINATION CONTROLS ================= */}
          <div className="px-4 sm:px-6 py-4 bg-gray-50/50 border-t border-gray-200 flex flex-col sm:flex-row items-center justify-between gap-3 text-xs text-gray-600">
            <div>
              Showing{" "}
              <span className="font-bold text-gray-900">
                {customers.length === 0 ? 0 : (page - 1) * pageSize + 1}
              </span>{" "}
              to{" "}
              <span className="font-bold text-gray-900">
                {Math.min(page * pageSize, totalCount)}
              </span>{" "}
              of <span className="font-bold text-gray-900">{totalCount}</span>{" "}
              matching customers
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || loading}
                className="flex items-center gap-1 px-3 py-1.5 rounded-lg border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 font-semibold disabled:opacity-40 disabled:cursor-not-allowed transition-all cursor-pointer shadow-2xs"
              >
                <ChevronLeft className="w-3.5 h-3.5" />
                <span>Previous</span>
              </button>

              <span className="px-2 font-bold text-gray-800">
                Page {page} of {Math.max(1, totalPages)}
              </span>

              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || loading}
                className="flex items-center gap-1 px-3 py-1.5 rounded-lg border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 font-semibold disabled:opacity-40 disabled:cursor-not-allowed transition-all cursor-pointer shadow-2xs"
              >
                <span>Next</span>
                <ChevronRight className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        </div>

        {/* ================= CREATE CUSTOMER MODAL ================= */}
        {showCreateModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div
              className="fixed inset-0 bg-black/50 backdrop-blur-xs transition-opacity"
              onClick={() => !createSubmitLoading && setShowCreateModal(false)}
            />

            <div className="relative w-full max-w-lg bg-white rounded-2xl shadow-2xl border border-gray-200 overflow-hidden z-50 flex flex-col">
              {/* Header */}
              <div className="px-6 py-4 bg-[#0B3D0B] text-white flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <UserPlus className="w-5 h-5 text-[#FFD700]" />
                  <h2 className="text-base font-bold tracking-tight">
                    Create New Customer Profile
                  </h2>
                </div>
                <button
                  onClick={() => !createSubmitLoading && setShowCreateModal(false)}
                  className="p-1 rounded-lg text-emerald-200 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Form Body */}
              <form onSubmit={handleCreateCustomerSubmit} className="p-6 space-y-4">
                {/* Information banner */}
                <div className="p-3.5 rounded-xl bg-emerald-50 border border-emerald-200 text-xs text-emerald-900 leading-relaxed">
                  <strong>Registration Flow:</strong> Staff-entered contacts remain unverified until the customer verifies via OTP. A customer password is not required. When the customer logs in via mobile OTP, they will connect seamlessly to this profile.
                </div>

                {/* Server Error Alert */}
                {createSubmitError && (
                  <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 flex items-start gap-2 text-xs text-red-700">
                    <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0 mt-0.5" />
                    <span>{createSubmitError}</span>
                  </div>
                )}

                {/* Full Name */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1">
                    Full Name <span className="text-red-600">*</span>
                  </label>
                  <input
                    type="text"
                    value={createForm.fullName}
                    onChange={(e) =>
                      setCreateForm((prev) => ({ ...prev, fullName: e.target.value }))
                    }
                    placeholder="e.g. Oliver Brown"
                    className={`w-full px-3.5 py-2 rounded-xl border text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                      createErrors.fullName ? "border-red-400 bg-red-50/30" : "border-gray-300"
                    }`}
                  />
                  {createErrors.fullName && (
                    <p className="text-[11px] text-red-600 mt-1">{createErrors.fullName}</p>
                  )}
                </div>

                {/* Phone Number */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1">
                    Phone Number <span className="text-red-600">*</span>
                  </label>
                  <input
                    type="tel"
                    value={createForm.phoneNumber}
                    onChange={(e) =>
                      setCreateForm((prev) => ({ ...prev, phoneNumber: e.target.value }))
                    }
                    placeholder="e.g. +94771234567 or 0771234567"
                    className={`w-full px-3.5 py-2 rounded-xl border text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                      createErrors.phoneNumber ? "border-red-400 bg-red-50/30" : "border-gray-300"
                    }`}
                  />
                  {createErrors.phoneNumber && (
                    <p className="text-[11px] text-red-600 mt-1">{createErrors.phoneNumber}</p>
                  )}
                </div>

                {/* Email Address */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1">
                    Email Address <span className="text-red-600">*</span>
                  </label>
                  <input
                    type="email"
                    value={createForm.email}
                    onChange={(e) =>
                      setCreateForm((prev) => ({ ...prev, email: e.target.value }))
                    }
                    placeholder="e.g. oliver.brown@example.com"
                    className={`w-full px-3.5 py-2 rounded-xl border text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                      createErrors.email ? "border-red-400 bg-red-50/30" : "border-gray-300"
                    }`}
                  />
                  {createErrors.email && (
                    <p className="text-[11px] text-red-600 mt-1">{createErrors.email}</p>
                  )}
                </div>

                {/* NIC */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1">
                    National Identity Card (NIC) <span className="text-red-600">*</span>
                  </label>
                  <input
                    type="text"
                    value={createForm.nic}
                    onChange={(e) =>
                      setCreateForm((prev) => ({ ...prev, nic: e.target.value }))
                    }
                    placeholder="e.g. 199512345678 or 951234567V"
                    className={`w-full px-3.5 py-2 rounded-xl border text-sm text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] ${
                      createErrors.nic ? "border-red-400 bg-red-50/30" : "border-gray-300"
                    }`}
                  />
                  {createErrors.nic && (
                    <p className="text-[11px] text-red-600 mt-1">{createErrors.nic}</p>
                  )}
                </div>

                {/* Branch Selection */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1">
                    Primary Branch <span className="text-red-600">*</span>
                  </label>
                  {isSuperAdmin ? (
                    <select
                      value={createForm.branchId}
                      onChange={(e) =>
                        setCreateForm((prev) => ({ ...prev, branchId: e.target.value }))
                      }
                      className={`w-full px-3.5 py-2 rounded-xl border text-sm text-gray-900 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] cursor-pointer ${
                        createErrors.branchId ? "border-red-400 bg-red-50/30" : "border-gray-300"
                      }`}
                    >
                      <option value="">Select Branch...</option>
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.name} ({b.code})
                        </option>
                      ))}
                    </select>
                  ) : (
                    <div className="px-3.5 py-2 rounded-xl bg-gray-100 border border-gray-200 text-sm font-semibold text-gray-700 flex items-center gap-2">
                      <Building2 className="w-4 h-4 text-[#0B3D0B]" />
                      <span>
                        {user?.assignedBranches?.[0]?.name || "Assigned Branch"} (Permitted Scope)
                      </span>
                    </div>
                  )}
                  {createErrors.branchId && (
                    <p className="text-[11px] text-red-600 mt-1">{createErrors.branchId}</p>
                  )}
                </div>

                {/* Footer Buttons */}
                <div className="pt-3 flex items-center justify-end gap-3 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setShowCreateModal(false)}
                    disabled={createSubmitLoading}
                    className="px-4 py-2 rounded-xl text-xs font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-100 transition-colors cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={createSubmitLoading}
                    className="px-5 py-2 rounded-xl btn-primary-green text-xs font-bold flex items-center gap-2 cursor-pointer disabled:opacity-50"
                  >
                    {createSubmitLoading ? (
                      <>
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                        <span>Creating Customer...</span>
                      </>
                    ) : (
                      <>
                        <UserPlus className="w-3.5 h-3.5 text-[#FFD700]" />
                        <span>Save Customer</span>
                      </>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ================= ACTIVATE / DEACTIVATE CONFIRMATION MODAL ================= */}
        {statusActionCustomer && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div
              className="fixed inset-0 bg-black/50 backdrop-blur-xs transition-opacity"
              onClick={() => !statusActionLoading && setStatusActionCustomer(null)}
            />

            <div className="relative w-full max-w-md bg-white rounded-2xl shadow-2xl border border-gray-200 overflow-hidden z-50 flex flex-col">
              {/* Header */}
              <div
                className={`px-6 py-4 text-white flex items-center justify-between ${
                  statusActionCustomer.isActive ? "bg-red-800" : "bg-[#0B3D0B]"
                }`}
              >
                <div className="flex items-center gap-2">
                  <Power className="w-5 h-5 text-[#FFD700]" />
                  <h2 className="text-base font-bold tracking-tight">
                    {statusActionCustomer.isActive
                      ? "Confirm Customer Deactivation"
                      : "Confirm Customer Reactivation"}
                  </h2>
                </div>
                <button
                  onClick={() => !statusActionLoading && setStatusActionCustomer(null)}
                  className="p-1 rounded-lg hover:bg-white/10 transition-colors cursor-pointer"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Body */}
              <form onSubmit={handleStatusActionSubmit} className="p-6 space-y-4">
                {statusActionError && (
                  <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 flex items-start gap-2 text-xs text-red-700">
                    <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0 mt-0.5" />
                    <span>{statusActionError}</span>
                  </div>
                )}

                <div className="space-y-2">
                  <p className="text-xs text-gray-700 leading-relaxed">
                    You are changing the account status for:
                  </p>
                  <div className="p-3 bg-gray-50 rounded-xl border border-gray-200 text-xs">
                    <div className="font-bold text-gray-900 text-sm">
                      {statusActionCustomer.fullName}
                    </div>
                    <div className="text-gray-500 font-mono mt-0.5">
                      Phone: {statusActionCustomer.phoneNumber || "—"} | Branch: {statusActionCustomer.primaryBranchName}
                    </div>
                  </div>
                </div>

                {statusActionCustomer.isActive ? (
                  <>
                    <div className="p-3 rounded-xl bg-amber-50 border border-amber-200 text-[11px] text-amber-900 leading-relaxed">
                      <strong>Security Notice:</strong> Deactivating will immediately revoke all sessions, refresh tokens, and active JWT access tokens for this customer. They will be barred from logging in or transacting. Profiles, savings plans, payments, and history will remain safely preserved.
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-gray-800 mb-1">
                        Deactivation Reason <span className="text-red-600">*</span>
                      </label>
                      <textarea
                        rows={2}
                        value={statusActionReason}
                        onChange={(e) => {
                          setStatusActionReason(e.target.value);
                          if (statusActionReasonError) setStatusActionReasonError(null);
                        }}
                        placeholder="Provide reason for audit records (e.g. Compliance review, fraud check, requested suspension)..."
                        className={`w-full px-3 py-2 rounded-xl border text-xs text-gray-900 focus:outline-none focus:ring-2 focus:ring-red-600 ${
                          statusActionReasonError ? "border-red-400 bg-red-50/30" : "border-gray-300"
                        }`}
                      />
                      {statusActionReasonError && (
                        <p className="text-[11px] text-red-600 mt-1">
                          {statusActionReasonError}
                        </p>
                      )}
                    </div>
                  </>
                ) : (
                  <>
                    <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-200 text-[11px] text-emerald-900 leading-relaxed">
                      <strong>Reactivation Notice:</strong> Reactivating this customer account will permit them to authenticate via OTP, access mobile services, and make contributions. Note: An authorized admin action is required; customer OTP login alone cannot reactivate an inactive account.
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-gray-800 mb-1">
                        Optional Reason or Remarks
                      </label>
                      <input
                        type="text"
                        value={statusActionReason}
                        onChange={(e) => setStatusActionReason(e.target.value)}
                        placeholder="e.g. Identity re-verified upon customer request"
                        className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                      />
                    </div>
                  </>
                )}

                {/* Footer Buttons */}
                <div className="pt-3 flex items-center justify-end gap-3 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setStatusActionCustomer(null)}
                    disabled={statusActionLoading}
                    className="px-4 py-2 rounded-xl text-xs font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-100 transition-colors cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={statusActionLoading}
                    className={`px-5 py-2 rounded-xl text-xs font-bold text-white flex items-center gap-2 cursor-pointer disabled:opacity-50 shadow-sm ${
                      statusActionCustomer.isActive
                        ? "bg-red-700 hover:bg-red-800"
                        : "btn-primary-green"
                    }`}
                  >
                    {statusActionLoading ? (
                      <>
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                        <span>Updating Status...</span>
                      </>
                    ) : statusActionCustomer.isActive ? (
                      <>
                        <Power className="w-3.5 h-3.5" />
                        <span>Confirm Deactivation</span>
                      </>
                    ) : (
                      <>
                        <RotateCcw className="w-3.5 h-3.5" />
                        <span>Reactivate Account</span>
                      </>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ================= DETAIL MODAL (FULL UNMASKED NIC) ================= */}
        {(detailCustomer || detailLoading || detailError) && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div
              className="fixed inset-0 bg-black/50 backdrop-blur-xs transition-opacity"
              onClick={handleCloseDetail}
            />

            <div className="relative w-full max-w-2xl bg-white rounded-2xl shadow-2xl border border-gray-200 overflow-hidden z-50 flex flex-col max-h-[90vh]">
              {/* Header */}
              <div className="px-6 py-4 bg-[#0B3D0B] text-white flex items-center justify-between">
                <div className="flex items-center gap-2.5">
                  <Shield className="w-5 h-5 text-[#FFD700]" />
                  <h2 className="text-base font-bold tracking-tight">
                    Customer Profile & Authorized Details
                  </h2>
                </div>
                <button
                  onClick={handleCloseDetail}
                  className="p-1 rounded-lg text-emerald-200 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Body */}
              <div className="p-6 overflow-y-auto space-y-6">
                {detailLoading && (
                  <div className="py-12 flex flex-col items-center justify-center gap-3">
                    <div className="w-8 h-8 border-3 border-[#0B3D0B] border-t-[#FFD700] rounded-full animate-spin" />
                    <span className="text-xs font-semibold text-gray-500">
                      Loading customer profile and unmasked credentials...
                    </span>
                  </div>
                )}

                {detailError && (
                  <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-700 text-sm">
                    {detailError}
                  </div>
                )}

                {detailCustomer && !detailLoading && (
                  <>
                    {/* Header Summary Banner */}
                    <div className="flex items-center gap-4 p-4 rounded-xl bg-gray-50 border border-gray-200">
                      <div className="w-14 h-14 rounded-full bg-[#0B3D0B] text-[#FFD700] font-black text-lg flex items-center justify-center shadow-xs">
                        {detailCustomer.fullName
                          ? detailCustomer.fullName
                              .split(" ")
                              .map((n) => n[0])
                              .join("")
                              .substring(0, 2)
                              .toUpperCase()
                          : "CU"}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <h3 className="text-lg font-black text-gray-900 truncate">
                            {detailCustomer.fullName}
                          </h3>
                          {detailCustomer.isActive ? (
                            <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-green-100 text-green-800">
                              Active
                            </span>
                          ) : (
                            <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-red-100 text-red-800">
                              Inactive
                            </span>
                          )}
                        </div>
                        <div className="flex items-center gap-3 text-xs text-gray-500 mt-1">
                          <span className="flex items-center gap-1">
                            <Building2 className="w-3.5 h-3.5 text-[#0B3D0B]" />
                            {detailCustomer.primaryBranchName} ({detailCustomer.primaryBranchCode})
                          </span>
                          <span>•</span>
                          <span>
                            Registered {new Date(detailCustomer.createdAtUtc).toLocaleDateString()}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Account Status / Deactivation Details if Inactive */}
                    {!detailCustomer.isActive && (
                      <div className="p-3.5 rounded-xl bg-red-50/80 border border-red-200 text-xs text-red-900 space-y-1">
                        <div className="font-bold flex items-center gap-1.5">
                          <AlertCircle className="w-4 h-4 text-red-600" />
                          <span>Account Inactive / Deactivated</span>
                        </div>
                        {detailCustomer.closureReason && (
                          <p>
                            <strong>Closure Reason:</strong> {detailCustomer.closureReason} (Closed on:{" "}
                            {detailCustomer.closedAtUtc
                              ? new Date(detailCustomer.closedAtUtc).toLocaleString()
                              : "—"}
                            )
                          </p>
                        )}
                        {detailCustomer.deactivationReason && (
                          <p>
                            <strong>Deactivation Reason:</strong> {detailCustomer.deactivationReason} (Deactivated on:{" "}
                            {detailCustomer.deactivatedAtUtc
                              ? new Date(detailCustomer.deactivatedAtUtc).toLocaleString()
                              : "—"}
                            )
                          </p>
                        )}
                      </div>
                    )}

                    {/* Attributes Grid (Including FULL UNMASKED NIC) */}
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      {/* Phone */}
                      <div className="p-3.5 rounded-xl border border-gray-200 bg-white">
                        <div className="flex items-center justify-between text-xs text-gray-500">
                          <span className="font-semibold flex items-center gap-1.5">
                            <Phone className="w-3.5 h-3.5 text-[#0B3D0B]" />
                            Phone Number
                          </span>
                          {detailCustomer.isPhoneVerified ? (
                            <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full">
                              Verified
                            </span>
                          ) : (
                            <span className="text-[10px] text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full">
                              Unverified
                            </span>
                          )}
                        </div>
                        <p className="text-sm font-mono font-bold text-gray-900 mt-1.5">
                          {detailCustomer.phoneNumber || "Not provided"}
                        </p>
                      </div>

                      {/* Email */}
                      <div className="p-3.5 rounded-xl border border-gray-200 bg-white">
                        <div className="flex items-center justify-between text-xs text-gray-500">
                          <span className="font-semibold flex items-center gap-1.5">
                            <Mail className="w-3.5 h-3.5 text-[#0B3D0B]" />
                            Email Address
                          </span>
                          {detailCustomer.isEmailVerified ? (
                            <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full">
                              Verified
                            </span>
                          ) : (
                            <span className="text-[10px] text-amber-700 bg-amber-50 px-2 py-0.5 rounded-full">
                              Unverified
                            </span>
                          )}
                        </div>
                        <p className="text-sm font-bold text-gray-900 mt-1.5 truncate">
                          {detailCustomer.email || "Not provided"}
                        </p>
                      </div>

                      {/* Full Unmasked NIC */}
                      <div className="p-3.5 rounded-xl border border-emerald-300 bg-emerald-50/20">
                        <div className="flex items-center justify-between text-xs text-gray-500">
                          <span className="font-semibold flex items-center gap-1.5 text-[#0B3D0B]">
                            <CreditCard className="w-3.5 h-3.5 text-[#0B3D0B]" />
                            National Identity Card (NIC)
                          </span>
                          <span className="text-[10px] font-bold text-[#0B3D0B] bg-[#FFD700]/30 px-2 py-0.5 rounded-full">
                            Full Unmasked NIC
                          </span>
                        </div>
                        <p className="text-sm font-mono font-black text-gray-900 mt-1.5 tracking-wider">
                          {detailCustomer.nic || "Not recorded"}
                        </p>
                        <span className="text-[10px] text-emerald-800 font-medium mt-0.5 block">
                          Verified for administrative & compliance inspection
                        </span>
                      </div>

                      {/* Date of Birth */}
                      <div className="p-3.5 rounded-xl border border-gray-200 bg-white">
                        <span className="text-xs text-gray-500 font-semibold flex items-center gap-1.5">
                          <Calendar className="w-3.5 h-3.5 text-[#0B3D0B]" />
                          Date of Birth
                        </span>
                        <p className="text-sm font-bold text-gray-900 mt-1.5">
                          {detailCustomer.dateOfBirth
                            ? new Date(detailCustomer.dateOfBirth).toLocaleDateString("en-US", {
                                year: "numeric",
                                month: "long",
                                day: "numeric",
                              })
                            : "Profile incomplete (pending customer DOB)"}
                        </p>
                      </div>
                    </div>

                    {/* Savings Plans Section */}
                    <div>
                      <h4 className="text-sm font-bold text-gray-900 mb-3 flex items-center gap-2">
                        <Gem className="w-4 h-4 text-[#0B3D0B]" />
                        <span>Registered Savings & Investment Plans</span>
                      </h4>

                      {detailCustomer.chituSlots.length === 0 &&
                      detailCustomer.jewelleryEnrolments.length === 0 ? (
                        <div className="p-4 rounded-xl border border-dashed border-gray-300 text-center text-xs text-gray-500">
                          This customer currently has no active or closed savings plans.
                        </div>
                      ) : (
                        <div className="space-y-2.5">
                          {detailCustomer.chituSlots.map((slot) => (
                            <div
                              key={slot.id}
                              className="p-3 rounded-xl border border-gray-200 bg-emerald-50/30 flex items-center justify-between text-xs"
                            >
                              <div>
                                <span className="font-bold text-gray-900 block">
                                  {slot.planName} (Slot #{slot.slotNumber})
                                </span>
                                <span className="text-gray-500 text-[11px]">
                                  LKR {slot.monthlyInstalmentAmount.toLocaleString()}/mo
                                </span>
                              </div>
                              <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800">
                                {slot.status}
                              </span>
                            </div>
                          ))}

                          {detailCustomer.jewelleryEnrolments.map((enr) => (
                            <div
                              key={enr.id}
                              className="p-3 rounded-xl border border-gray-200 bg-amber-50/30 flex items-center justify-between text-xs"
                            >
                              <div>
                                <span className="font-bold text-gray-900 block">
                                  {enr.planName}
                                </span>
                                <span className="text-gray-500 text-[11px]">
                                  Target: {enr.targetWeightGrams}g | Saved: {enr.totalSavedGrams}g
                                </span>
                              </div>
                              <span className="px-2.5 py-0.5 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800">
                                {enr.status}
                              </span>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </>
                )}
              </div>

              {/* Footer */}
              <div className="px-6 py-3.5 bg-gray-50 border-t border-gray-200 flex justify-end">
                <button
                  onClick={handleCloseDetail}
                  className="px-4 py-2 rounded-xl text-xs font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-100 transition-colors cursor-pointer"
                >
                  Close Profile
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
