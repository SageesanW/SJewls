"use client";

import React, { useState, useEffect, useCallback, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { AdminLayout } from "../../components/AdminLayout";
import { jewelleryEnrolmentsApi, ApiError } from "../../lib/api";
import { useAuth } from "../../lib/auth-context";
import {
  AdminReminderContext,
  AdminSendReminderResult,
} from "../../types/jewelleryPlan";
import {
  Bell,
  Mail,
  MessageSquare,
  Smartphone,
  Send,
  ArrowLeft,
  CheckCircle2,
  AlertCircle,
  Clock,
  User,
  Calendar,
  Sparkles,
  Info,
  ShieldCheck,
  RefreshCw,
} from "lucide-react";

function NotificationComposerContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuth();

  const enrolmentId = searchParams.get("enrolmentId");
  const queryCustomerId = searchParams.get("customerId");
  const queryReason = searchParams.get("reason");

  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [context, setContext] = useState<AdminReminderContext | null>(null);

  // Form states
  const [selectedChannels, setSelectedChannels] = useState<{
    inApp: boolean;
    email: boolean;
    sms: boolean;
    push: boolean;
  }>({
    inApp: true,
    email: false,
    sms: false,
    push: false,
  });

  const [title, setTitle] = useState<string>("");
  const [message, setMessage] = useState<string>("");

  // Submission states
  const [sending, setSending] = useState<boolean>(false);
  const [sendResult, setSendResult] = useState<AdminSendReminderResult | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);

  // Fetch verified context using stable enrolment ID
  const fetchContext = useCallback(async () => {
    if (!enrolmentId) {
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const data = await jewelleryEnrolmentsApi.getReminderContext(enrolmentId);
      setContext(data);

      // Pre-fill suggested title & message
      setTitle(data.suggestedTitle);
      setMessage(data.suggestedMessage);

      // Configure default channels based on availability
      setSelectedChannels({
        inApp: data.inApp.available,
        email: data.email.available,
        sms: data.sms.available,
        push: false, // Push is honest unsupported
      });
    } catch (err: any) {
      console.error("Failed to load reminder context:", err);
      setError(
        err?.message ||
          "Failed to retrieve authorised customer and enrolment reminder context."
      );
    } finally {
      setLoading(false);
    }
  }, [enrolmentId]);

  useEffect(() => {
    fetchContext();
  }, [fetchContext]);

  // Handle Channel Toggle
  const toggleChannel = (channel: "inApp" | "email" | "sms" | "push") => {
    if (channel === "push") return; // Push disabled
    if (channel === "email" && !context?.email.available) return;
    if (channel === "sms" && !context?.sms.available) return;

    setSelectedChannels((prev) => ({
      ...prev,
      [channel]: !prev[channel],
    }));
  };

  // Submit Reminder Notification
  const handleDispatch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!enrolmentId || !context) return;

    const channels: string[] = [];
    if (selectedChannels.inApp) channels.push("InApp");
    if (selectedChannels.email) channels.push("Email");
    if (selectedChannels.sms) channels.push("Sms");

    if (channels.length === 0) {
      setSendError("Please select at least one available delivery channel.");
      return;
    }

    if (!title.trim()) {
      setSendError("Please provide a notification title.");
      return;
    }

    if (!message.trim()) {
      setSendError("Please provide a notification message.");
      return;
    }

    setSending(true);
    setSendError(null);

    try {
      const result = await jewelleryEnrolmentsApi.sendReminder(enrolmentId, {
        channels,
        title: title.trim(),
        message: message.trim(),
      });

      setSendResult(result);
    } catch (err: any) {
      console.error("Failed to send reminder:", err);
      setSendError(err?.message || "Failed to dispatch reminder notification.");
    } finally {
      setSending(false);
    }
  };

  const formatDate = (dateOnlyStr: string | null | undefined) => {
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

  const formatDateTime = (isoUtc: string | null | undefined) => {
    if (!isoUtc) return "None (Never paid)";
    try {
      const d = new Date(isoUtc);
      return new Intl.DateTimeFormat("en-GB", {
        year: "numeric",
        month: "short",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
      }).format(d);
    } catch {
      return isoUtc;
    }
  };

  return (
    <AdminLayout title="Notification Composer">
      <div className="max-w-4xl mx-auto space-y-6 pb-20">
        {/* Navigation Breadcrumb */}
        <div className="flex items-center gap-3">
          <button
            onClick={() => router.push("/jewellery-plans/payments")}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl border border-[#E8E5DD] bg-white hover:bg-stone-50 text-xs font-semibold text-stone-700 transition-colors cursor-pointer"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            <span>Back to Payments</span>
          </button>
          <span className="text-xs text-stone-400">/</span>
          <span className="text-xs font-semibold text-stone-800">
            Reminder Notification Composer
          </span>
        </div>

        {/* Page Title */}
        <div className="bg-white p-6 rounded-2xl shadow-xs border border-[#E8E5DD] flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-xl bg-[#C5A059]/15 border border-[#C5A059]/30 text-[#0B3D0B] flex items-center justify-center shadow-xs">
              <Bell className="w-6 h-6 text-[#8C6D2D]" />
            </div>
            <div>
              <h1 className="text-2xl font-serif-luxury font-bold text-[#151A14] tracking-tight">
                Draft Reminder Notification
              </h1>
              <p className="text-xs sm:text-sm text-stone-500 font-medium mt-0.5">
                Staff-reviewed reminder dispatch with verified enrolment context and delivery channel selection
              </p>
            </div>
          </div>
        </div>

        {/* No Enrolment Selected State */}
        {!enrolmentId && (
          <div className="bg-white p-12 rounded-2xl border border-neutral-100 text-center space-y-4">
            <AlertCircle className="w-12 h-12 text-neutral-300 mx-auto" />
            <h2 className="text-lg font-bold text-neutral-800">No Enrolment Selected</h2>
            <p className="text-sm text-neutral-500 max-w-md mx-auto">
              Please navigate to Jewellery Plan Payments and click &quot;Send Reminder&quot; on an eligible payment or incomplete plan row to compose a notification with verified context.
            </p>
            <Link
              href="/jewellery-plans/payments"
              className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#0B3D0B] text-white text-sm font-semibold hover:bg-[#0B3D0B]/90 transition-colors"
            >
              Go to Jewellery Payments
            </Link>
          </div>
        )}

        {/* Loading State */}
        {loading && (
          <div className="bg-white p-12 rounded-2xl border border-neutral-100 flex flex-col items-center justify-center min-h-[300px] text-neutral-400">
            <RefreshCw className="w-8 h-8 animate-spin text-[#0B3D0B] mb-3" />
            <p className="text-sm font-medium">Fetching verified reminder context from server...</p>
          </div>
        )}

        {/* Error State */}
        {error && (
          <div className="p-6 rounded-2xl bg-red-50 border border-red-200 text-red-800 flex flex-col items-center justify-center text-center gap-3">
            <AlertCircle className="w-8 h-8 text-red-600" />
            <div>
              <h3 className="font-bold text-base">Failed to load reminder context</h3>
              <p className="text-sm text-red-600 mt-1">{error}</p>
            </div>
            <button
              onClick={fetchContext}
              className="px-4 py-2 bg-red-600 text-white font-semibold text-sm rounded-xl hover:bg-red-700 transition-colors"
            >
              Retry
            </button>
          </div>
        )}

        {/* Success Modal / Banner */}
        {sendResult && (
          <div className="p-8 bg-white rounded-2xl border border-emerald-200 shadow-md space-y-6">
            <div className="flex items-center gap-3 text-emerald-800">
              <CheckCircle2 className="w-8 h-8 text-emerald-600 flex-shrink-0" />
              <div>
                <h2 className="text-xl font-bold">Reminder Dispatched Successfully</h2>
                <p className="text-sm text-neutral-600 mt-0.5">
                  The reminder notification has been authoritatively processed and logged.
                </p>
              </div>
            </div>

            <div className="bg-neutral-50 p-4 rounded-xl space-y-2 text-xs">
              <div className="flex items-center justify-between">
                <span className="font-semibold text-neutral-600">Delivered Channels:</span>
                <span className="font-bold text-emerald-700">
                  {sendResult.deliveredChannels.join(", ") || "None"}
                </span>
              </div>
              {sendResult.failedChannels.length > 0 && (
                <div className="flex items-center justify-between text-amber-700">
                  <span className="font-semibold">Skipped / Failed Channels:</span>
                  <span>{sendResult.failedChannels.join(", ")}</span>
                </div>
              )}
              <div className="flex items-center justify-between text-neutral-500">
                <span>Dispatched At:</span>
                <span>{new Date(sendResult.sentAtUtc).toLocaleString()}</span>
              </div>
            </div>

            <div className="flex items-center gap-3 pt-2">
              <button
                onClick={() => router.push("/jewellery-plans/payments")}
                className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] text-white text-sm font-semibold hover:bg-[#0B3D0B]/90 transition-colors"
              >
                Return to Payments Ledger
              </button>
              {context && (
                <Link
                  href={`/customers/${context.customerId}`}
                  className="px-5 py-2.5 rounded-xl border border-neutral-300 text-neutral-700 text-sm font-semibold hover:bg-neutral-50 transition-colors"
                >
                  View Customer Profile
                </Link>
              )}
            </div>
          </div>
        )}

        {/* Main Composer Form */}
        {!loading && !error && context && !sendResult && (
          <form onSubmit={handleDispatch} className="space-y-6">
            {/* Context Summary Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Customer Profile Card */}
              <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm space-y-3">
                <div className="flex items-center justify-between text-xs font-bold text-neutral-500 uppercase tracking-wider">
                  <span className="flex items-center gap-1.5">
                    <User className="w-4 h-4 text-neutral-400" />
                    Recipient Profile
                  </span>
                  <span className="px-2 py-0.5 rounded-full text-[11px] bg-emerald-100 text-emerald-800 font-bold">
                    Active Account
                  </span>
                </div>

                <div>
                  <div className="text-base font-bold text-neutral-900">
                    {context.customerName}
                  </div>
                  <div className="text-xs text-neutral-500 mt-1 space-y-0.5">
                    <div>
                      Phone:{" "}
                      <span className="font-medium text-neutral-800">
                        {context.customerPhone || "No phone registered"}
                      </span>
                      {context.isPhoneVerified && (
                        <span className="ml-1.5 text-[10px] text-emerald-600 font-semibold">
                          (Verified)
                        </span>
                      )}
                    </div>
                    <div>
                      Email:{" "}
                      <span className="font-medium text-neutral-800">
                        {context.customerEmail || "No email registered"}
                      </span>
                      {context.isEmailVerified && (
                        <span className="ml-1.5 text-[10px] text-emerald-600 font-semibold">
                          (Verified)
                        </span>
                      )}
                    </div>
                  </div>
                </div>
              </div>

              {/* Enrolment Terms Card */}
              <div className="bg-white p-5 rounded-2xl border border-neutral-100 shadow-sm space-y-3">
                <div className="flex items-center justify-between text-xs font-bold text-neutral-500 uppercase tracking-wider">
                  <span className="flex items-center gap-1.5">
                    <Sparkles className="w-4 h-4 text-amber-500" />
                    Plan Context
                  </span>
                  <span className="px-2 py-0.5 rounded text-[11px] font-mono bg-neutral-100 text-neutral-700">
                    {context.enrolmentReference}
                  </span>
                </div>

                <div>
                  <div className="text-base font-bold text-neutral-900">
                    {context.planName}
                  </div>
                  <div className="grid grid-cols-2 gap-2 mt-2 text-xs">
                    <div className="bg-neutral-50 p-2 rounded-lg">
                      <span className="text-neutral-400 block text-[10px]">Target Grams:</span>
                      <span className="font-bold text-neutral-800">
                        {context.targetGrams.toFixed(4)} g
                      </span>
                    </div>
                    <div className="bg-neutral-50 p-2 rounded-lg">
                      <span className="text-neutral-400 block text-[10px]">Saved Grams:</span>
                      <span className="font-bold text-[#0B3D0B]">
                        {context.savedGrams.toFixed(4)} g
                      </span>
                    </div>
                    <div className="bg-neutral-50 p-2 rounded-lg">
                      <span className="text-neutral-400 block text-[10px]">Deadline:</span>
                      <span className="font-bold text-neutral-800">
                        {formatDate(context.deadline)}
                      </span>
                    </div>
                    <div className="bg-neutral-50 p-2 rounded-lg">
                      <span className="text-neutral-400 block text-[10px]">Last Payment:</span>
                      <span className="font-bold text-neutral-800">
                        {formatDateTime(context.lastSuccessfulPaymentAt)}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>

            {/* Reminder Reason Alert Banner */}
            <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 flex items-start gap-3">
              <Clock className="w-5 h-5 text-amber-700 flex-shrink-0 mt-0.5" />
              <div>
                <div className="text-sm font-bold text-amber-900">
                  Reminder Reason:{" "}
                  {context.reminderReason === "PlanIncomplete"
                    ? "Plan Duration Ended Below Target"
                    : "No Payment for 3+ Calendar Months"}
                </div>
                <p className="text-xs text-amber-800 mt-0.5">
                  {context.reminderReason === "PlanIncomplete"
                    ? "The enrolment target deadline has passed. The customer is guided to visit the branch under existing settlement options."
                    : "Three calendar months have elapsed since the customer's last confirmed payment (or plan joining date)."}
                </p>
              </div>
            </div>

            {/* Delivery Channel Selector */}
            <div className="bg-white p-6 rounded-2xl border border-neutral-100 shadow-sm space-y-4">
              <h2 className="text-base font-bold text-neutral-900 flex items-center gap-2">
                <Send className="w-4 h-4 text-[#0B3D0B]" />
                Select Delivery Channels
              </h2>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {/* In-App */}
                <label
                  onClick={() => toggleChannel("inApp")}
                  className={`p-4 rounded-xl border flex items-start gap-3 cursor-pointer transition-all ${
                    selectedChannels.inApp
                      ? "border-[#0B3D0B] bg-[#0B3D0B]/5 ring-1 ring-[#0B3D0B]"
                      : "border-neutral-200 hover:bg-neutral-50"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={selectedChannels.inApp}
                    onChange={() => {}}
                    className="mt-1 h-4 w-4 text-[#0B3D0B] rounded border-neutral-300 focus:ring-[#0B3D0B]"
                  />
                  <div>
                    <div className="flex items-center gap-1.5 font-bold text-sm text-neutral-900">
                      <Bell className="w-4 h-4 text-[#0B3D0B]" />
                      In-App Notification
                    </div>
                    <p className="text-xs text-neutral-500 mt-1">{context.inApp.description}</p>
                  </div>
                </label>

                {/* Email */}
                <label
                  onClick={() => toggleChannel("email")}
                  className={`p-4 rounded-xl border flex items-start gap-3 transition-all ${
                    !context.email.available
                      ? "opacity-50 cursor-not-allowed bg-neutral-50 border-neutral-200"
                      : selectedChannels.email
                      ? "border-[#0B3D0B] bg-[#0B3D0B]/5 ring-1 ring-[#0B3D0B] cursor-pointer"
                      : "border-neutral-200 hover:bg-neutral-50 cursor-pointer"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={selectedChannels.email}
                    disabled={!context.email.available}
                    onChange={() => {}}
                    className="mt-1 h-4 w-4 text-[#0B3D0B] rounded border-neutral-300 focus:ring-[#0B3D0B]"
                  />
                  <div>
                    <div className="flex items-center gap-1.5 font-bold text-sm text-neutral-900">
                      <Mail className="w-4 h-4 text-blue-600" />
                      Email Dispatch
                    </div>
                    <p className="text-xs text-neutral-500 mt-1">{context.email.description}</p>
                  </div>
                </label>

                {/* SMS */}
                <label
                  onClick={() => toggleChannel("sms")}
                  className={`p-4 rounded-xl border flex items-start gap-3 transition-all ${
                    !context.sms.available
                      ? "opacity-50 cursor-not-allowed bg-neutral-50 border-neutral-200"
                      : selectedChannels.sms
                      ? "border-[#0B3D0B] bg-[#0B3D0B]/5 ring-1 ring-[#0B3D0B] cursor-pointer"
                      : "border-neutral-200 hover:bg-neutral-50 cursor-pointer"
                  }`}
                >
                  <input
                    type="checkbox"
                    checked={selectedChannels.sms}
                    disabled={!context.sms.available}
                    onChange={() => {}}
                    className="mt-1 h-4 w-4 text-[#0B3D0B] rounded border-neutral-300 focus:ring-[#0B3D0B]"
                  />
                  <div>
                    <div className="flex items-center gap-1.5 font-bold text-sm text-neutral-900">
                      <MessageSquare className="w-4 h-4 text-emerald-600" />
                      SMS Message
                    </div>
                    <p className="text-xs text-neutral-500 mt-1">{context.sms.description}</p>
                  </div>
                </label>

                {/* Push Notification (Unavailable) */}
                <div className="p-4 rounded-xl border border-neutral-200 bg-neutral-50 opacity-60 flex items-start gap-3 cursor-not-allowed">
                  <input
                    type="checkbox"
                    checked={false}
                    disabled
                    className="mt-1 h-4 w-4 text-neutral-400 rounded border-neutral-300"
                  />
                  <div>
                    <div className="flex items-center gap-1.5 font-bold text-sm text-neutral-600">
                      <Smartphone className="w-4 h-4 text-neutral-400" />
                      Push Notification
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-neutral-200 text-neutral-700 font-semibold">
                        Unavailable
                      </span>
                    </div>
                    <p className="text-xs text-neutral-400 mt-1">{context.push.description}</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Message Composer Card */}
            <div className="bg-white p-6 rounded-2xl border border-neutral-100 shadow-sm space-y-4">
              <h2 className="text-base font-bold text-neutral-900">Message Content</h2>

              <div>
                <label className="block text-xs font-bold text-neutral-700 uppercase tracking-wider mb-1.5">
                  Subject / Title
                </label>
                <input
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  maxLength={200}
                  className="w-full px-4 py-2.5 text-sm border border-neutral-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  placeholder="Enter reminder title..."
                  required
                />
              </div>

              <div>
                <div className="flex items-center justify-between mb-1.5">
                  <label className="block text-xs font-bold text-neutral-700 uppercase tracking-wider">
                    Message Body
                  </label>
                  <span className="text-xs text-neutral-400">{message.length} / 1000 characters</span>
                </div>
                <textarea
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                  maxLength={1000}
                  rows={5}
                  className="w-full px-4 py-3 text-sm border border-neutral-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  placeholder="Draft message to customer..."
                  required
                />
              </div>
            </div>

            {/* Send Error Notice */}
            {sendError && (
              <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 flex items-center gap-2.5 text-sm">
                <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
                <span>{sendError}</span>
              </div>
            )}

            {/* Action Bar */}
            <div className="flex items-center justify-between pt-2">
              <button
                type="button"
                onClick={() => router.push("/jewellery-plans/payments")}
                disabled={sending}
                className="px-5 py-2.5 rounded-xl border border-neutral-300 text-neutral-700 text-sm font-semibold hover:bg-neutral-50 transition-colors disabled:opacity-50"
              >
                Cancel
              </button>

              <button
                type="submit"
                disabled={sending}
                className="inline-flex items-center gap-2 px-6 py-2.5 rounded-xl bg-[#0B3D0B] text-white text-sm font-bold hover:bg-[#0B3D0B]/90 transition-all shadow-sm hover:shadow disabled:opacity-50"
              >
                {sending ? (
                  <>
                    <RefreshCw className="w-4 h-4 animate-spin text-[#FFD700]" />
                    <span>Dispatching...</span>
                  </>
                ) : (
                  <>
                    <Send className="w-4 h-4 text-[#FFD700]" />
                    <span>Dispatch Reminder Notification</span>
                  </>
                )}
              </button>
            </div>
          </form>
        )}
      </div>
    </AdminLayout>
  );
}

export default function NotificationComposerPage() {
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
      <NotificationComposerContent />
    </Suspense>
  );
}
