"use client";

import React, { useState, useEffect, useCallback, use, Suspense } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { AdminLayout } from "../../../components/AdminLayout";
import { adminApi, ApiError } from "../../../lib/api";
import { useAuth } from "../../../lib/auth-context";
import { CustomerDetail, CustomerEnrolment } from "../../../types/customer";
import { EnrolmentContribution } from "../../../types/jewelleryPlan";
import { AssignPlanModal } from "../../../components/AssignPlanModal";
import { RecordPaymentModal } from "../../../components/RecordPaymentModal";
import {
  User,
  Phone,
  Mail,
  Shield,
  Calendar,
  Building2,
  DollarSign,
  Gem,
  CheckCircle2,
  AlertCircle,
  ArrowLeft,
  Plus,
  CreditCard,
  RefreshCw,
  Power,
  RotateCcw,
  Sparkles,
  ExternalLink,
  ChevronRight,
  Filter,
} from "lucide-react";

interface CustomerDetailsPageProps {
  params: Promise<{ id: string }>;
}

export default function CustomerDetailsPage({ params }: CustomerDetailsPageProps) {
  return (
    <Suspense
      fallback={
        <AdminLayout>
          <div className="flex flex-col items-center justify-center min-h-[400px]">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-[#0B3D0B]" />
            <p className="mt-4 text-xs font-bold text-gray-500 uppercase tracking-widest">
              Loading customer details...
            </p>
          </div>
        </AdminLayout>
      }
    >
      <CustomerDetailsInner params={params} />
    </Suspense>
  );
}

function CustomerDetailsInner({ params }: CustomerDetailsPageProps) {
  const router = useRouter();
  const resolvedParams = use(params);
  const customerId = resolvedParams.id;
  const { user, isSuperAdmin, isBranchAdmin } = useAuth();

  // Customer state
  const [customer, setCustomer] = useState<CustomerDetail | null>(null);
  const [enrolments, setEnrolments] = useState<CustomerEnrolment[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  // Active tab state: "overview" | "plans" | "payments"
  const [activeTab, setActiveTab] = useState<"overview" | "plans" | "payments">("overview");

  // Payments tab enrolment filter
  const [selectedEnrolmentFilter, setSelectedEnrolmentFilter] = useState<string>("ALL");

  // Modals state
  const [showAssignModal, setShowAssignModal] = useState<boolean>(false);
  const [showPaymentModal, setShowPaymentModal] = useState<boolean>(false);
  const [selectedEnrolmentForPayment, setSelectedEnrolmentForPayment] = useState<CustomerEnrolment | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Status Action Modal (Deactivate / Activate Customer)
  const [showStatusModal, setShowStatusModal] = useState<boolean>(false);
  const [statusReason, setStatusReason] = useState<string>("");
  const [statusLoading, setStatusLoading] = useState<boolean>(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  const loadCustomerData = useCallback(async () => {
    if (!customerId) return;
    setLoading(true);
    setError(null);

    try {
      const [detailRes, enrolmentsRes] = await Promise.all([
        adminApi.getCustomerDetail(customerId),
        adminApi.getCustomerJewelleryEnrolments(customerId),
      ]);

      setCustomer(detailRes);
      setEnrolments(enrolmentsRes);
    } catch (err: any) {
      console.error("Failed to load customer details:", err);
      setError(err.message || "Failed to load customer profile details.");
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    loadCustomerData();
  }, [loadCustomerData]);

  // Aggregate summaries across all enrolments before pagination
  const activeEnrolments = enrolments.filter((e) => e.status === "Active");
  const totalEnrolmentsCount = enrolments.length;
  const activeEnrolmentsCount = activeEnrolments.length;

  // Flatten all confirmed contributions across all enrolments
  const allContributionsWithEnrolment: Array<
    EnrolmentContribution & {
      enrolmentId: string;
      enrolmentReference: string;
      planName: string;
    }
  > = [];

  enrolments.forEach((e) => {
    (e.contributions || []).forEach((c) => {
      allContributionsWithEnrolment.push({
        ...c,
        enrolmentId: e.id,
        enrolmentReference: e.enrolmentReference,
        planName: e.planName,
      });
    });
  });

  allContributionsWithEnrolment.sort(
    (a, b) => new Date(b.confirmedAtUtc).getTime() - new Date(a.confirmedAtUtc).getTime()
  );

  // Calculate total confirmed payments in LKR (grouped by payment ID to prevent duplicate counts)
  const uniqueConfirmedPaymentsMap = new Map<string, number>();
  allContributionsWithEnrolment.forEach((c) => {
    if (c.status === "Succeeded") {
      const key = c.paymentReference || c.id;
      if (!uniqueConfirmedPaymentsMap.has(key)) {
        uniqueConfirmedPaymentsMap.set(key, c.currencyAmount);
      }
    }
  });

  const totalConfirmedPaymentsAmount = Array.from(uniqueConfirmedPaymentsMap.values()).reduce(
    (acc, val) => acc + val,
    0
  );

  // Filter contributions for Payments tab
  const filteredContributions =
    selectedEnrolmentFilter === "ALL"
      ? allContributionsWithEnrolment
      : allContributionsWithEnrolment.filter((c) => c.enrolmentId === selectedEnrolmentFilter);

  // Handle status toggle (Activate / Deactivate Customer)
  const handleToggleStatus = async () => {
    if (!customer) return;

    if (customer.isActive && !statusReason.trim()) {
      setStatusError("Deactivation reason is strictly required.");
      return;
    }

    setStatusLoading(true);
    setStatusError(null);

    try {
      await adminApi.updateCustomerStatus(customer.id, {
        isActive: !customer.isActive,
        reason: customer.isActive ? statusReason.trim() : "Reactivated by admin",
      });

      setShowStatusModal(false);
      setStatusReason("");
      setToastMessage(
        customer.isActive
          ? "Customer account deactivated successfully."
          : "Customer account reactivated successfully."
      );
      loadCustomerData();
    } catch (err: any) {
      setStatusError(err.message || "Failed to update customer status.");
    } finally {
      setStatusLoading(false);
    }
  };

  const initials = customer?.fullName
    ? customer.fullName
        .split(" ")
        .map((n) => n[0])
        .join("")
        .substring(0, 2)
        .toUpperCase()
    : "CU";

  return (
    <AdminLayout title={customer ? customer.fullName : "Customer Profile"}>
      <div className="space-y-6 max-w-7xl mx-auto pb-12">
        {/* Back Link */}
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => router.back()}
            className="text-xs font-bold text-gray-500 hover:text-gray-900 flex items-center gap-1.5 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            Back to Roster
          </button>
        </div>

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

        {/* Error State */}
        {error && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center justify-between">
            <div className="flex items-center gap-2.5">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
            <button
              onClick={loadCustomerData}
              className="px-3 py-1 bg-red-100 hover:bg-red-200 font-bold rounded-lg transition-colors"
            >
              Retry
            </button>
          </div>
        )}

        {/* Profile Header matching Screenshot 2 & 3 */}
        {loading && !customer ? (
          <div className="p-12 text-center text-gray-400 bg-white rounded-2xl border border-gray-100 flex flex-col items-center justify-center gap-2">
            <RefreshCw className="w-6 h-6 animate-spin text-[#0B3D0B]" />
            <span>Loading customer profile...</span>
          </div>
        ) : customer ? (
          <>
            {/* Top Customer Card */}
            <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
              <div className="p-6 border-b border-gray-100 flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div className="flex items-center gap-4">
                  <div className="w-16 h-16 rounded-2xl bg-amber-100 text-amber-900 font-black text-xl flex items-center justify-center shadow-inner flex-shrink-0">
                    {initials}
                  </div>
                  <div>
                    <div className="flex items-center gap-2.5">
                      <h1 className="text-2xl font-black text-gray-900 tracking-tight">{customer.fullName}</h1>
                      <span
                        className={`text-xs font-bold px-2.5 py-0.5 rounded-full ${
                          customer.isActive
                            ? "bg-emerald-100 text-emerald-800"
                            : "bg-red-100 text-red-800"
                        }`}
                      >
                        {customer.isActive ? "Active" : "Inactive"}
                      </span>
                    </div>

                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500 mt-1">
                      <span className="flex items-center gap-1 font-mono">
                        <Phone className="w-3.5 h-3.5 text-gray-400" />
                        {customer.phoneNumber || "No phone number"}
                      </span>
                      <span className="flex items-center gap-1">
                        <Mail className="w-3.5 h-3.5 text-gray-400" />
                        {customer.email || "No email address"}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Account Actions */}
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={() => setShowStatusModal(true)}
                    className={`px-4 py-2 rounded-xl text-xs font-extrabold uppercase tracking-wider flex items-center gap-1.5 transition-colors border ${
                      customer.isActive
                        ? "border-red-200 text-red-700 hover:bg-red-50"
                        : "border-emerald-200 text-emerald-800 hover:bg-emerald-50"
                    }`}
                  >
                    <Power className="w-3.5 h-3.5" />
                    {customer.isActive ? "Deactivate" : "Activate"}
                  </button>
                </div>
              </div>

              {/* Sub-header Identity and Registration Details */}
              <div className="px-6 py-4 bg-[#FAF9F6] grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs">
                <div>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block mb-0.5">
                    Identity Details (NIC)
                  </span>
                  <span className="font-mono font-bold text-gray-800 text-sm">
                    {customer.nic || "Not Provided"}
                  </span>
                </div>

                <div>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block mb-0.5">
                    Branch Location
                  </span>
                  <span className="font-bold text-gray-800 text-sm">
                    {customer.primaryBranchName} ({customer.primaryBranchCode})
                  </span>
                </div>

                <div>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block mb-0.5">
                    Customer Since
                  </span>
                  <span className="font-bold text-gray-800 text-sm">
                    {new Date(customer.createdAtUtc).toLocaleDateString("en-GB", {
                      day: "numeric",
                      month: "short",
                      year: "numeric",
                    })}
                  </span>
                </div>
              </div>
            </div>

            {/* 3 Summary Cards matching Screenshot 2 & 3 */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {/* Active Plans Card */}
              <div className="p-5 rounded-2xl bg-white border border-gray-100 shadow-sm flex items-center justify-between">
                <div>
                  <span className="text-xs font-bold text-gray-400 uppercase tracking-wider block mb-1">
                    Active Jewellery Enrolments
                  </span>
                  <div className="text-3xl font-black text-gray-900">{activeEnrolmentsCount}</div>
                </div>
                <div className="w-12 h-12 rounded-xl bg-emerald-50 text-[#0B3D0B] flex items-center justify-center">
                  <Gem className="w-6 h-6" />
                </div>
              </div>

              {/* Total Confirmed Payments Card */}
              <div className="p-5 rounded-2xl bg-white border border-gray-100 shadow-sm flex items-center justify-between">
                <div>
                  <span className="text-xs font-bold text-gray-400 uppercase tracking-wider block mb-1">
                    Total Confirmed Payments
                  </span>
                  <div className="text-2xl font-black text-[#0B3D0B]">
                    LKR {totalConfirmedPaymentsAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </div>
                </div>
                <div className="w-12 h-12 rounded-xl bg-amber-50 text-amber-700 flex items-center justify-center">
                  <DollarSign className="w-6 h-6" />
                </div>
              </div>

              {/* Total Jewellery Enrolments Card */}
              <div className="p-5 rounded-2xl bg-white border border-gray-100 shadow-sm flex items-center justify-between">
                <div>
                  <span className="text-xs font-bold text-gray-400 uppercase tracking-wider block mb-1">
                    Total Jewellery Enrolments
                  </span>
                  <div className="text-3xl font-black text-gray-900">{totalEnrolmentsCount}</div>
                </div>
                <div className="w-12 h-12 rounded-xl bg-gray-50 text-gray-700 flex items-center justify-center">
                  <Sparkles className="w-6 h-6 text-[#0B3D0B]" />
                </div>
              </div>
            </div>

            {/* Navigation Tabs and Quick Action Buttons */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-gray-200 pb-2">
              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setActiveTab("overview")}
                  className={`px-4 py-2 text-xs font-extrabold uppercase tracking-wider rounded-xl transition-all ${
                    activeTab === "overview"
                      ? "bg-[#0B3D0B] text-[#FFD700] shadow-sm"
                      : "text-gray-600 hover:text-gray-900 hover:bg-gray-100"
                  }`}
                >
                  Overview
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab("plans")}
                  className={`px-4 py-2 text-xs font-extrabold uppercase tracking-wider rounded-xl transition-all ${
                    activeTab === "plans"
                      ? "bg-[#0B3D0B] text-[#FFD700] shadow-sm"
                      : "text-gray-600 hover:text-gray-900 hover:bg-gray-100"
                  }`}
                >
                  Jewellery Plans ({totalEnrolmentsCount})
                </button>
                <button
                  type="button"
                  onClick={() => setActiveTab("payments")}
                  className={`px-4 py-2 text-xs font-extrabold uppercase tracking-wider rounded-xl transition-all ${
                    activeTab === "payments"
                      ? "bg-[#0B3D0B] text-[#FFD700] shadow-sm"
                      : "text-gray-600 hover:text-gray-900 hover:bg-gray-100"
                  }`}
                >
                  Jewellery Payments ({allContributionsWithEnrolment.length})
                </button>
              </div>

              {/* Action Buttons: Record Payment & Assign Plan */}
              <div className="flex items-center gap-2.5">
                <button
                  type="button"
                  onClick={() => {
                    setSelectedEnrolmentForPayment(activeEnrolments[0] || null);
                    setShowPaymentModal(true);
                  }}
                  disabled={activeEnrolments.length === 0}
                  className="px-4 py-2 rounded-xl bg-[#0B3D0B] hover:bg-[#082b08] disabled:opacity-50 disabled:cursor-not-allowed text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-1.5 shadow-sm transition-all"
                >
                  <CreditCard className="w-3.5 h-3.5" />
                  Record Payment
                </button>

                <button
                  type="button"
                  onClick={() => setShowAssignModal(true)}
                  className="px-4 py-2 rounded-xl bg-white border border-[#0B3D0B] text-[#0B3D0B] hover:bg-emerald-50 text-xs font-bold uppercase tracking-wider flex items-center gap-1.5 transition-colors shadow-sm"
                >
                  <Plus className="w-3.5 h-3.5 stroke-[3]" />
                  Assign Plan
                </button>
              </div>
            </div>

            {/* TAB CONTENT */}

            {/* 1. OVERVIEW TAB */}
            {activeTab === "overview" && (
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {/* Contact & Identity Details Card */}
                <div className="bg-white p-6 rounded-2xl border border-gray-100 shadow-sm space-y-4">
                  <h3 className="text-sm font-black text-gray-900 uppercase tracking-wider border-b border-gray-100 pb-3">
                    Contact & Identity Details
                  </h3>

                  <div className="grid grid-cols-2 gap-y-4 gap-x-6 text-xs">
                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Full Name</span>
                      <span className="font-bold text-gray-800 text-sm">{customer.fullName}</span>
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Phone Number</span>
                      <span className="font-mono font-bold text-gray-800 text-sm">
                        {customer.phoneNumber || "—"}
                      </span>
                      {customer.isPhoneVerified && (
                        <span className="text-[10px] text-emerald-700 font-semibold block">Verified</span>
                      )}
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Email Address</span>
                      <span className="font-bold text-gray-800 truncate block text-sm">
                        {customer.email || "—"}
                      </span>
                      {customer.isEmailVerified && (
                        <span className="text-[10px] text-emerald-700 font-semibold block">Verified</span>
                      )}
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">NIC / Passport</span>
                      <span className="font-mono font-bold text-gray-800 text-sm">
                        {customer.nic || "—"}
                      </span>
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Date of Birth</span>
                      <span className="font-bold text-gray-800 text-sm">
                        {customer.dateOfBirth || "Not recorded"}
                      </span>
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Primary Branch</span>
                      <span className="font-bold text-gray-800 text-sm">{customer.primaryBranchName}</span>
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Account Status</span>
                      <span
                        className={`inline-block mt-0.5 text-[11px] font-bold px-2 py-0.5 rounded-full ${
                          customer.isActive ? "bg-emerald-100 text-emerald-800" : "bg-red-100 text-red-800"
                        }`}
                      >
                        {customer.isActive ? "Active" : "Inactive"}
                      </span>
                    </div>

                    <div>
                      <span className="text-gray-400 block text-[10px] font-bold uppercase">Joined Date</span>
                      <span className="font-bold text-gray-800 text-sm">
                        {new Date(customer.createdAtUtc).toLocaleDateString("en-GB", {
                          day: "numeric",
                          month: "short",
                          year: "numeric",
                        })}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Recent Activity Card */}
                <div className="bg-white p-6 rounded-2xl border border-gray-100 shadow-sm space-y-4">
                  <h3 className="text-sm font-black text-gray-900 uppercase tracking-wider border-b border-gray-100 pb-3">
                    Recent Activity
                  </h3>

                  {allContributionsWithEnrolment.length > 0 ? (
                    <div className="space-y-3">
                      {allContributionsWithEnrolment.slice(0, 5).map((c) => (
                        <div
                          key={c.id}
                          className="flex items-center justify-between p-3 rounded-xl bg-[#FAF9F6] border border-gray-100"
                        >
                          <div className="flex items-center gap-3">
                            <div className="w-8 h-8 rounded-lg bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold text-xs">
                              <DollarSign className="w-4 h-4" />
                            </div>
                            <div>
                              <div className="text-xs font-bold text-gray-900">
                                Payment Recorded: {c.currency} {c.currencyAmount.toLocaleString()}
                              </div>
                              <div className="text-[11px] text-gray-500">
                                for {c.planName} ({c.enrolmentReference})
                              </div>
                            </div>
                          </div>
                          <div className="text-right">
                            <div className="text-xs font-extrabold text-emerald-800">
                              +{c.goldGrams.toFixed(4)} g
                            </div>
                            <div className="text-[10px] text-gray-400">
                              {new Date(c.confirmedAtUtc).toLocaleDateString()}
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="p-8 text-center text-gray-400 text-xs">
                      No payment or enrolment transactions recorded yet for this customer.
                    </div>
                  )}
                </div>
              </div>
            )}

            {/* 2. JEWELLERY PLANS TAB */}
            {activeTab === "plans" && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden">
                <div className="p-6 border-b border-gray-100 flex items-center justify-between">
                  <div>
                    <h3 className="text-base font-black text-gray-900">Customer Jewellery Plans</h3>
                    <p className="text-xs text-gray-500">
                      All jewellery savings plans assigned to this customer, including repeat and historical enrolments.
                    </p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setShowAssignModal(true)}
                    className="px-4 py-2 rounded-xl bg-[#0B3D0B] text-[#FFD700] text-xs font-bold uppercase tracking-wider flex items-center gap-1.5 shadow-sm transition-all"
                  >
                    <Plus className="w-3.5 h-3.5 stroke-[3]" />
                    Assign New Plan
                  </button>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="border-b border-gray-100 bg-[#FAF9F6] text-[11px] font-extrabold text-gray-500 uppercase tracking-wider">
                        <th className="py-3 px-4">Plan & Category</th>
                        <th className="py-3 px-4">Enrolment ID</th>
                        <th className="py-3 px-4">Duration</th>
                        <th className="py-3 px-4">Joined / Deadline</th>
                        <th className="py-3 px-4">Target</th>
                        <th className="py-3 px-4">Credited Gold</th>
                        <th className="py-3 px-4">Remaining</th>
                        <th className="py-3 px-4">Paid Total</th>
                        <th className="py-3 px-4">Progress</th>
                        <th className="py-3 px-4">Status</th>
                        <th className="py-3 px-4 text-right">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50 text-xs">
                      {enrolments.length > 0 ? (
                        enrolments.map((enr) => (
                          <tr key={enr.id} className="hover:bg-gray-50/80 transition-colors">
                            <td className="py-3 px-4">
                              <div className="font-bold text-gray-900">{enr.planName}</div>
                              <div className="text-[11px] text-gray-400">{enr.categoryName || "Jewellery"}</div>
                            </td>
                            <td className="py-3 px-4 font-mono font-bold text-gray-700 text-[11px]">
                              {enr.enrolmentReference}
                            </td>
                            <td className="py-3 px-4 text-gray-600 font-semibold">
                              {enr.selectedDurationMonths} mos
                            </td>
                            <td className="py-3 px-4 text-gray-600">
                              <div>{enr.startDate}</div>
                              <div className="text-[10px] text-gray-400 font-mono">to {enr.targetEndDate}</div>
                            </td>
                            <td className="py-3 px-4 font-bold text-gray-900">{enr.targetWeightGrams} g</td>
                            <td className="py-3 px-4 font-bold text-emerald-800">{enr.totalSavedGrams.toFixed(4)} g</td>
                            <td className="py-3 px-4 text-gray-500">{enr.remainingGrams.toFixed(4)} g</td>
                            <td className="py-3 px-4 font-extrabold text-[#0B3D0B]">
                              {enr.currency} {enr.totalContributedCurrency.toLocaleString()}
                            </td>
                            <td className="py-3 px-4 min-w-[100px]">
                              <div className="flex items-center gap-2">
                                <div className="flex-1 h-2 rounded-full bg-gray-100 overflow-hidden">
                                  <div
                                    className="h-full bg-gradient-to-r from-emerald-600 to-[#0B3D0B] rounded-full"
                                    style={{ width: `${Math.min(100, enr.progressPercentage)}%` }}
                                  />
                                </div>
                                <span className="font-bold text-gray-800 text-[11px]">{enr.progressPercentage}%</span>
                              </div>
                            </td>
                            <td className="py-3 px-4">
                              <span
                                className={`px-2.5 py-1 rounded-full text-[10px] font-bold ${
                                  enr.status === "Active"
                                    ? "bg-emerald-100 text-emerald-800"
                                    : enr.status === "Completed"
                                    ? "bg-[#FFD700]/30 text-amber-900 font-black border border-amber-300"
                                    : "bg-gray-100 text-gray-600"
                                }`}
                              >
                                {enr.status}
                              </span>
                            </td>
                            <td className="py-3 px-4 text-right">
                              <div className="flex items-center justify-end gap-1.5">
                                {enr.status === "Active" && (
                                  <button
                                    type="button"
                                    onClick={() => {
                                      setSelectedEnrolmentForPayment(enr);
                                      setShowPaymentModal(true);
                                    }}
                                    className="px-2 py-1 rounded-lg bg-emerald-50 text-emerald-800 hover:bg-emerald-100 text-[11px] font-bold transition-colors"
                                  >
                                    Record Cash
                                  </button>
                                )}
                                <button
                                  type="button"
                                  onClick={() => {
                                    setSelectedEnrolmentFilter(enr.id);
                                    setActiveTab("payments");
                                  }}
                                  className="px-2 py-1 rounded-lg bg-gray-100 hover:bg-gray-200 text-gray-700 text-[11px] font-bold transition-colors"
                                >
                                  Payments
                                </button>
                              </div>
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={11} className="py-8 text-center text-gray-400">
                            No jewellery savings enrolments found for this customer.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* 3. JEWELLERY PAYMENTS TAB */}
            {activeTab === "payments" && (
              <div className="bg-white rounded-2xl border border-gray-100 shadow-sm overflow-hidden space-y-4">
                <div className="p-6 border-b border-gray-100 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                  <div>
                    <h3 className="text-base font-black text-gray-900">Jewellery Payment History</h3>
                    <p className="text-xs text-gray-500">
                      Authoritative confirmed payment and gold contribution records.
                    </p>
                  </div>

                  {/* Filter by Enrolment Dropdown */}
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-gray-500 font-medium">Filter Enrolment:</span>
                    <select
                      value={selectedEnrolmentFilter}
                      onChange={(e) => setSelectedEnrolmentFilter(e.target.value)}
                      className="text-xs font-semibold px-3 py-1.5 rounded-xl border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/20"
                    >
                      <option value="ALL">All Enrolments ({enrolments.length})</option>
                      {enrolments.map((e) => (
                        <option key={e.id} value={e.id}>
                          {e.planName} ({e.enrolmentReference})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="border-b border-gray-100 bg-[#FAF9F6] text-[11px] font-extrabold text-gray-500 uppercase tracking-wider">
                        <th className="py-3 px-4">Payment Reference</th>
                        <th className="py-3 px-4">Enrolment & Plan</th>
                        <th className="py-3 px-4">Payment Date</th>
                        <th className="py-3 px-4">Amount Paid</th>
                        <th className="py-3 px-4">Credited Gold</th>
                        <th className="py-3 px-4">Applied Rate</th>
                        <th className="py-3 px-4">Method</th>
                        <th className="py-3 px-4">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-50 text-xs">
                      {filteredContributions.length > 0 ? (
                        filteredContributions.map((c) => (
                          <tr key={c.id} className="hover:bg-gray-50/80 transition-colors">
                            <td className="py-3 px-4 font-mono font-bold text-gray-900 text-[11px]">
                              {c.paymentReference || `TXN-${c.id.substring(0, 8).toUpperCase()}`}
                            </td>
                            <td className="py-3 px-4">
                              <div className="font-bold text-gray-900">{c.planName}</div>
                              <div className="text-[11px] text-gray-400 font-mono">{c.enrolmentReference}</div>
                            </td>
                            <td className="py-3 px-4 text-gray-600">
                              {new Date(c.confirmedAtUtc).toLocaleString("en-GB", {
                                day: "numeric",
                                month: "short",
                                year: "numeric",
                                hour: "2-digit",
                                minute: "2-digit",
                              })}
                            </td>
                            <td className="py-3 px-4 font-extrabold text-[#0B3D0B]">
                              {c.currency} {c.currencyAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                            </td>
                            <td className="py-3 px-4 font-bold text-emerald-800">
                              +{c.goldGrams.toFixed(4)} g
                            </td>
                            <td className="py-3 px-4 text-gray-600 font-mono">
                              {c.currency} {c.appliedRatePerGram.toLocaleString()} / g
                            </td>
                            <td className="py-3 px-4">
                              <span className="font-medium text-gray-700">{c.paymentMethod}</span>
                            </td>
                            <td className="py-3 px-4">
                              <span
                                className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                                  c.status === "Succeeded"
                                    ? "bg-emerald-100 text-emerald-800"
                                    : "bg-amber-100 text-amber-800"
                                }`}
                              >
                                {c.status}
                              </span>
                            </td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={8} className="py-8 text-center text-gray-400">
                            No payment transactions found matching the selected filter.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </>
        ) : null}
      </div>

      {/* Assign Plan Modal */}
      {customer && (
        <AssignPlanModal
          isOpen={showAssignModal}
          onClose={() => setShowAssignModal(false)}
          initialCustomerId={customer.id}
          onSuccess={(msg) => {
            setToastMessage(msg);
            loadCustomerData();
          }}
        />
      )}

      {/* Record Payment Modal */}
      {customer && (
        <RecordPaymentModal
          isOpen={showPaymentModal}
          onClose={() => {
            setShowPaymentModal(false);
            setSelectedEnrolmentForPayment(null);
          }}
          customerId={customer.id}
          customerName={customer.fullName}
          initialEnrolmentId={selectedEnrolmentForPayment?.id}
          enrolments={enrolments}
          onSuccess={(msg) => {
            setToastMessage(msg);
            loadCustomerData();
          }}
        />
      )}

      {/* Activate / Deactivate Modal */}
      {customer && showStatusModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in">
          <div className="relative w-full max-w-md bg-white rounded-2xl shadow-2xl border border-gray-100 overflow-hidden p-6 space-y-4">
            <h3 className="text-base font-bold text-gray-900">
              {customer.isActive ? "Deactivate Customer Account" : "Reactivate Customer Account"}
            </h3>

            {statusError && (
              <div className="p-3 bg-red-50 text-red-800 text-xs rounded-xl border border-red-200">
                {statusError}
              </div>
            )}

            <p className="text-xs text-gray-500 leading-relaxed">
              {customer.isActive
                ? "Deactivating this customer will revoke active login sessions and block new plan enrolments. Existing jewellery enrolments will remain inspectable."
                : "Reactivating this customer will permit account logins and new jewellery plan assignments."}
            </p>

            {customer.isActive && (
              <div>
                <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1">
                  Reason for Deactivation *
                </label>
                <textarea
                  value={statusReason}
                  onChange={(e) => setStatusReason(e.target.value)}
                  placeholder="e.g. Customer request or verification hold"
                  rows={3}
                  className="w-full p-2.5 text-xs rounded-xl border border-gray-200 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30"
                  required
                />
              </div>
            )}

            <div className="flex items-center justify-end gap-2 pt-2 border-t border-gray-100">
              <button
                type="button"
                onClick={() => setShowStatusModal(false)}
                disabled={statusLoading}
                className="px-4 py-2 text-xs font-bold text-gray-600 hover:bg-gray-100 rounded-xl transition-colors"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleToggleStatus}
                disabled={statusLoading}
                className={`px-4 py-2 text-xs font-bold uppercase tracking-wider text-white rounded-xl transition-colors ${
                  customer.isActive ? "bg-red-600 hover:bg-red-700" : "bg-emerald-700 hover:bg-emerald-800"
                }`}
              >
                {statusLoading ? "Updating..." : customer.isActive ? "Confirm Deactivation" : "Confirm Activation"}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
}
