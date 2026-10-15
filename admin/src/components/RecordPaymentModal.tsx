"use client";

import React, { useState, useEffect, useCallback } from "react";
import {
  X,
  DollarSign,
  Scale,
  AlertCircle,
  CheckCircle2,
  Loader2,
  Sparkles,
  RefreshCw,
  ArrowRight,
  TrendingUp,
  Coins,
  ShieldCheck,
} from "lucide-react";
import { adminApi, ApiError } from "../lib/api";
import { CustomerEnrolment } from "../types/customer";
import { AdminRecordPaymentPreviewResponse } from "../types/jewelleryPlan";

interface RecordPaymentModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
  customerId?: string;
  customerName?: string;
  initialEnrolmentId?: string;
  enrolments?: CustomerEnrolment[];
}

interface LiveRateInfo {
  ratePerGram: number;
  currency: string;
  rateSource: string;
  rateAsOf: string;
  paymentDate: string;
  premiumPercentage: number;
  targetGrams: number;
  currentSavedGrams: number;
  remainingGrams: number;
}

export function RecordPaymentModal({
  isOpen,
  onClose,
  onSuccess,
  customerId,
  customerName,
  initialEnrolmentId,
  enrolments = [],
}: RecordPaymentModalProps) {
  const [availableEnrolments, setAvailableEnrolments] = useState<CustomerEnrolment[]>(enrolments);
  const [loadingEnrolments, setLoadingEnrolments] = useState(false);

  const [selectedEnrolmentId, setSelectedEnrolmentId] = useState<string>(initialEnrolmentId || "");
  const [mode, setMode] = useState<"BuyAmount" | "BuyGram">("BuyAmount");
  const [enteredAmount, setEnteredAmount] = useState<string>("");
  const [requestedGrams, setRequestedGrams] = useState<string>("");
  const [notes, setNotes] = useState<string>("");
  const [confirmedReceived, setConfirmedReceived] = useState<boolean>(false);

  // Live gold rate state
  const [liveRateInfo, setLiveRateInfo] = useState<LiveRateInfo | null>(null);
  const [rateLoading, setRateLoading] = useState(false);
  const [rateError, setRateError] = useState<string | null>(null);

  // Preview calculation state from server
  const [preview, setPreview] = useState<AdminRecordPaymentPreviewResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  // Submission state
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Load enrolments if customerId provided but enrolments list is empty
  useEffect(() => {
    if (!isOpen) return;

    if (enrolments.length > 0) {
      setAvailableEnrolments(enrolments.filter((e) => e.status === "Active"));
    } else if (customerId) {
      setLoadingEnrolments(true);
      adminApi
        .getCustomerJewelleryEnrolments(customerId)
        .then((res) => {
          setAvailableEnrolments(res.filter((e) => e.status === "Active"));
        })
        .catch((err) => {
          console.error("Failed to load customer enrolments for payment modal:", err);
        })
        .finally(() => {
          setLoadingEnrolments(false);
        });
    }
  }, [isOpen, customerId, enrolments]);

  // Set initial enrolment
  useEffect(() => {
    if (!isOpen) return;

    if (initialEnrolmentId) {
      setSelectedEnrolmentId(initialEnrolmentId);
    } else if (availableEnrolments.length > 0 && !selectedEnrolmentId) {
      setSelectedEnrolmentId(availableEnrolments[0].id);
    }
  }, [isOpen, initialEnrolmentId, availableEnrolments, selectedEnrolmentId]);

  // Reset form when opened / closed
  useEffect(() => {
    if (!isOpen) {
      setPreview(null);
      setPreviewError(null);
      setSubmitError(null);
      setConfirmedReceived(false);
      setEnteredAmount("");
      setRequestedGrams("");
      setNotes("");
    }
  }, [isOpen]);

  const selectedEnrolment = availableEnrolments.find((e) => e.id === selectedEnrolmentId);

  // Fetch initial live gold rate & enrolment snapshot
  const fetchLiveRate = useCallback(async (enrolmentId: string) => {
    if (!enrolmentId) return;
    setRateLoading(true);
    setRateError(null);
    try {
      const res = await adminApi.previewInStorePayment(enrolmentId, {
        mode: "BuyAmount",
      });
      setLiveRateInfo({
        ratePerGram: res.ratePerGram,
        currency: res.currency || "LKR",
        rateSource: res.rateSource,
        rateAsOf: res.rateAsOf,
        paymentDate: res.paymentDate,
        premiumPercentage: res.premiumPercentage,
        targetGrams: res.targetGrams,
        currentSavedGrams: res.currentSavedGrams,
        remainingGrams: res.remainingGramsBeforePayment,
      });
    } catch (err: any) {
      console.error("Failed to fetch initial live rate:", err);
      setRateError(err.message || "Failed to fetch live 24K gold rate.");
    } finally {
      setRateLoading(false);
    }
  }, []);

  // Fetch rate whenever modal opens or enrolment changes
  useEffect(() => {
    if (isOpen && selectedEnrolmentId) {
      fetchLiveRate(selectedEnrolmentId);
    }
  }, [isOpen, selectedEnrolmentId, fetchLiveRate]);

  // Debounced server preview calculation
  useEffect(() => {
    if (!selectedEnrolmentId) {
      setPreview(null);
      return;
    }

    const numAmount = parseFloat(enteredAmount);
    const numGrams = parseFloat(requestedGrams);

    const hasValidAmount = mode === "BuyAmount" && !isNaN(numAmount) && numAmount > 0;
    const hasValidGrams = mode === "BuyGram" && !isNaN(numGrams) && numGrams > 0;

    if (!hasValidAmount && !hasValidGrams) {
      setPreview(null);
      setPreviewError(null);
      return;
    }

    let active = true;
    const timer = setTimeout(async () => {
      setPreviewLoading(true);
      setPreviewError(null);

      try {
        const res = await adminApi.previewInStorePayment(selectedEnrolmentId, {
          mode,
          enteredAmount: mode === "BuyAmount" ? numAmount : undefined,
          requestedGrams: mode === "BuyGram" ? numGrams : undefined,
        });

        if (active) {
          setPreview(res);
          // Keep live rate metadata freshly synchronized
          setLiveRateInfo({
            ratePerGram: res.ratePerGram,
            currency: res.currency || "LKR",
            rateSource: res.rateSource,
            rateAsOf: res.rateAsOf,
            paymentDate: res.paymentDate,
            premiumPercentage: res.premiumPercentage,
            targetGrams: res.targetGrams,
            currentSavedGrams: res.currentSavedGrams,
            remainingGrams: res.remainingGramsBeforePayment,
          });
        }
      } catch (err: any) {
        if (active) {
          setPreview(null);
          setPreviewError(err.message || "Failed to calculate payment preview.");
        }
      } finally {
        if (active) setPreviewLoading(false);
      }
    }, 350);

    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [selectedEnrolmentId, mode, enteredAmount, requestedGrams]);

  // Active rate & enrolment metrics
  const activeRate = preview?.ratePerGram || liveRateInfo?.ratePerGram || 0;
  const activePremiumPct =
    preview?.premiumPercentage ??
    liveRateInfo?.premiumPercentage ??
    (selectedEnrolment?.premiumPercentage || 0);
  const activeRemainingGrams =
    preview?.remainingGramsBeforePayment ??
    liveRateInfo?.remainingGrams ??
    (selectedEnrolment?.remainingGrams || 0);
  const activeTargetGrams =
    preview?.targetGrams ??
    liveRateInfo?.targetGrams ??
    (selectedEnrolment?.targetWeightGrams || 0);
  const activeSavedGrams =
    preview?.currentSavedGrams ??
    liveRateInfo?.currentSavedGrams ??
    (selectedEnrolment?.totalSavedGrams || 0);

  const numAmount = parseFloat(enteredAmount) || 0;
  const numGrams = parseFloat(requestedGrams) || 0;
  const hasValidInput = mode === "BuyAmount" ? numAmount > 0 : numGrams > 0;

  // Exact maximum cash needed to pay off the remaining target in BuyAmount mode:
  // A = (RemainingGrams * R) / (1 - p)
  const maxPayableForRemaining =
    activeRate > 0 && activeRemainingGrams > 0 && activePremiumPct < 100
      ? Math.round((activeRemainingGrams * activeRate) / (1 - activePremiumPct / 100))
      : 0;

  // Real-time client-side instant conversion calculation (0ms typing latency)
  let instantAmountPayable = 0;
  let instantCreditedGrams = 0;
  let instantBaseGoldGrams = 0;
  let instantPremiumGrams = 0;
  let instantBaseAmount = 0;
  let instantPremiumAmount = 0;
  let instantWillExceed = false;
  let instantWillComplete = false;

  if (activeRate > 0) {
    const p = activePremiumPct / 100;
    if (mode === "BuyAmount" && numAmount > 0) {
      instantAmountPayable = numAmount;
      instantBaseGoldGrams = numAmount / activeRate;
      instantPremiumGrams = instantBaseGoldGrams * p;
      // Floor down to 4 decimal places matching backend logic
      const rawCredit = instantBaseGoldGrams - instantPremiumGrams;
      instantCreditedGrams = Math.floor(rawCredit * 10000) / 10000;
      instantWillExceed = instantCreditedGrams > activeRemainingGrams;
      instantWillComplete = activeRemainingGrams > 0 && instantCreditedGrams >= activeRemainingGrams;
    } else if (mode === "BuyGram" && numGrams > 0) {
      instantCreditedGrams = numGrams;
      instantBaseGoldGrams = numGrams;
      instantPremiumGrams = numGrams * p;
      instantBaseAmount = Math.round(numGrams * activeRate * 100) / 100;
      instantPremiumAmount = Math.round(numGrams * p * activeRate * 100) / 100;
      instantAmountPayable = Math.round((instantBaseAmount + instantPremiumAmount) * 100) / 100;
      instantWillExceed = instantCreditedGrams > activeRemainingGrams;
      instantWillComplete = activeRemainingGrams > 0 && instantCreditedGrams >= activeRemainingGrams;
    }
  }

  // Display metrics: prefer verified server preview when available, fallback to instant calculations
  const displayAmountPayable = preview ? preview.amountPayable : instantAmountPayable;
  const displayCreditedGrams = preview ? preview.netCreditedGrams : instantCreditedGrams;
  const displayBaseGoldGrams = preview ? preview.baseGoldGrams : instantBaseGoldGrams;
  const displayPremiumGrams = preview ? preview.premiumGrams : instantPremiumGrams;
  const displayBaseAmount =
    mode === "BuyGram"
      ? instantBaseAmount || (preview ? displayBaseGoldGrams * activeRate : 0)
      : undefined;
  const displayPremiumAmount =
    mode === "BuyGram"
      ? instantPremiumAmount || (preview ? displayPremiumGrams * activeRate : 0)
      : undefined;

  const displayWillExceed = preview ? preview.willExceedTarget : instantWillExceed;
  const displayWillComplete = preview ? preview.willReachCompletion : instantWillComplete;
  const displayRemainingAfter = preview
    ? preview.remainingGramsAfterPayment
    : Math.max(0, activeRemainingGrams - instantCreditedGrams);
  const displayNewSavedTotal = activeSavedGrams + displayCreditedGrams;
  const displayNewProgress =
    activeTargetGrams > 0
      ? Math.min(100, Math.round((displayNewSavedTotal / activeTargetGrams) * 1000) / 10)
      : 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedEnrolmentId) {
      setSubmitError("Please select an eligible enrolment.");
      return;
    }

    if (!confirmedReceived) {
      setSubmitError("Staff confirmation of cash receipt is strictly required.");
      return;
    }

    if (displayWillExceed) {
      setSubmitError(
        `Payment exceeds remaining target (${activeRemainingGrams.toFixed(4)} g). Please adjust amount.`
      );
      return;
    }

    if (!hasValidInput) {
      setSubmitError("Please enter a valid amount or gram weight.");
      return;
    }

    setSubmitting(true);
    setSubmitError(null);

    const idempotencyKey = `CASH-REC-${Date.now()}-${Math.random().toString(36).substring(2, 7)}`;

    try {
      const result = await adminApi.recordInStorePayment(selectedEnrolmentId, {
        mode,
        enteredAmount: mode === "BuyAmount" ? numAmount : undefined,
        requestedGrams: mode === "BuyGram" ? numGrams : undefined,
        notes: notes.trim() || undefined,
        confirmedReceived: true,
        idempotencyKey,
      });

      onSuccess(
        result.planCompleted
          ? `In-store cash payment of LKR ${result.amountPaid.toLocaleString()} recorded successfully! Plan target completed!`
          : `In-store cash payment of LKR ${result.amountPaid.toLocaleString()} recorded successfully (+${result.netCreditedGrams}g credited).`
      );
      onClose();
    } catch (err: any) {
      setSubmitError(err.message || "Failed to record payment.");
    } finally {
      setSubmitting(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-in fade-in duration-200">
      <div className="relative w-full max-w-2xl bg-white rounded-2xl shadow-2xl border border-gray-100 overflow-hidden flex flex-col max-h-[92vh]">
        {/* Modal Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-[#FAF9F6]">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center shadow-sm">
              <DollarSign className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-lg font-bold text-gray-900 leading-tight">Record In-Store Payment</h2>
                <span className="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800">
                  Cash Counter
                </span>
              </div>
              <p className="text-xs text-gray-500">
                Collect cash and credit gold grams to customer jewellery enrolment
              </p>
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

        {/* Modal Form Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-5">
          {submitError && (
            <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-start gap-2.5">
              <AlertCircle className="w-4 h-4 flex-shrink-0 text-red-600 mt-0.5" />
              <div className="flex-1 leading-relaxed">{submitError}</div>
            </div>
          )}

          {/* Customer & Date Info Strip */}
          <div className="p-3.5 rounded-xl bg-emerald-50/70 border border-emerald-100 flex items-center justify-between text-xs">
            <div>
              <span className="text-gray-500">Customer: </span>
              <span className="font-bold text-gray-900">
                {customerName || selectedEnrolment?.planName || "Authorized Customer"}
              </span>
            </div>
            <div className="text-right">
              <span className="text-gray-500">Receipt Date: </span>
              <span className="font-bold text-[#0B3D0B]">
                {liveRateInfo?.paymentDate || new Date().toISOString().split("T")[0]} (Today Only)
              </span>
            </div>
          </div>

          {/* 1. Target Jewellery Enrolment Selector */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider">
                Target Jewellery Enrolment <span className="text-red-500">*</span>
              </label>
              {selectedEnrolment && (
                <span className="text-xs text-gray-500">
                  Enrolment Ref: <span className="font-mono font-bold text-gray-800">{selectedEnrolment.enrolmentReference}</span>
                </span>
              )}
            </div>
            <select
              value={selectedEnrolmentId}
              onChange={(e) => {
                setSelectedEnrolmentId(e.target.value);
                setPreview(null);
                setEnteredAmount("");
                setRequestedGrams("");
              }}
              disabled={submitting || loadingEnrolments}
              className="w-full px-3.5 py-2.5 text-sm rounded-xl border border-gray-200 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
              required
            >
              <option value="">-- Choose Enrolment --</option>
              {availableEnrolments.map((enr) => (
                <option key={enr.id} value={enr.id}>
                  {enr.planName} ({enr.enrolmentReference}) • {enr.totalSavedGrams}g / {enr.targetWeightGrams}g (
                  {enr.progressPercentage}%)
                </option>
              ))}
            </select>

            {selectedEnrolment && (
              <div className="mt-2 grid grid-cols-3 gap-2 p-2.5 rounded-xl bg-gray-50 border border-gray-100 text-xs text-gray-600">
                <div>
                  <span className="text-gray-400 block text-[10px] uppercase font-semibold">Total Target</span>
                  <span className="font-bold text-gray-900">{activeTargetGrams.toFixed(4)} g</span>
                </div>
                <div>
                  <span className="text-gray-400 block text-[10px] uppercase font-semibold">Saved So Far</span>
                  <span className="font-bold text-emerald-800">{activeSavedGrams.toFixed(4)} g</span>
                </div>
                <div>
                  <span className="text-gray-400 block text-[10px] uppercase font-semibold">Remaining Target</span>
                  <span className="font-bold text-[#0B3D0B]">{activeRemainingGrams.toFixed(4)} g</span>
                </div>
              </div>
            )}
          </div>

          {/* LIVE GOLD RATE BANNER */}
          <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-[#0B3D0B] via-[#0d470d] to-[#082b08] p-4 text-white shadow-md border border-[#FFD700]/30">
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2">
                <span className="relative flex h-2.5 w-2.5">
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                  <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-400"></span>
                </span>
                <span className="text-[11px] font-extrabold uppercase tracking-wider text-emerald-300 flex items-center gap-1.5">
                  <Sparkles className="w-3.5 h-3.5 text-[#FFD700]" />
                  Live 24K Gold Rate
                </span>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-white/10 text-white/90 border border-white/10 font-semibold">
                  {liveRateInfo?.rateSource === "Live"
                    ? "Live Market Feed"
                    : liveRateInfo?.rateSource || "Active Feed"}
                </span>
              </div>

              <button
                type="button"
                onClick={() => selectedEnrolmentId && fetchLiveRate(selectedEnrolmentId)}
                disabled={rateLoading}
                title="Refresh Live Gold Rate"
                className="px-2.5 py-1 rounded-lg bg-white/10 hover:bg-white/20 text-white/90 hover:text-white transition-all text-xs font-semibold flex items-center gap-1.5 disabled:opacity-50"
              >
                <RefreshCw className={`w-3.5 h-3.5 ${rateLoading ? "animate-spin" : ""}`} />
                <span>Refresh</span>
              </button>
            </div>

            <div className="mt-3 flex items-baseline justify-between flex-wrap gap-2">
              <div>
                <div className="flex items-baseline gap-2">
                  <span className="text-3xl font-black text-[#FFD700] tracking-tight">
                    LKR {(activeRate || 0).toLocaleString(undefined, {
                      minimumFractionDigits: 2,
                      maximumFractionDigits: 2,
                    })}
                  </span>
                  <span className="text-xs text-emerald-100 font-bold">/ gram (24K)</span>
                </div>
                <p className="text-[11px] text-emerald-100/80 mt-0.5">
                  Effective as of:{" "}
                  {liveRateInfo?.rateAsOf
                    ? new Date(liveRateInfo.rateAsOf).toLocaleTimeString([], {
                        hour: "2-digit",
                        minute: "2-digit",
                        second: "2-digit",
                      })
                    : "Live Now"}{" "}
                  • Valid for transactions today
                </p>
              </div>

              <div className="text-right">
                <span className="text-[10px] text-emerald-200/80 uppercase font-semibold block">
                  Enrolment Premium
                </span>
                <span className="text-sm font-bold text-white bg-white/15 px-2.5 py-0.5 rounded-lg border border-white/10 inline-block mt-0.5">
                  +{activePremiumPct}%
                </span>
              </div>
            </div>

            {rateError && (
              <div className="mt-2.5 p-2 rounded-lg bg-red-900/40 border border-red-500/40 text-red-200 text-xs flex items-center gap-2">
                <AlertCircle className="w-3.5 h-3.5 flex-shrink-0" />
                <span>{rateError}</span>
              </div>
            )}
          </div>

          {/* 2. Method of Payment / Mode Selector */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider">
                Method of Payment / Conversion Mode <span className="text-red-500">*</span>
              </label>
              <span className="text-[11px] text-gray-500 font-medium">
                {mode === "BuyAmount"
                  ? "Enter cash LKR ➔ Computes gold credited"
                  : "Enter gold grams ➔ Computes cash to collect"}
              </span>
            </div>

            <div className="grid grid-cols-2 gap-2 p-1 rounded-xl bg-gray-100 border border-gray-200">
              <button
                type="button"
                onClick={() => {
                  setMode("BuyAmount");
                  setRequestedGrams("");
                }}
                className={`py-2.5 px-3 text-xs font-bold rounded-lg transition-all flex items-center justify-center gap-2 ${
                  mode === "BuyAmount"
                    ? "bg-white text-[#0B3D0B] shadow-sm font-extrabold ring-1 ring-black/5"
                    : "text-gray-600 hover:text-gray-900"
                }`}
              >
                <DollarSign className="w-4 h-4 text-emerald-700" />
                <span>Buy Amount (LKR)</span>
              </button>

              <button
                type="button"
                onClick={() => {
                  setMode("BuyGram");
                  setEnteredAmount("");
                }}
                className={`py-2.5 px-3 text-xs font-bold rounded-lg transition-all flex items-center justify-center gap-2 ${
                  mode === "BuyGram"
                    ? "bg-white text-[#0B3D0B] shadow-sm font-extrabold ring-1 ring-black/5"
                    : "text-gray-600 hover:text-gray-900"
                }`}
              >
                <Scale className="w-4 h-4 text-[#0B3D0B]" />
                <span>Buy Gold (Grams)</span>
              </button>
            </div>
          </div>

          {/* 3. Input Value with Quick Action Preset Chips */}
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider">
                {mode === "BuyAmount" ? "Enter Cash Amount to Collect *" : "Enter Gold Weight to Purchase *"}
              </label>
              <span className="text-xs text-gray-400 font-medium">
                {mode === "BuyAmount" ? "Currency: LKR" : "Unit: 24K Grams"}
              </span>
            </div>

            <div className="relative">
              <input
                type="number"
                step={mode === "BuyAmount" ? "100" : "0.0001"}
                min="0.0001"
                value={mode === "BuyAmount" ? enteredAmount : requestedGrams}
                onChange={(e) => {
                  if (mode === "BuyAmount") {
                    setEnteredAmount(e.target.value);
                  } else {
                    setRequestedGrams(e.target.value);
                  }
                }}
                placeholder={mode === "BuyAmount" ? "e.g. 50000" : "e.g. 1.0000"}
                className="w-full pl-3.5 pr-16 py-3 text-base font-bold text-gray-900 rounded-xl border border-gray-200 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
                required
              />
              <span className="absolute right-3.5 top-1/2 -translate-y-1/2 text-xs font-extrabold text-gray-500 uppercase tracking-wider bg-gray-100 px-2 py-1 rounded-md">
                {mode === "BuyAmount" ? "LKR" : "Grams"}
              </span>
            </div>

            {/* Quick helper preset chips */}
            <div className="flex items-center gap-1.5 flex-wrap pt-1">
              <span className="text-[11px] font-semibold text-gray-400 mr-1 flex items-center gap-1">
                <Coins className="w-3 h-3 text-[#0B3D0B]" /> Quick:
              </span>
              {mode === "BuyAmount" ? (
                <>
                  {[10000, 25000, 50000, 100000].map((amt) => (
                    <button
                      key={amt}
                      type="button"
                      onClick={() => setEnteredAmount(amt.toString())}
                      className="px-2.5 py-1 text-[11px] font-bold rounded-lg bg-gray-100 hover:bg-emerald-50 hover:text-emerald-900 text-gray-700 transition-colors border border-gray-200/80"
                    >
                      +{amt.toLocaleString()}
                    </button>
                  ))}
                  {maxPayableForRemaining > 0 && (
                    <button
                      type="button"
                      onClick={() => setEnteredAmount(maxPayableForRemaining.toString())}
                      title="Amount needed to complete remaining grams"
                      className="px-2.5 py-1 text-[11px] font-bold rounded-lg bg-amber-50 hover:bg-amber-100 text-amber-900 transition-colors border border-amber-200"
                    >
                      Pay Full Remaining (~LKR {maxPayableForRemaining.toLocaleString()})
                    </button>
                  )}
                </>
              ) : (
                <>
                  {[0.5, 1.0, 2.0, 5.0].map((g) => (
                    <button
                      key={g}
                      type="button"
                      onClick={() => setRequestedGrams(g.toFixed(4))}
                      className="px-2.5 py-1 text-[11px] font-bold rounded-lg bg-gray-100 hover:bg-emerald-50 hover:text-emerald-900 text-gray-700 transition-colors border border-gray-200/80"
                    >
                      +{g.toFixed(1)} g
                    </button>
                  ))}
                  {activeRemainingGrams > 0 && (
                    <button
                      type="button"
                      onClick={() => setRequestedGrams(activeRemainingGrams.toFixed(4))}
                      title="Grams needed to complete remaining target"
                      className="px-2.5 py-1 text-[11px] font-bold rounded-lg bg-amber-50 hover:bg-amber-100 text-amber-900 transition-colors border border-amber-200"
                    >
                      Pay Full Remaining ({activeRemainingGrams.toFixed(4)} g)
                    </button>
                  )}
                </>
              )}
            </div>
          </div>

          {/* 4. REAL-TIME CONVERSION & BREAKDOWN DISPLAY */}
          {previewLoading && (
            <div className="p-3.5 rounded-xl bg-emerald-50/50 border border-emerald-100 flex items-center justify-center gap-2 text-xs text-[#0B3D0B]">
              <Loader2 className="w-4 h-4 animate-spin text-[#0B3D0B]" />
              Syncing live conversion with market rate engine...
            </div>
          )}

          {previewError && (
            <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-red-800 text-xs flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
              <span>{previewError}</span>
            </div>
          )}

          {hasValidInput && activeRate > 0 && (
            <div className="p-4 rounded-2xl bg-[#FAF9F6] border-2 border-[#0B3D0B]/15 space-y-4 shadow-sm">
              {/* Conversion Flow Banner */}
              <div className="flex items-center justify-between pb-3 border-b border-gray-200">
                <div className="flex items-center gap-1.5 text-xs font-bold text-gray-700 uppercase tracking-wide">
                  <TrendingUp className="w-3.5 h-3.5 text-[#0B3D0B]" />
                  <span>
                    {mode === "BuyAmount"
                      ? "Cash Amount (LKR) ➔ Net Gold Credited (Grams)"
                      : "Requested Gold (Grams) ➔ Total Cash Payable (LKR)"}
                  </span>
                </div>
                <span className="text-[11px] font-bold text-[#0B3D0B] bg-emerald-100/80 px-2 py-0.5 rounded-md">
                  @ LKR {activeRate.toLocaleString(undefined, { minimumFractionDigits: 2 })} / g
                </span>
              </div>

              {/* Main Visual Conversion Box */}
              <div className="grid grid-cols-1 sm:grid-cols-5 gap-3 items-center">
                {/* Input side */}
                <div className="sm:col-span-2 p-3 rounded-xl bg-white border border-gray-200 shadow-xs">
                  <span className="text-[10px] uppercase font-bold text-gray-400 block mb-0.5">
                    {mode === "BuyAmount" ? "Cash Received" : "Gold Purchased"}
                  </span>
                  <div className="text-lg font-black text-gray-900 truncate">
                    {mode === "BuyAmount"
                      ? `LKR ${numAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                      : `${numGrams.toFixed(4)} g`}
                  </div>
                  <span className="text-[10px] text-gray-500 font-medium">
                    {mode === "BuyAmount" ? "Physical Cash" : "24K Fine Gold"}
                  </span>
                </div>

                {/* Conversion Direction Icon */}
                <div className="sm:col-span-1 flex flex-col items-center justify-center py-1">
                  <div className="w-8 h-8 rounded-full bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center shadow-xs">
                    <ArrowRight className="w-4 h-4" />
                  </div>
                  <span className="text-[9px] font-bold text-gray-400 mt-1 uppercase">Converts</span>
                </div>

                {/* Converted Output side */}
                <div className="sm:col-span-2 p-3 rounded-xl bg-gradient-to-br from-emerald-50 to-white border-2 border-emerald-300 shadow-xs">
                  <span className="text-[10px] uppercase font-bold text-emerald-800 block mb-0.5">
                    {mode === "BuyAmount" ? "Net Gold to Credit" : "Total Cash to Collect"}
                  </span>
                  <div className="text-xl font-black text-[#0B3D0B] truncate">
                    {mode === "BuyAmount"
                      ? `+${displayCreditedGrams.toFixed(4)} g`
                      : `LKR ${displayAmountPayable.toLocaleString(undefined, {
                          minimumFractionDigits: 2,
                        })}`}
                  </div>
                  <span className="text-[10px] font-bold text-emerald-700">
                    {mode === "BuyAmount"
                      ? "Credited to customer plan"
                      : "Collect at counter from customer"}
                  </span>
                </div>
              </div>

              {/* Itemized Calculation Breakdown */}
              <div className="p-3 rounded-xl bg-white border border-gray-200 text-xs space-y-1.5">
                <div className="flex justify-between text-gray-600">
                  <span>Applied Live 24K Rate:</span>
                  <span className="font-semibold text-gray-900">
                    LKR {activeRate.toLocaleString(undefined, { minimumFractionDigits: 2 })} / g
                  </span>
                </div>

                {mode === "BuyAmount" ? (
                  <>
                    <div className="flex justify-between text-gray-600">
                      <span>Gross Base Gold (Cash ÷ Rate):</span>
                      <span className="font-mono text-gray-900">{displayBaseGoldGrams.toFixed(4)} g</span>
                    </div>
                    <div className="flex justify-between text-amber-800">
                      <span>Plan Premium Deduction ({activePremiumPct}%):</span>
                      <span className="font-mono font-medium">-{displayPremiumGrams.toFixed(4)} g</span>
                    </div>
                    <div className="flex justify-between font-bold text-[#0B3D0B] pt-1.5 border-t border-gray-100 text-sm">
                      <span>Net Gold Credited to Customer:</span>
                      <span className="font-mono font-black">+{displayCreditedGrams.toFixed(4)} g</span>
                    </div>
                  </>
                ) : (
                  <>
                    <div className="flex justify-between text-gray-600">
                      <span>Base Gold Value (Grams × Rate):</span>
                      <span className="font-mono text-gray-900">
                        LKR{" "}
                        {(displayBaseAmount ?? (numGrams * activeRate)).toLocaleString(undefined, {
                          minimumFractionDigits: 2,
                        })}
                      </span>
                    </div>
                    <div className="flex justify-between text-amber-800">
                      <span>Plan Premium ({activePremiumPct}%):</span>
                      <span className="font-mono font-medium">
                        +LKR{" "}
                        {(
                          displayPremiumAmount ?? (numGrams * activeRate * (activePremiumPct / 100))
                        ).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                    <div className="flex justify-between font-bold text-[#0B3D0B] pt-1.5 border-t border-gray-100 text-sm">
                      <span>Total Cash to Collect from Customer:</span>
                      <span className="font-mono font-black">
                        LKR{" "}
                        {displayAmountPayable.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                  </>
                )}

                {/* Progress preview */}
                <div className="pt-2 border-t border-gray-100 space-y-1">
                  <div className="flex justify-between text-[11px] text-gray-500 font-medium">
                    <span>Target Progress Impact:</span>
                    <span>
                      {activeSavedGrams.toFixed(4)} g ➔{" "}
                      <strong className="text-gray-900">{displayNewSavedTotal.toFixed(4)} g</strong> of{" "}
                      {activeTargetGrams.toFixed(4)} g ({displayNewProgress}%)
                    </span>
                  </div>
                  <div className="w-full bg-gray-100 rounded-full h-1.5 overflow-hidden">
                    <div
                      className="bg-[#0B3D0B] h-full rounded-full transition-all duration-300"
                      style={{ width: `${Math.min(100, displayNewProgress)}%` }}
                    />
                  </div>
                  <div className="flex justify-between text-[11px] text-gray-500">
                    <span>Remaining After Payment:</span>
                    <span className="font-semibold text-gray-800">
                      {displayRemainingAfter.toFixed(4)} g
                    </span>
                  </div>
                </div>
              </div>

              {/* Status Alert Flags */}
              {displayWillComplete && !displayWillExceed && (
                <div className="p-3 rounded-xl bg-emerald-100 text-emerald-900 text-xs font-bold flex items-center gap-2.5 border border-emerald-200">
                  <CheckCircle2 className="w-4 h-4 text-emerald-700 flex-shrink-0" />
                  <span>
                    🎉 This payment completes the remaining target weight! Enrolment will be marked as Completed.
                  </span>
                </div>
              )}

              {displayWillExceed && (
                <div className="p-3 rounded-xl bg-red-100 text-red-900 text-xs font-bold flex items-start gap-2.5 border border-red-200">
                  <AlertCircle className="w-4 h-4 text-red-700 flex-shrink-0 mt-0.5" />
                  <div>
                    Entered payment would credit {displayCreditedGrams.toFixed(4)} g, exceeding the remaining
                    target ({activeRemainingGrams.toFixed(4)} g). Please adjust the payment to not exceed the
                    plan target.
                  </div>
                </div>
              )}
            </div>
          )}

          {!hasValidInput && activeRate > 0 && (
            <div className="p-4 rounded-xl bg-gray-50 border border-dashed border-gray-300 text-center text-xs text-gray-500 space-y-1">
              <Coins className="w-6 h-6 text-gray-400 mx-auto" />
              <p className="font-semibold text-gray-700">
                {mode === "BuyAmount"
                  ? "Enter a cash amount (LKR) above to see real-time conversion into gold grams"
                  : "Enter gold grams above to see real-time conversion into cash amount to collect"}
              </p>
              <p className="text-[11px] text-gray-400">
                Live rate of LKR {activeRate.toLocaleString(undefined, { minimumFractionDigits: 2 })} / g will be applied immediately.
              </p>
            </div>
          )}

          {/* 5. Notes / Remarks */}
          <div>
            <label className="block text-xs font-semibold text-gray-700 uppercase tracking-wider mb-1.5">
              Cash Receipt Remarks / Notes (Optional)
            </label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="e.g. Received at Colombo counter 2 by cashier"
              className="w-full px-3.5 py-2.5 text-sm rounded-xl border border-gray-200 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/30 focus:border-[#0B3D0B] transition-colors"
            />
          </div>

          {/* 6. Explicit Confirmation Checkbox */}
          <div className="p-3.5 rounded-xl bg-amber-50/90 border border-amber-200">
            <label className="flex items-start gap-2.5 cursor-pointer">
              <input
                type="checkbox"
                checked={confirmedReceived}
                onChange={(e) => setConfirmedReceived(e.target.checked)}
                className="mt-0.5 w-4 h-4 rounded text-[#0B3D0B] focus:ring-[#0B3D0B] border-gray-300"
                required
              />
              <span className="text-xs font-bold text-amber-950 leading-relaxed">
                I explicitly confirm physical receipt of{" "}
                <span className="underline decoration-amber-900 font-black">
                  {hasValidInput
                    ? `LKR ${displayAmountPayable.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                    : "the specified cash amount"}
                </span>{" "}
                in-store today to credit{" "}
                <span className="underline decoration-amber-900 font-black">
                  {hasValidInput ? `+${displayCreditedGrams.toFixed(4)} g` : "the corresponding gold grams"}
                </span>{" "}
                to customer enrolment ({selectedEnrolment?.enrolmentReference || "selected plan"}).
              </span>
            </label>
          </div>

          {/* Modal Footer Buttons */}
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
                rateLoading ||
                !hasValidInput ||
                displayWillExceed ||
                !confirmedReceived
              }
              className="px-6 py-2.5 text-xs font-bold uppercase tracking-wider rounded-xl bg-[#0B3D0B] text-[#FFD700] hover:bg-[#082b08] disabled:opacity-50 disabled:cursor-not-allowed transition-all shadow-sm flex items-center gap-2"
            >
              {submitting ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Recording Cash...
                </>
              ) : hasValidInput ? (
                `Record LKR ${displayAmountPayable.toLocaleString(undefined, {
                  maximumFractionDigits: 0,
                })} (+${displayCreditedGrams.toFixed(4)}g)`
              ) : (
                "Record Payment"
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
