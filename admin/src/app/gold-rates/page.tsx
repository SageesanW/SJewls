"use client";

import React, { useEffect, useState, useCallback } from "react";
import { useAuth } from "../../lib/auth-context";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, ApiError } from "../../lib/api";
import {
  GoldRateOverview,
  GoldRateHistoryItem,
  SetGoldRateOverrideRequest,
} from "../../types/goldRate";
import {
  Scale,
  RefreshCw,
  TrendingUp,
  ShieldCheck,
  CheckCircle2,
  AlertCircle,
  Radio,
  Building2,
  SlidersHorizontal,
  Clock,
  ArrowRight,
  Info,
  Calendar,
  Sparkles,
} from "lucide-react";

export default function GoldRatesPage() {
  const { user } = useAuth();

  // State: Data
  const [overview, setOverview] = useState<GoldRateOverview | null>(null);
  const [history, setHistory] = useState<GoldRateHistoryItem[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [saving, setSaving] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // State: Filter Scope
  const [selectedBranchId, setSelectedBranchId] = useState<string>(""); // "" = Global

  // State: Override Form
  const [rate24k, setRate24k] = useState<string>("33600");
  const [rate22k, setRate22k] = useState<string>("30800");
  const [isOverrideEnabled, setIsOverrideEnabled] = useState<boolean>(true);
  const [reason, setReason] = useState<string>("Daily store retail rate approved by Management");

  const loadData = useCallback(async (branchId?: string) => {
    try {
      setLoading(true);
      setError(null);
      const targetBranch = branchId || undefined;
      const [overviewData, historyData] = await Promise.all([
        adminApi.getGoldRateOverview(targetBranch),
        adminApi.getGoldRateHistory(targetBranch, 30),
      ]);

      setOverview(overviewData);
      setHistory(historyData);

      // Pre-fill form values with current effective or manual rates
      if (overviewData.manualOverride) {
        setRate24k(overviewData.manualOverride.rate24k.toString());
        setRate22k(overviewData.manualOverride.rate22k.toString());
        setIsOverrideEnabled(overviewData.manualOverride.isEnabled);
        if (overviewData.manualOverride.reason) {
          setReason(overviewData.manualOverride.reason);
        }
      } else {
        setRate24k(overviewData.effectiveRate.rate24k.toString());
        setRate22k(overviewData.effectiveRate.rate22k.toString());
        setIsOverrideEnabled(true);
      }
    } catch (err: any) {
      setError(err?.message || "Failed to load gold rate information.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData(selectedBranchId);
  }, [loadData, selectedBranchId]);

  const handleSaveOverride = async (e: React.FormEvent) => {
    e.preventDefault();
    const r24 = parseFloat(rate24k);
    const r22 = parseFloat(rate22k);

    if (isNaN(r24) || r24 <= 0) {
      setError("Please enter a valid positive 24K gold rate per gram.");
      return;
    }

    if (isNaN(r22) || r22 <= 0) {
      setError("Please enter a valid positive 22K gold rate per gram.");
      return;
    }

    try {
      setSaving(true);
      setError(null);
      setSuccessMessage(null);

      const payload: SetGoldRateOverrideRequest = {
        branchId: selectedBranchId ? selectedBranchId : null,
        rate24k: r24,
        rate22k: r22,
        isEnabled: isOverrideEnabled,
        reason: reason.trim() || undefined,
      };

      const updatedOverview = await adminApi.setGoldRateOverride(payload);
      setOverview(updatedOverview);

      // Refresh history
      const freshHistory = await adminApi.getGoldRateHistory(selectedBranchId || undefined, 30);
      setHistory(freshHistory);

      setSuccessMessage("Authoritative store gold rate successfully saved and published!");
      setTimeout(() => setSuccessMessage(null), 5000);
    } catch (err: any) {
      setError(err?.message || "Failed to set gold rate override.");
    } finally {
      setSaving(false);
    }
  };

  const handleToggleOnly = async (enabled: boolean) => {
    try {
      setSaving(true);
      setError(null);
      setSuccessMessage(null);

      const updatedOverview = await adminApi.toggleGoldRateOverride({
        branchId: selectedBranchId ? selectedBranchId : null,
        isEnabled: enabled,
      });
      setOverview(updatedOverview);
      setIsOverrideEnabled(enabled);

      const freshHistory = await adminApi.getGoldRateHistory(selectedBranchId || undefined, 30);
      setHistory(freshHistory);

      setSuccessMessage(
        enabled
          ? "Manual gold rate override turned ON. Store rates are now authoritative."
          : "Manual override turned OFF. System is now reading live market spot feeds."
      );
      setTimeout(() => setSuccessMessage(null), 6000);
    } catch (err: any) {
      setError(err?.message || "Failed to toggle override status.");
    } finally {
      setSaving(false);
    }
  };

  const handleAutoCalc22k = () => {
    const r24 = parseFloat(rate24k);
    if (!isNaN(r24) && r24 > 0) {
      const calculated = Math.round((r24 * 22) / 24);
      setRate22k(calculated.toString());
    }
  };

  const formatLkr = (num?: number | null) => {
    if (num === undefined || num === null || isNaN(num)) return "--";
    return Number(num).toLocaleString("en-US", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  };

  const formatDate = (dateStr?: string | null) => {
    if (!dateStr) return "--";
    try {
      return new Date(dateStr).toLocaleString("en-GB", {
        timeZone: "Asia/Colombo",
        day: "2-digit",
        month: "short",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      });
    } catch {
      return dateStr;
    }
  };

  return (
    <AdminLayout title="Gold Rate Authority">
      <div className="space-y-6 max-w-7xl mx-auto pb-12">
        {/* Top Header */}
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 bg-white p-6 rounded-2xl border border-[#E8E5DD] shadow-xs">
          <div>
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-xl bg-[#C5A059]/15 border border-[#C5A059]/30 text-[#0B3D0B] flex items-center justify-center shadow-xs">
                <Scale className="w-6 h-6 text-[#8C6D2D]" />
              </div>
              <div>
                <h1 className="text-2xl font-serif-luxury font-bold text-[#151A14] tracking-tight flex items-center gap-2">
                  Daily Gold Rates
                  <span className="text-[10.5px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-800 border border-emerald-200 uppercase tracking-wider">
                    Authoritative Spot
                  </span>
                </h1>
                <p className="text-xs text-stone-500 font-medium mt-0.5">
                  Live market spot pricing, store pricing policy, and authoritative customer rate overrides
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Branch Scope Selector */}
            <div className="flex items-center bg-[#FAF9F6] border border-[#E8E5DD] rounded-xl px-3 py-2">
              <Building2 className="w-4 h-4 text-[#C5A059] mr-2" />
              <select
                aria-label="Target Branch Scope"
                value={selectedBranchId}
                onChange={(e) => setSelectedBranchId(e.target.value)}
                className="bg-transparent text-xs font-semibold text-[#151A14] focus:outline-hidden cursor-pointer pr-2"
              >
                <option value="">Global (All Branches)</option>
                {overview?.branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name} ({b.code})
                  </option>
                ))}
              </select>
            </div>

            <button
              onClick={() => loadData(selectedBranchId)}
              disabled={loading}
              className="flex items-center gap-2 px-3.5 py-2.5 text-xs font-bold text-stone-700 bg-white hover:bg-stone-50 border border-[#E8E5DD] rounded-xl transition-all shadow-2xs disabled:opacity-50 cursor-pointer"
            >
              <RefreshCw className={`w-3.5 h-3.5 text-[#0B3D0B] ${loading ? "animate-spin" : ""}`} />
              <span>Refresh</span>
            </button>
          </div>
        </div>

        {/* Alerts */}
        {error && (
          <div className="flex items-start gap-3 p-4 rounded-xl bg-rose-50 border border-rose-200 text-rose-800 text-xs animate-fadeIn">
            <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5 text-rose-600" />
            <div className="flex-1 font-medium">{error}</div>
            <button onClick={() => setError(null)} className="text-xs text-rose-600 hover:underline">
              Dismiss
            </button>
          </div>
        )}

        {successMessage && (
          <div className="flex items-start gap-3 p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-900 text-xs animate-fadeIn">
            <CheckCircle2 className="w-5 h-5 flex-shrink-0 mt-0.5 text-emerald-700" />
            <div className="flex-1 font-semibold">{successMessage}</div>
            <button onClick={() => setSuccessMessage(null)} className="text-xs text-emerald-700 hover:underline">
              Dismiss
            </button>
          </div>
        )}

        {/* 4 Top KPI Metrics */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* 1. Effective 24K Rate */}
          <div className="p-5 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs flex flex-col justify-between hover:border-[#D8D3C5] hover:shadow-md transition-all">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-stone-500">
                Active Store Rate (24K)
              </span>
              <span
                className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${
                  overview?.effectiveRate.isOverridden
                    ? "bg-[#D4AF37]/15 text-[#8C6D2D] border-[#D4AF37]/35"
                    : "bg-emerald-50 text-emerald-800 border-emerald-200"
                }`}
              >
                {overview?.effectiveRate.isOverridden ? "MANUAL OVERRIDE" : "LIVE FEED"}
              </span>
            </div>
            <div className="my-1.5">
              <span className="text-2xl sm:text-3xl font-extrabold text-[#0B3D0B] tracking-tight font-sans tabular-nums block">
                LKR {formatLkr(overview?.effectiveRate.rate24k)}
              </span>
              <span className="text-[11px] text-stone-500 font-medium">Per Gram Pure Bullion</span>
            </div>
            <div className="mt-2 pt-2 border-t border-stone-100 flex items-center justify-between text-[11px] text-stone-500">
              <span>Per Sovereign (8g):</span>
              <span className="font-bold text-[#151A14] tabular-nums">
                LKR {formatLkr((overview?.effectiveRate.rate24k || 0) * 8)}
              </span>
            </div>
          </div>

          {/* 2. Effective 22K Rate */}
          <div className="p-5 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs flex flex-col justify-between hover:border-[#D8D3C5] hover:shadow-md transition-all">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-stone-500">
                Active Store Rate (22K)
              </span>
              <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-[#FAF9F6] text-stone-700 border border-[#E8E5DD]">
                Jewellery Grade
              </span>
            </div>
            <div className="my-1.5">
              <span className="text-2xl sm:text-3xl font-extrabold text-[#0B3D0B] tracking-tight font-sans tabular-nums block">
                LKR {formatLkr(overview?.effectiveRate.rate22k)}
              </span>
              <span className="text-[11px] text-stone-500 font-medium">Per Gram 91.67% Gold</span>
            </div>
            <div className="mt-2 pt-2 border-t border-stone-100 flex items-center justify-between text-[11px] text-stone-500">
              <span>Per Sovereign (8g):</span>
              <span className="font-bold text-[#151A14] tabular-nums">
                LKR {formatLkr((overview?.effectiveRate.rate22k || 0) * 8)}
              </span>
            </div>
          </div>

          {/* 3. Live API Spot Market */}
          <div className="p-5 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs flex flex-col justify-between hover:border-[#D8D3C5] hover:shadow-md transition-all">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-stone-500 flex items-center gap-1.5">
                <Radio className="w-3.5 h-3.5 text-emerald-600 animate-pulse" />
                Live Spot Feed
              </span>
              <span
                className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${
                  overview?.liveApiRate.isAvailable
                    ? "bg-emerald-50 text-emerald-800 border-emerald-200"
                    : "bg-stone-100 text-stone-600 border-stone-200"
                }`}
              >
                {overview?.liveApiRate.isAvailable ? "CONNECTED" : "FALLBACK"}
              </span>
            </div>
            <div className="my-1.5">
              <span className="text-xl sm:text-2xl font-bold text-[#151A14] tracking-tight font-sans tabular-nums block">
                {overview?.liveApiRate.rate24k
                  ? `LKR ${formatLkr(overview.liveApiRate.rate24k)}`
                  : "No Key Set"}
              </span>
              <span className="text-[11px] text-stone-500 font-medium truncate block">
                {overview?.liveApiRate.statusMessage || "GoldAPI.io feed"}
              </span>
            </div>
            <div className="mt-2 pt-2 border-t border-stone-100 text-[11px] text-stone-400">
              Real-time gold spot exchange
            </div>
          </div>

          {/* 4. Pricing Policy Status */}
          <div className="p-5 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs flex flex-col justify-between hover:border-[#D8D3C5] hover:shadow-md transition-all">
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-bold uppercase tracking-[0.14em] text-stone-500">
                System Guardrail
              </span>
              <ShieldCheck className="w-4 h-4 text-[#0B3D0B]" />
            </div>
            <div className="my-1.5">
              <span className="text-sm font-bold text-[#151A14] block">
                {overview?.effectiveRate.isOverridden
                  ? "Store Override Active"
                  : "Live Market Feed Active"}
              </span>
              <span className="text-[11px] text-stone-500 font-medium block mt-0.5">
                5-min checkout rate guarantee
              </span>
            </div>
            <div className="mt-2 pt-2 border-t border-stone-100 text-[11px] text-stone-400">
              Customer payments locked on quote
            </div>
          </div>
        </div>

        {/* Main Grid: Control Panel + Branch Table */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column: Interactive Override Control Panel (7 cols) */}
          <div className="lg:col-span-7 space-y-6">
            <div className="p-6 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs space-y-5">
              <div className="flex items-center justify-between border-b border-[#E8E5DD] pb-4">
                <div className="flex items-center gap-3">
                  <div className="p-2.5 rounded-xl bg-[#C5A059]/12 text-[#0B3D0B] border border-[#C5A059]/25">
                    <SlidersHorizontal className="w-5 h-5 text-[#8C6D2D]" />
                  </div>
                  <div>
                    <h2 className="text-base font-serif-luxury font-bold text-[#151A14]">Store Gold Rate Control Panel</h2>
                    <p className="text-xs text-stone-500 font-medium">
                      Publish authoritative rates that directly govern customer quotes and jewellery plans
                    </p>
                  </div>
                </div>

                {/* Quick Toggle Switch */}
                <div className="flex items-center gap-2">
                  <span className="text-xs text-stone-600 font-semibold">Override Mode:</span>
                  <button
                    type="button"
                    onClick={() => {
                      const next = !isOverrideEnabled;
                      setIsOverrideEnabled(next);
                      handleToggleOnly(next);
                    }}
                    disabled={saving}
                    className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-hidden ${
                      isOverrideEnabled ? "bg-[#0B3D0B]" : "bg-stone-200"
                    }`}
                  >
                    <span
                      aria-hidden="true"
                      className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow-xs ring-0 transition duration-200 ease-in-out ${
                        isOverrideEnabled ? "translate-x-5" : "translate-x-0"
                      }`}
                    />
                  </button>
                </div>
              </div>

              <form onSubmit={handleSaveOverride} className="space-y-4">
                {/* Notice banner */}
                <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200/80 flex items-start gap-2.5 text-xs text-amber-900">
                  <Info className="w-4 h-4 text-amber-700 flex-shrink-0 mt-0.5" />
                  <div>
                    When <strong>Manual Override Mode</strong> is active, all mobile customer quotes,
                    jewellery investment calculations, and payment conversions strictly use these
                    defined store rates instead of external market spot feeds.
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {/* 24K Input */}
                  <div>
                    <label className="block text-xs font-bold text-stone-700 mb-1.5">
                      24K Pure Gold Rate (LKR / gram) <span className="text-rose-600">*</span>
                    </label>
                    <div className="relative">
                      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-xs font-bold text-stone-500">
                        LKR
                      </span>
                      <input
                        type="number"
                        step="0.01"
                        min="1"
                        required
                        value={rate24k}
                        onChange={(e) => setRate24k(e.target.value)}
                        placeholder="e.g. 33600.00"
                        className="w-full bg-[#FAF9F6] border border-[#E8E5DD] rounded-xl pl-12 pr-3 py-2.5 text-xs sm:text-sm text-[#151A14] font-bold focus:outline-hidden focus:ring-2 focus:ring-[#0B3D0B] transition-all"
                      />
                    </div>
                    {/* Quick increment buttons */}
                    <div className="flex items-center gap-1.5 mt-2">
                      {[-500, -100, 100, 500].map((delta) => (
                        <button
                          key={delta}
                          type="button"
                          onClick={() => {
                            const val = (parseFloat(rate24k) || 33500) + delta;
                            setRate24k(val.toString());
                          }}
                          className="px-2 py-1 text-[10px] font-semibold rounded-lg bg-[#FAF9F6] hover:bg-stone-200/60 text-stone-700 border border-[#E8E5DD] transition-colors cursor-pointer"
                        >
                          {delta > 0 ? `+${delta}` : delta}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* 22K Input */}
                  <div>
                    <div className="flex items-center justify-between mb-1.5">
                      <label className="block text-xs font-bold text-stone-700">
                        22K Sovereign Gold Rate (LKR / g) <span className="text-rose-600">*</span>
                      </label>
                      <button
                        type="button"
                        onClick={handleAutoCalc22k}
                        className="text-[11px] font-bold text-[#8C6D2D] hover:underline flex items-center gap-1 cursor-pointer"
                      >
                        <Sparkles className="w-3 h-3 text-[#D4AF37]" />
                        Auto 91.67%
                      </button>
                    </div>
                    <div className="relative">
                      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-xs font-bold text-stone-500">
                        LKR
                      </span>
                      <input
                        type="number"
                        step="0.01"
                        min="1"
                        required
                        value={rate22k}
                        onChange={(e) => setRate22k(e.target.value)}
                        placeholder="e.g. 30800.00"
                        className="w-full bg-[#FAF9F6] border border-[#E8E5DD] rounded-xl pl-12 pr-3 py-2.5 text-xs sm:text-sm text-[#151A14] font-bold focus:outline-hidden focus:ring-2 focus:ring-[#0B3D0B] transition-all"
                      />
                    </div>
                    <p className="text-[10px] text-stone-400 mt-1 font-medium">
                      Typically 22/24ths of the pure 24K gold bullion rate.
                    </p>
                  </div>
                </div>

                {/* Target Scope */}
                <div>
                  <label className="block text-xs font-bold text-stone-700 mb-1.5">
                    Target Branch Scope
                  </label>
                  <select
                    value={selectedBranchId}
                    onChange={(e) => setSelectedBranchId(e.target.value)}
                    className="w-full bg-[#FAF9F6] border border-[#E8E5DD] rounded-xl px-3 py-2.5 text-xs text-[#151A14] font-semibold focus:outline-hidden focus:ring-2 focus:ring-[#0B3D0B] cursor-pointer"
                  >
                    <option value="">
                      Global — Applies across all store branches without custom overrides
                    </option>
                    {overview?.branches.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name} ({b.city}) — Branch-Specific Override
                      </option>
                    ))}
                  </select>
                </div>

                {/* Reason / Remarks */}
                <div>
                  <label className="block text-xs font-bold text-stone-700 mb-1.5">
                    Audit Remarks / Justification
                  </label>
                  <input
                    type="text"
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="e.g., Morning store rate approved by Head Office"
                    className="w-full bg-[#FAF9F6] border border-[#E8E5DD] rounded-xl px-3 py-2.5 text-xs text-[#151A14] focus:outline-hidden focus:ring-2 focus:ring-[#0B3D0B]"
                  />
                </div>

                {/* Form Buttons */}
                <div className="pt-2 flex flex-col sm:flex-row items-center gap-3">
                  <button
                    type="submit"
                    disabled={saving}
                    className="w-full sm:w-auto flex-1 flex items-center justify-center gap-2 px-5 py-3 rounded-xl btn-primary-green text-xs font-bold tracking-wide shadow-sm transition-all disabled:opacity-50 cursor-pointer"
                  >
                    {saving ? (
                      <>
                        <RefreshCw className="w-4 h-4 animate-spin text-[#D4AF37]" />
                        <span>Saving & Applying Rate...</span>
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="w-4 h-4 text-[#D4AF37]" />
                        <span>Save & Enforce Store Rate</span>
                      </>
                    )}
                  </button>

                  {isOverrideEnabled && (
                    <button
                      type="button"
                      onClick={() => handleToggleOnly(false)}
                      disabled={saving}
                      className="w-full sm:w-auto px-4 py-3 rounded-xl bg-white hover:bg-stone-50 text-stone-700 text-xs font-bold border border-[#E8E5DD] transition-colors cursor-pointer"
                    >
                      Revert to Live API
                    </button>
                  )}
                </div>
              </form>
            </div>
          </div>

          {/* Right Column: Branch Summary & Info (5 cols) */}
          <div className="lg:col-span-5 space-y-6">
            {/* Branch Rates Card */}
            <div className="p-5 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs space-y-4">
              <div className="flex items-center justify-between border-b border-[#E8E5DD] pb-3">
                <div className="flex items-center gap-2 text-[#151A14] font-serif-luxury font-bold text-sm">
                  <Building2 className="w-4 h-4 text-[#C5A059]" />
                  <span>Branch Rate Status</span>
                </div>
                <span className="text-[10px] font-semibold text-stone-500">
                  {overview?.branches.length || 0} active branches
                </span>
              </div>

              <div className="space-y-2.5">
                {overview?.branches.map((b) => (
                  <div
                    key={b.id}
                    className="p-3 rounded-xl bg-[#FAF9F6] border border-[#E8E5DD] hover:border-[#D8D3C5] transition-all flex items-center justify-between"
                  >
                    <div>
                      <div className="font-bold text-xs text-[#151A14] flex items-center gap-1.5">
                        {b.name}
                        <span className="text-[9px] font-mono px-1.5 py-0.2 rounded bg-white text-stone-700 border border-stone-200">
                          {b.code}
                        </span>
                      </div>
                      <div className="text-[11px] text-stone-500 mt-0.5">
                        {b.city} • {b.hasCustomOverride ? "Branch Override" : "Inherits Global"}
                      </div>
                    </div>

                    <div className="text-right">
                      <div className="text-xs sm:text-sm font-extrabold text-[#0B3D0B] tabular-nums">
                        LKR {formatLkr(b.rate24k)}
                      </div>
                      <div className="text-[10px] text-stone-500 tabular-nums">
                        22K: LKR {formatLkr(b.rate22k)}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* How Gold Rate Pricing Works info box */}
            <div className="p-5 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs space-y-3">
              <h3 className="text-xs font-bold text-[#151A14] flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-[#0B3D0B]" />
                Financial Precedence & Safety Guardrails
              </h3>
              <ul className="text-xs text-stone-600 space-y-2 list-disc list-inside leading-relaxed">
                <li>
                  <strong className="text-[#151A14]">Store Manual Override</strong> takes top priority.
                  It prevents quoting volatile spikes to walk-in and mobile customers.
                </li>
                <li>
                  Customer quotes lock the active 24K rate for <strong>5 minutes</strong> to guarantee
                  payment integrity during checkout.
                </li>
                <li>
                  Instalment cash payments convert directly into pure gold grams:
                  <div className="mt-1.5 p-2 rounded-lg bg-[#FAF9F6] border border-[#E8E5DD] font-mono text-[11px] text-[#0B3D0B] font-bold">
                    Grams = Amount (LKR) ÷ Rate (LKR/g)
                  </div>
                </li>
              </ul>
            </div>
          </div>
        </div>

        {/* Historical Audit Trail */}
        <div className="p-6 rounded-2xl bg-white border border-[#E8E5DD] shadow-xs space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-[#E8E5DD] pb-4">
            <div>
              <h3 className="text-base font-serif-luxury font-bold text-[#151A14] flex items-center gap-2">
                <Clock className="w-4 h-4 text-[#C5A059]" />
                Gold Rate Audit Trail
              </h3>
              <p className="text-xs text-stone-500 font-medium">
                Audited historical log of all rate recordings, overrides, and staff approvals
              </p>
            </div>
            <div className="text-xs text-stone-500 flex items-center gap-1.5 font-medium">
              <Calendar className="w-3.5 h-3.5 text-[#C5A059]" />
              Showing last {history.length} records
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs text-stone-800">
              <thead className="bg-[#FAF9F6] text-stone-500 uppercase tracking-[0.14em] font-bold text-[10.5px] border-b border-[#E8E5DD]">
                <tr>
                  <th className="py-3.5 px-4">Effective Date (SL Time)</th>
                  <th className="py-3.5 px-4">Branch Scope</th>
                  <th className="py-3.5 px-4">Karat</th>
                  <th className="py-3.5 px-4">Rate per Gram</th>
                  <th className="py-3.5 px-4">Per Sovereign (8g)</th>
                  <th className="py-3.5 px-4">Source</th>
                  <th className="py-3.5 px-4">Recorded By</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-stone-100">
                {history.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-8 text-center text-stone-400">
                      No gold rate audit records found.
                    </td>
                  </tr>
                ) : (
                  history.map((item) => (
                    <tr key={item.id} className="hover:bg-[#FAF9F6] transition-colors">
                      <td className="py-3.5 px-4 font-mono text-stone-600">
                        {formatDate(item.effectiveFromUtc)}
                      </td>
                      <td className="py-3.5 px-4 font-semibold text-[#151A14]">
                        {item.branchName || "Global (All Branches)"}
                      </td>
                      <td className="py-3.5 px-4">
                        <span
                          className={`px-2 py-0.5 rounded-full text-[10px] font-bold border ${
                            item.karat === 24
                              ? "bg-[#D4AF37]/15 text-[#8C6D2D] border-[#D4AF37]/35"
                              : "bg-stone-100 text-stone-700 border-stone-200"
                          }`}
                        >
                          {item.karat}K
                        </span>
                      </td>
                      <td className="py-3.5 px-4 font-bold text-[#0B3D0B] tabular-nums">
                        LKR {formatLkr(item.ratePerGram)}
                      </td>
                      <td className="py-3.5 px-4 text-stone-600 tabular-nums">
                        LKR {formatLkr(item.ratePerGram * 8)}
                      </td>
                      <td className="py-3.5 px-4">
                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-800 border border-emerald-200">
                          {item.source}
                        </span>
                      </td>
                      <td className="py-3.5 px-4 text-stone-600 font-medium">
                        {item.recordedByStaffName || "System Admin"}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
}
