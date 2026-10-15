"use client";

import React, { useState, useEffect, useCallback, useId } from "react";
import { X, Search, AlertCircle, CheckCircle2, UserPlus, Sparkles, Loader2, ShieldAlert } from "lucide-react";
import { adminApi, ApiError } from "../lib/api";
import { JewelleryPlan, AdminEnrolmentPreviewResponse } from "../types/jewelleryPlan";
import { CustomerListItem } from "../types/customer";

interface AssignPlanModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  initialPlanId?: string;
  initialCustomerId?: string;
  onOpenCreateCustomer?: () => void;
}

export function AssignPlanModal({
  isOpen,
  onClose,
  onSuccess,
  initialPlanId,
  initialCustomerId,
  onOpenCreateCustomer,
}: AssignPlanModalProps) {
  const [plans, setPlans] = useState<JewelleryPlan[]>([]);
  const [loadingPlans, setLoadingPlans] = useState(false);

  const [selectedPlanId, setSelectedPlanId] = useState<string>(initialPlanId || "");
  const [selectedCustomerId, setSelectedCustomerId] = useState<string>(initialCustomerId || "");
  const [selectedDuration, setSelectedDuration] = useState<number | "">("");

  // Customer search
  const [customerSearch, setCustomerSearch] = useState("");
  const [customerResults, setCustomerResults] = useState<CustomerListItem[]>([]);
  const [searchingCustomers, setSearchingCustomers] = useState(false);
  const [selectedCustomerObj, setSelectedCustomerObj] = useState<CustomerListItem | null>(null);

  // Preview & repeat check state
  const [previewData, setPreviewData] = useState<AdminEnrolmentPreviewResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [confirmRepeat, setConfirmRepeat] = useState(false);

  // Submission state
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const customerSearchInputId = useId();

  // Load plans on open
  useEffect(() => {
    if (!isOpen) return;

    let mounted = true;
    async function fetchPlans() {
      setLoadingPlans(true);
      try {
        const res = await adminApi.getJewelleryPlans({ pageSize: 100 });
        if (mounted) {
          setPlans(res.items);
        }
      } catch (err: any) {
        console.error("Failed to load plans for assignment:", err);
      } finally {
        if (mounted) setLoadingPlans(false);
      }
    }

    fetchPlans();
    return () => {
      mounted = false;
    };
  }, [isOpen]);

  // Handle preselected customer
  useEffect(() => {
    if (!isOpen) return;

    if (initialCustomerId) {
      setSelectedCustomerId(initialCustomerId);
      adminApi.getCustomerDetail(initialCustomerId).then((c) => {
        setSelectedCustomerObj({
          id: c.id,
          fullName: c.fullName,
          phoneNumber: c.phoneNumber,
          email: c.email,
          nic: c.nic,
          isPhoneVerified: c.isPhoneVerified,
          isEmailVerified: c.isEmailVerified,
          primaryBranchId: c.primaryBranchId,
          primaryBranchName: c.primaryBranchName,
          primaryBranchCode: c.primaryBranchCode,
          isActive: c.isActive,
          isProfileComplete: c.isProfileComplete,
          activePlansCount: c.jewelleryEnrolments?.filter((e) => e.status === "Active").length || 0,
          totalSlotsCount: c.chituSlots?.length || 0,
          createdAtUtc: c.createdAtUtc,
        });
      }).catch((err) => {
        console.error("Failed to load preselected customer:", err);
      });
    } else {
      setSelectedCustomerId("");
      setSelectedCustomerObj(null);
      setCustomerSearch("");
    }

    if (initialPlanId) {
      setSelectedPlanId(initialPlanId);
    } else {
      setSelectedPlanId("");
    }

    setSelectedDuration("");
    setConfirmRepeat(false);
    setPreviewData(null);
    setErrorMessage(null);
  }, [isOpen, initialPlanId, initialCustomerId]);

  // Customer debounced search
  useEffect(() => {
    if (!customerSearch.trim() || selectedCustomerObj) {
      setCustomerResults([]);
      return;
    }

    const timer = setTimeout(async () => {
      setSearchingCustomers(true);
      try {
        const res = await adminApi.getCustomers({ search: customerSearch.trim(), pageSize: 8 });
        setCustomerResults(res.items);
      } catch (err) {
        console.error("Error searching customers:", err);
      } finally {
        setSearchingCustomers(false);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [customerSearch, selectedCustomerObj]);

  // Fetch preview when plan and customer are selected
  useEffect(() => {
    if (!selectedCustomerId || !selectedPlanId) {
      setPreviewData(null);
      return;
    }

    let active = true;
    async function loadPreview() {
      setPreviewLoading(true);
      setErrorMessage(null);
      try {
        const res = await adminApi.previewPlanAssignment({
          customerId: selectedCustomerId,
          planId: selectedPlanId,
        });
        if (active) {
          setPreviewData(res);
          if (res.allowedDurationsMonths.length > 0 && !selectedDuration) {
            setSelectedDuration(res.allowedDurationsMonths[0]);
          }
        }
      } catch (err: any) {
        if (active) {
          setErrorMessage(err.message || "Failed to check plan assignment eligibility.");
        }
      } finally {
        if (active) setPreviewLoading(false);
      }
    }

    loadPreview();
    return () => {
      active = false;
    };
  }, [selectedCustomerId, selectedPlanId]);

  const handleSelectCustomer = (c: CustomerListItem) => {
    setSelectedCustomerObj(c);
    setSelectedCustomerId(c.id);
    setCustomerSearch("");
    setCustomerResults([]);
  };

  const handleClearCustomer = () => {
    setSelectedCustomerObj(null);
    setSelectedCustomerId("");
    setCustomerSearch("");
    setPreviewData(null);
  };

  const selectedPlan = plans.find((p) => p.id === selectedPlanId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCustomerId) {
      setErrorMessage("Please select an authorized customer.");
      return;
    }
    if (!selectedPlanId) {
      setErrorMessage("Please select a jewellery plan.");
      return;
    }
    if (!selectedDuration) {
      setErrorMessage("Please select an offered duration.");
      return;
    }

    if (previewData?.requiresRepeatConfirmation && !confirmRepeat) {
      setErrorMessage("Please confirm your intention to assign an additional repeat enrolment for this customer.");
      return;
    }

    setSubmitting(true);
    setErrorMessage(null);

    const idempotencyKey = `ENR-ASSIGN-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;

    try {
      await adminApi.assignJewelleryPlan({
        customerId: selectedCustomerId,
        planId: selectedPlanId,
        selectedDurationMonths: Number(selectedDuration),
        confirmRepeatEnrolment: confirmRepeat,
        idempotencyKey,
      });

      onSuccess("Jewellery plan assigned successfully! Active enrolment has been created.");
      onClose();
    } catch (err: any) {
      if (err.status === 409 && err.data?.requiresConfirmation) {
        setErrorMessage(err.data.message || "Repeat enrolment confirmation required.");
        if (previewData) {
          setPreviewData({
            ...previewData,
            requiresRepeatConfirmation: true,
            totalExistingEnrolments: err.data.totalEnrolmentCount || 1,
            activeExistingEnrolments: err.data.activeEnrolmentCount || 1,
          });
        }
      } else {
        setErrorMessage(err.message || "Failed to assign jewellery plan.");
      }
    } finally {
      setSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="relative w-full max-w-xl bg-white rounded-2xl shadow-2xl border border-gray-100 overflow-hidden flex flex-col max-h-[90vh]">
        {/* Modal Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-[#FAF9F6]">
          <div className="flex items-center gap-2.5">
            <div className="w-9 h-9 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center shadow-sm">
              <Sparkles className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-lg font-bold text-gray-900 leading-tight">Assign Gold Savings Plan</h2>
              <p className="text-xs text-gray-500">Create an active jewellery enrolment for an existing customer</p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="w-8 h-8 rounded-full bg-gray-100 hover:bg-gray-200 text-gray-600 flex items-center justify-center transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Modal Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-5">
          {errorMessage && (
            <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-start gap-2.5">
              <AlertCircle className="w-4 h-4 flex-shrink-0 text-red-600 mt-0.5" />
              <div className="flex-1 leading-relaxed">{errorMessage}</div>
            </div>
          )}

          {/* 1. Jewellery Plan Selector */}
          <div>
            <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1.5">
              Jewellery Plan <span className="text-red-500">*</span>
            </label>
            <select
              value={selectedPlanId}
              onChange={(e) => {
                setSelectedPlanId(e.target.value);
                setSelectedDuration("");
              }}
              disabled={loadingPlans || submitting}
              className="w-full px-3.5 py-2.5 text-sm rounded-xl border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
              required
            >
              <option value="">-- Select a Jewellery Plan --</option>
              {plans.map((p) => {
                const isUnavailable = !p.isActive || p.displayStatus === "Deactivated" || p.displayStatus === "Scheduled";
                return (
                  <option
                    key={p.id}
                    value={p.id}
                    disabled={isUnavailable}
                    className={isUnavailable ? "text-gray-400 bg-gray-50" : "text-gray-900"}
                  >
                    {p.name} ({p.categoryName || "Jewellery"})
                    {isUnavailable ? ` [Unavailable: ${p.displayStatus}]` : ""}
                  </option>
                );
              })}
            </select>
            {selectedPlan && (
              <p className="mt-1 text-xs text-gray-500">
                Branch: <span className="font-medium text-gray-700">{selectedPlan.branchName}</span> | Premium:{" "}
                <span className="font-semibold text-[#0B3D0B]">{selectedPlan.premiumPercentage}%</span>
              </p>
            )}
          </div>

          {/* 2. Customer Selector */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider">
                Existing Customer <span className="text-red-500">*</span>
              </label>
              {onOpenCreateCustomer && (
                <button
                  type="button"
                  onClick={onOpenCreateCustomer}
                  className="text-xs font-semibold text-[#0B3D0B] hover:text-emerald-800 flex items-center gap-1 transition-colors"
                >
                  <UserPlus className="w-3.5 h-3.5" />
                  Create Customer
                </button>
              )}
            </div>

            {selectedCustomerObj ? (
              <div className="flex items-center justify-between p-3 rounded-xl border border-emerald-200 bg-emerald-50/50">
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-full bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center font-bold text-xs">
                    {selectedCustomerObj.fullName.substring(0, 2).toUpperCase()}
                  </div>
                  <div>
                    <h4 className="text-sm font-bold text-gray-900">{selectedCustomerObj.fullName}</h4>
                    <p className="text-xs text-gray-600">
                      {selectedCustomerObj.phoneNumber || "No phone"} | {selectedCustomerObj.email || "No email"}
                    </p>
                    <p className="text-[11px] text-gray-500">
                      NIC: {selectedCustomerObj.nic || "N/A"} | Branch: {selectedCustomerObj.primaryBranchName}
                    </p>
                  </div>
                </div>
                {!initialCustomerId && (
                  <button
                    type="button"
                    onClick={handleClearCustomer}
                    className="p-1 rounded-lg text-gray-400 hover:text-gray-600 hover:bg-gray-200 transition-colors"
                    title="Change Customer"
                  >
                    <X className="w-4 h-4" />
                  </button>
                )}
              </div>
            ) : (
              <div className="relative">
                <div className="relative">
                  <Search className="w-4 h-4 absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
                  <input
                    id={customerSearchInputId}
                    type="text"
                    value={customerSearch}
                    onChange={(e) => setCustomerSearch(e.target.value)}
                    placeholder="Search customer by name, phone or email..."
                    className="w-full pl-10 pr-4 py-2.5 text-sm rounded-xl border border-gray-200 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
                  />
                  {searchingCustomers && (
                    <Loader2 className="w-4 h-4 absolute right-3.5 top-1/2 -translate-y-1/2 animate-spin text-gray-400" />
                  )}
                </div>

                {customerResults.length > 0 && (
                  <div className="absolute top-full left-0 right-0 mt-1 max-h-48 overflow-y-auto bg-white rounded-xl border border-gray-200 shadow-lg z-20 divide-y divide-gray-100">
                    {customerResults.map((c) => (
                      <button
                        key={c.id}
                        type="button"
                        onClick={() => handleSelectCustomer(c)}
                        className="w-full text-left px-3.5 py-2.5 hover:bg-gray-50 transition-colors flex items-center justify-between"
                      >
                        <div>
                          <div className="text-sm font-bold text-gray-900">{c.fullName}</div>
                          <div className="text-xs text-gray-500">
                            {c.phoneNumber} • {c.email}
                          </div>
                          <div className="text-[11px] text-gray-400">NIC: {c.nic || "N/A"}</div>
                        </div>
                        <span
                          className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                            c.isActive ? "bg-emerald-100 text-emerald-800" : "bg-gray-100 text-gray-600"
                          }`}
                        >
                          {c.isActive ? "Active" : "Inactive"}
                        </span>
                      </button>
                    ))}
                  </div>
                )}
                {customerSearch.trim().length > 1 && !searchingCustomers && customerResults.length === 0 && (
                  <div className="p-3 text-center text-xs text-gray-500 bg-gray-50 rounded-xl mt-1 border border-gray-100">
                    No customer match found for &quot;{customerSearch}&quot;. You can use &quot;Create Customer&quot; above.
                  </div>
                )}
              </div>
            )}
          </div>

          {/* 3. Read-Only Target Grams and Offered Duration */}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1.5">
                Plan Target (Grams) <span className="text-red-500">*</span>
              </label>
              <div className="relative">
                <input
                  type="text"
                  readOnly
                  value={
                    previewData?.targetProductGoldWeightGrams ||
                    selectedPlan?.targetProductGoldWeightGrams ||
                    ""
                  }
                  placeholder="Target grams"
                  className="w-full px-3.5 py-2.5 text-sm rounded-xl border border-gray-200 bg-gray-100 text-gray-800 font-bold focus:outline-none cursor-not-allowed"
                />
                <span className="absolute right-3.5 top-1/2 -translate-y-1/2 text-xs font-semibold text-gray-500">
                  grams
                </span>
              </div>
              <p className="mt-1 text-[11px] text-gray-400">Fixed by plan rules (staff cannot override)</p>
            </div>

            <div>
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1.5">
                Offered Duration <span className="text-red-500">*</span>
              </label>
              <select
                value={selectedDuration}
                onChange={(e) => setSelectedDuration(Number(e.target.value))}
                disabled={!selectedPlan || submitting}
                className="w-full px-3.5 py-2.5 text-sm rounded-xl border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
                required
              >
                <option value="">-- Choose Duration --</option>
                {(previewData?.allowedDurationsMonths || selectedPlan?.allowedDurationsMonths || []).map(
                  (m) => (
                    <option key={m} value={m}>
                      {m} Months
                    </option>
                  )
                )}
              </select>
              <p className="mt-1 text-[11px] text-gray-400">Configured duration options only</p>
            </div>
          </div>

          {/* 4. Repeat Enrolment Warning & Confirmation */}
          {previewData?.requiresRepeatConfirmation && (
            <div className="p-4 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 space-y-2.5">
              <div className="flex items-start gap-2.5">
                <ShieldAlert className="w-5 h-5 flex-shrink-0 text-amber-600 mt-0.5" />
                <div className="text-xs leading-relaxed">
                  <span className="font-bold">Existing Enrolments Detected:</span> This customer has joined this plan{" "}
                  <span className="font-bold underline">{previewData.totalExistingEnrolments} time(s)</span> in the
                  past ({previewData.activeExistingEnrolments} currently active).
                  <p className="mt-1 text-amber-800">
                    Repeat enrolments are allowed. Please confirm your intention to assign an additional separate enrolment.
                  </p>
                </div>
              </div>
              <label className="flex items-center gap-2.5 cursor-pointer pt-1 border-t border-amber-200/60">
                <input
                  type="checkbox"
                  checked={confirmRepeat}
                  onChange={(e) => setConfirmRepeat(e.target.checked)}
                  className="w-4 h-4 rounded text-[#0B3D0B] focus:ring-[#0B3D0B] border-gray-300"
                />
                <span className="text-xs font-bold text-amber-950">
                  I confirm assigning an additional separate enrolment for this customer
                </span>
              </label>
            </div>
          )}

          {/* 5. Ineligibility Warning */}
          {previewData && !previewData.isEligible && (
            <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2.5">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{previewData.ineligibilityReason || "Customer is ineligible for this plan."}</span>
            </div>
          )}

          {/* Summary Details */}
          {selectedPlan && selectedCustomerObj && (
            <div className="p-3.5 rounded-xl bg-[#FAF9F6] border border-gray-200 text-xs text-gray-600 space-y-1">
              <div className="flex justify-between">
                <span>Plan Premium:</span>
                <span className="font-bold text-gray-900">{selectedPlan.premiumPercentage}%</span>
              </div>
              <div className="flex justify-between">
                <span>Initial Balance:</span>
                <span className="font-bold text-gray-900">0.0000 g (No initial payment required)</span>
              </div>
              <div className="flex justify-between">
                <span>Customer Contact Status:</span>
                <span className="text-gray-700">Preserved as unverified until verified via OTP</span>
              </div>
            </div>
          )}

          {/* Modal Footer */}
          <div className="flex items-center justify-end gap-3 pt-3 border-t border-gray-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-5 py-2.5 text-xs font-bold uppercase tracking-wider rounded-xl text-gray-700 hover:bg-gray-100 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={
                submitting ||
                previewLoading ||
                (previewData !== null && !previewData.isEligible) ||
                (previewData?.requiresRepeatConfirmation === true && !confirmRepeat)
              }
              className="px-6 py-2.5 text-xs font-bold uppercase tracking-wider rounded-xl bg-[#0B3D0B] text-[#FFD700] hover:bg-[#082b08] disabled:opacity-50 disabled:cursor-not-allowed transition-all shadow-sm flex items-center gap-2"
            >
              {submitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Assigning...
                </>
              ) : (
                "Assign Plan"
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
