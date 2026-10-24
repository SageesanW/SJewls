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

      setSuccessMessage(
        isOverrideEnabled
          ? `Store rate successfully updated and enforced! (24K: LKR ${r24.toLocaleString()} /g | 22K: LKR ${r22.toLocaleString()} /g)`
          : "Manual override disabled. Switched back to live market feeds."
      );

      setTimeout(() => setSuccessMessage(null), 6000);
    } catch (err: any) {
      setError(err?.message || "Failed to update gold rate override.");
    } finally {
      setSaving(false);
    }
  };

  const handleToggleOnly = async (enabled: boolean) => {
    try {
      setSaving(true);
      setError(null);
      setSuccessMessage(null);

      const updated = await adminApi.toggleGoldRateOverride({
        branchId: selectedBranchId ? selectedBranchId : null,
        isEnabled: enabled,
      });

      setOverview(updated);
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
    <AdminLayout>
      <div className="space-y-6 max-w-7xl mx-auto pb-12">
        {/* Top Header */}
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-white/10 pb-5">
          <div>
            <div className="flex items-center gap-3">
              <div className="p-2.5 rounded-xl bg-amber-500/10 border border-amber-500/20 text-[#FFD700]">
                <Scale className="w-6 h-6" />
              </div>
              <div>
                <h1 className="text-2xl font-bold text-white tracking-tight flex items-center gap-2">
                  Gold Rate Management
                  <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                    Active Authority
                  </span>
                </h1>
                <p className="text-xs text-emerald-200/70 mt-0.5">
                  Live market spot prices, store pricing policy, and authoritative customer rate overrides.
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Branch Scope Selector */}
            <div className="flex items-center bg-[#072407] border border-white/10 rounded-xl px-3 py-1.5">
              <Building2 className="w-4 h-4 text-emerald-400 mr-2" />
              <select
                aria-label="Target Branch Scope"
                value={selectedBranchId}
                onChange={(e) => setSelectedBranchId(e.target.value)}
                className="bg-transparent text-xs text-white focus:outline-none cursor-pointer pr-2"
              >
                <option value="" className="bg-[#0A260A] text-white">
                  Global (All Branches)
                </option>
                {overview?.branches.map((b) => (
                  <option key={b.id} value={b.id} className="bg-[#0A260A] text-white">
                    {b.name} ({b.code})
                  </option>
                ))}
              </select>
            </div>

            <button
              onClick={() => loadData(selectedBranchId)}
              disabled={loading}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-medium text-emerald-100 bg-white/5 hover:bg-white/10 border border-white/10 rounded-xl transition-all disabled:opacity-50"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${loading ? "animate-spin" : ""}`} />
              Refresh
            </button>
          </div>
        </div>

        {/* Alerts */}
        {error && (
          <div className="flex items-start gap-3 p-4 rounded-xl bg-red-500/10 border border-red-500/30 text-red-300 text-sm animate-in fade-in">
            <AlertCircle className="w-5 h-5 flex-shrink-0 mt-0.5 text-red-400" />
            <div className="flex-1">{error}</div>
            <button onClick={() => setError(null)} className="text-xs text-red-400 hover:underline">
              Dismiss
            </button>
          </div>
        )}

        {successMessage && (
          <div className="flex items-start gap-3 p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-emerald-200 text-sm animate-in fade-in">
            <CheckCircle2 className="w-5 h-5 flex-shrink-0 mt-0.5 text-emerald-400" />
            <div className="flex-1 font-medium">{successMessage}</div>
            <button onClick={() => setSuccessMessage(null)} className="text-xs text-emerald-400 hover:underline">
              Dismiss
            </button>
          </div>
        )}

        {/* 4 Top KPI Metrics */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* 1. Effective 24K Rate */}
          <div className="relative overflow-hidden p-5 rounded-2xl bg-gradient-to-br from-[#0B3D0B] to-[#072407] border border-amber-500/30 shadow-lg">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-amber-300/80">
                Active Store Rate (24K)
              </span>
              <span
                className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${
                  overview?.effectiveRate.isOverridden
                    ? "bg-amber-500/20 text-amber-300 border-amber-500/40"
                    : "bg-emerald-500/20 text-emerald-300 border-emerald-500/40"
                }`}
              >
                {overview?.effectiveRate.isOverridden ? "MANUAL OVERRIDE" : "LIVE API"}
              </span>
            </div>
            <div className="text-2xl lg:text-3xl font-extrabold text-[#FFD700] tracking-tight">
              LKR {formatLkr(overview?.effectiveRate.rate24k)}
              <span className="text-xs font-normal text-emerald-300/70 ml-1">/ gram</span>
            </div>
            <div className="mt-2 pt-2 border-t border-white/10 flex items-center justify-between text-[11px] text-emerald-200/60">
              <span>Per Sovereign (8g):</span>
              <span className="font-semibold text-white">
                LKR {formatLkr((overview?.effectiveRate.rate24k || 0) * 8)}
              </span>
            </div>
          </div>

          {/* 2. Effective 22K Rate */}
          <div className="relative overflow-hidden p-5 rounded-2xl bg-gradient-to-br from-[#0B3D0B] to-[#072407] border border-white/10 shadow-lg">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-emerald-300/80">
                Active Store Rate (22K)
              </span>
              <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-white/10 text-emerald-200 border border-white/10">
                Jewellery Sovereign
              </span>
            </div>
            <div className="text-2xl lg:text-3xl font-extrabold text-white tracking-tight">
              LKR {formatLkr(overview?.effectiveRate.rate22k)}
              <span className="text-xs font-normal text-emerald-300/70 ml-1">/ gram</span>
            </div>
            <div className="mt-2 pt-2 border-t border-white/10 flex items-center justify-between text-[11px] text-emerald-200/60">
              <span>Per Sovereign (8g):</span>
              <span className="font-semibold text-white">
                LKR {formatLkr((overview?.effectiveRate.rate22k || 0) * 8)}
              </span>
            </div>
          </div>

          {/* 3. Live API Spot Market */}
          <div className="relative overflow-hidden p-5 rounded-2xl bg-[#0A260A] border border-white/10 shadow-lg">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-emerald-300/80 flex items-center gap-1.5">
                <Radio className="w-3.5 h-3.5 text-emerald-400 animate-pulse" />
                Live Spot Feed
              </span>
              <span
                className={`text-[10px] font-semibold px-2 py-0.5 rounded-full border ${
                  overview?.liveApiRate.isAvailable
                    ? "bg-emerald-500/20 text-emerald-300 border-emerald-500/30"
                    : "bg-white/10 text-emerald-300/60 border-white/10"
                }`}
              >
                {overview?.liveApiRate.isAvailable ? "CONNECTED" : "FALLBACK"}
              </span>
            </div>
            <div className="text-xl font-bold text-white">
              {overview?.liveApiRate.rate24k
                ? `LKR ${formatLkr(overview.liveApiRate.rate24k)}`
                : "No API Key"}
            </div>
            <div className="mt-2 pt-2 border-t border-white/10 text-[11px] text-emerald-200/60 truncate">
              {overview?.liveApiRate.statusMessage || "GoldAPI.io feed provider"}
            </div>
          </div>

          {/* 4. Pricing Policy Status */}
          <div className="relative overflow-hidden p-5 rounded-2xl bg-[#0A260A] border border-white/10 shadow-lg">
            <div className="flex items-center justify-between mb-2">
              <span className="text-xs font-semibold uppercase tracking-wider text-emerald-300/80">
                System Guardrail
              </span>
              <ShieldCheck className="w-4 h-4 text-emerald-400" />
            </div>
            <div className="text-sm font-semibold text-white">
              {overview?.effectiveRate.isOverridden
                ? "Store Override Enforced"
                : "Live Market Feed Active"}
            </div>
            <div className="mt-2 pt-2 border-t border-white/10 text-[11px] text-emerald-200/60">
              Quotes & jewellery payments lock this rate for 5 minutes.
            </div>
          </div>
        </div>

        {/* Main Grid: Control Panel + Branch Table */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column: Interactive Override Control Panel (7 cols) */}
          <div className="lg:col-span-7 space-y-6">
            <div className="p-6 rounded-2xl bg-[#0A260A] border border-amber-500/20 shadow-xl space-y-5">
              <div className="flex items-center justify-between border-b border-white/10 pb-4">
                <div className="flex items-center gap-3">
                  <div className="p-2 rounded-lg bg-amber-500/20 text-[#FFD700]">
                    <SlidersHorizontal className="w-5 h-5" />
                  </div>
                  <div>
                    <h2 className="text-base font-bold text-white">Store Gold Rate Control Panel</h2>
                    <p className="text-xs text-emerald-200/60">
                      Configure custom rates that directly govern customer quotes and jewellery plans.
                    </p>
                  </div>
                </div>

                {/* Quick Toggle Switch */}
                <div className="flex items-center gap-2">
                  <span className="text-xs text-emerald-200/80 font-medium">Override Mode:</span>
                  <button
                    type="button"
                    onClick={() => {
                      const next = !isOverrideEnabled;
                      setIsOverrideEnabled(next);
                      handleToggleOnly(next);
                    }}
                    disabled={saving}
                    className={`relative inline-flex h-6 w-11 flex-shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${
                      isOverrideEnabled ? "bg-amber-500" : "bg-white/20"
                    }`}
                  >
                    <span
                      aria-hidden="true"
                      className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${
                        isOverrideEnabled ? "translate-x-5" : "translate-x-0"
                      }`}
                    />
                  </button>
                </div>
              </div>

              <form onSubmit={handleSaveOverride} className="space-y-4">
                {/* Notice banner */}
                <div className="p-3.5 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-start gap-2.5 text-xs text-amber-200/90">
                  <Info className="w-4 h-4 text-[#FFD700] flex-shrink-0 mt-0.5" />
                  <div>
                    When <strong>Manual Override Mode</strong> is active, all mobile customer quotes,
                    jewellery investment calculations, and payment conversions will strictly use these
                    defined store rates instead of external market spot feeds.
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {/* 24K Input */}
                  <div>
                    <label className="block text-xs font-semibold text-emerald-200 mb-1.5">
                      24K Pure Gold Rate (LKR / gram) <span className="text-red-400">*</span>
                    </label>
                    <div className="relative">
                      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-xs font-bold text-emerald-400">
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
                        className="w-full bg-[#072407] border border-white/10 rounded-xl pl-12 pr-3 py-2.5 text-sm text-white font-semibold focus:outline-none focus:border-amber-400/60 focus:ring-1 focus:ring-amber-400/60 transition-all"
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
                          className="px-2 py-1 text-[10px] font-semibold rounded bg-white/5 hover:bg-white/10 text-emerald-200 border border-white/10 transition-colors"
                        >
                          {delta > 0 ? `+${delta}` : delta}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* 22K Input */}
                  <div>
                    <div className="flex items-center justify-between mb-1.5">
                      <label className="block text-xs font-semibold text-emerald-200">
                        22K Sovereign Gold Rate (LKR / g) <span className="text-red-400">*</span>
                      </label>
                      <button
                        type="button"
                        onClick={handleAutoCalc22k}
                        className="text-[11px] text-[#FFD700] hover:underline flex items-center gap-1"
                      >
                        <Sparkles className="w-3 h-3" />
                        Auto 91.67%
                      </button>
                    </div>
                    <div className="relative">
                      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-xs font-bold text-emerald-400">
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
                        className="w-full bg-[#072407] border border-white/10 rounded-xl pl-12 pr-3 py-2.5 text-sm text-white font-semibold focus:outline-none focus:border-amber-400/60 focus:ring-1 focus:ring-amber-400/60 transition-all"
                      />
                    </div>
                    <p className="text-[10px] text-emerald-300/50 mt-1">
                      Typically 22/24ths of the pure 24K gold bullion rate.
                    </p>
                  </div>
                </div>

                {/* Target Scope */}
                <div>
                  <label className="block text-xs font-semibold text-emerald-200 mb-1.5">
                    Target Branch Scope
                  </label>
                  <select
                    value={selectedBranchId}
                    onChange={(e) => setSelectedBranchId(e.target.value)}
                    className="w-full bg-[#072407] border border-white/10 rounded-xl px-3 py-2.5 text-xs text-white focus:outline-none focus:border-amber-400/60 cursor-pointer"
                  >
                    <option value="" className="bg-[#0A260A]">
                      Global — Applies across all store branches without custom overrides
                    </option>
                    {overview?.branches.map((b) => (
                      <option key={b.id} value={b.id} className="bg-[#0A260A]">
                        {b.name} ({b.city}) — Branch-Specific Override
                      </option>
                    ))}
                  </select>
                </div>

                {/* Reason / Remarks */}
                <div>
                  <label className="block text-xs font-semibold text-emerald-200 mb-1.5">
                    Audit Remarks / Justification
                  </label>
                  <input
                    type="text"
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder="e.g., Morning store rate approved by Head Office"
                    className="w-full bg-[#072407] border border-white/10 rounded-xl px-3 py-2.5 text-xs text-white focus:outline-none focus:border-amber-400/60"
                  />
                </div>

                {/* Form Buttons */}
                <div className="pt-2 flex flex-col sm:flex-row items-center gap-3">
                  <button
                    type="submit"
                    disabled={saving}
                    className="w-full sm:w-auto flex-1 flex items-center justify-center gap-2 px-5 py-3 rounded-xl bg-gradient-to-r from-amber-500 to-amber-600 hover:from-amber-400 hover:to-amber-500 text-black font-bold text-xs uppercase tracking-wider shadow-lg transition-all disabled:opacity-50"
                  >
                    {saving ? (
                      <>
                        <RefreshCw className="w-4 h-4 animate-spin" />
                        Saving & Applying Rate...
                      </>
                    ) : (
                      <>
                        <CheckCircle2 className="w-4 h-4" />
                        Save & Enforce Store Rate
                      </>
                    )}
                  </button>

                  {isOverrideEnabled && (
                    <button
                      type="button"
                      onClick={() => handleToggleOnly(false)}
                      disabled={saving}
                      className="w-full sm:w-auto px-4 py-3 rounded-xl bg-white/5 hover:bg-white/10 text-emerald-200 text-xs font-medium border border-white/10 transition-colors"
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
            <div className="p-5 rounded-2xl bg-[#0A260A] border border-white/10 shadow-xl space-y-4">
              <div className="flex items-center justify-between border-b border-white/10 pb-3">
                <div className="flex items-center gap-2 text-white font-bold text-sm">
                  <Building2 className="w-4 h-4 text-emerald-400" />
                  Branch Rate Status
                </div>
                <span className="text-[10px] text-emerald-300/60">
                  {overview?.branches.length || 0} active branches
                </span>
              </div>

              <div className="space-y-3">
                {overview?.branches.map((b) => (
                  <div
                    key={b.id}
                    className="p-3.5 rounded-xl bg-[#072407] border border-white/10 hover:border-amber-500/30 transition-all flex items-center justify-between"
                  >
                    <div>
                      <div className="font-semibold text-xs text-white flex items-center gap-1.5">
                        {b.name}
                        <span className="text-[9px] px-1.5 py-0.5 rounded bg-white/10 text-emerald-300">
                          {b.code}
                        </span>
                      </div>
                      <div className="text-[11px] text-emerald-300/60 mt-0.5">
                        {b.city} • {b.hasCustomOverride ? "Branch Override" : "Inherits Global"}
                      </div>
                    </div>

                    <div className="text-right">
                      <div className="text-sm font-bold text-[#FFD700]">
                        LKR {formatLkr(b.rate24k)}
                      </div>
                      <div className="text-[10px] text-emerald-300/60">
                        22K: LKR {formatLkr(b.rate22k)}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            {/* How Gold Rate Pricing Works info box */}
            <div className="p-5 rounded-2xl bg-gradient-to-br from-[#072407] to-[#0A260A] border border-white/10 shadow-xl space-y-3">
              <h3 className="text-xs font-bold text-white flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-emerald-400" />
                Precedence & Financial Protections
              </h3>
              <ul className="text-xs text-emerald-200/70 space-y-2 list-disc list-inside">
                <li>
                  <strong className="text-white">Store Manual Override</strong> takes top priority.
                  It prevents quoting volatile spikes to walk-in and mobile customers.
                </li>
                <li>
                  Customer quotes lock the active 24K rate for <strong>5 minutes</strong> to guarantee
                  payment integrity during checkout.
                </li>
                <li>
                  Instalment cash payments convert directly into pure gold grams:
                  <div className="mt-1 p-2 rounded-lg bg-black/40 font-mono text-[11px] text-amber-300">
                    Grams = Amount (LKR) ÷ Rate (LKR/g)
                  </div>
                </li>
              </ul>
            </div>
          </div>
        </div>

        {/* Historical Audit Trail */}
        <div className="p-6 rounded-2xl bg-[#0A260A] border border-white/10 shadow-xl space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-2 border-b border-white/10 pb-4">
            <div>
              <h3 className="text-base font-bold text-white flex items-center gap-2">
                <Clock className="w-4 h-4 text-emerald-400" />
                Gold Rate Audit Trail
              </h3>
              <p className="text-xs text-emerald-200/60">
                Audited historical log of all rate recordings, overrides, and staff approvals.
              </p>
            </div>
            <div className="text-xs text-emerald-300/70 flex items-center gap-1.5">
              <Calendar className="w-3.5 h-3.5" />
              Showing last {history.length} records
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs text-emerald-100">
              <thead className="bg-[#072407] text-emerald-300 uppercase tracking-wider font-semibold border-b border-white/10">
                <tr>
                  <th className="py-3 px-4">Effective Date (SL Time)</th>
                  <th className="py-3 px-4">Branch Scope</th>
                  <th className="py-3 px-4">Karat</th>
                  <th className="py-3 px-4">Rate per Gram</th>
                  <th className="py-3 px-4">Per Sovereign (8g)</th>
                  <th className="py-3 px-4">Source</th>
                  <th className="py-3 px-4">Recorded By</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {history.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-8 text-center text-emerald-300/50">
                      No gold rate audit records found.
                    </td>
                  </tr>
                ) : (
                  history.map((item) => (
                    <tr key={item.id} className="hover:bg-white/5 transition-colors">
                      <td className="py-3 px-4 font-mono text-emerald-200">
                        {formatDate(item.effectiveFromUtc)}
                      </td>
                      <td className="py-3 px-4 font-medium text-white">
                        {item.branchName || "Global (All Branches)"}
                      </td>
                      <td className="py-3 px-4">
                        <span
                          className={`px-2 py-0.5 rounded-full text-[10px] font-bold border ${
                            item.karat === 24
                              ? "bg-amber-500/20 text-[#FFD700] border-amber-500/30"
                              : "bg-white/10 text-emerald-200 border-white/10"
                          }`}
                        >
                          {item.karat}K
                        </span>
                      </td>
                      <td className="py-3 px-4 font-bold text-white">
                        LKR {formatLkr(item.ratePerGram)}
                      </td>
                      <td className="py-3 px-4 text-emerald-300/80">
                        LKR {formatLkr(item.ratePerGram * 8)}
                      </td>
                      <td className="py-3 px-4">
                        <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-300 border border-emerald-500/20">
                          {item.source}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-emerald-200/80">
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
