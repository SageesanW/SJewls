"use client";

import React, { useState } from "react";
import Link from "next/link";
import { adminApi } from "../../lib/api";
import { SJewlsBrand } from "../../components/SJewlsBrand";
import { Mail, ArrowLeft, CheckCircle2, AlertCircle } from "lucide-react";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!email.trim() || !email.includes("@")) {
      setError("Please enter a valid email address.");
      return;
    }

    setIsSubmitting(true);
    try {
      await adminApi.forgotPassword(email.trim());
      setSubmitted(true);
    } catch (err: any) {
      setSubmitted(true);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 bg-[#FAF9F6] relative overflow-hidden">
      <div className="absolute top-1/4 -right-32 w-96 h-96 bg-[#0B3D0B]/5 rounded-full blur-3xl pointer-events-none" />

      <div className="w-full max-w-md relative z-10">
        <div className="flex flex-col items-center text-center mb-8">
          <SJewlsBrand collapsed={false} theme="light" size="lg" className="mb-3" />
          <h1 className="text-xl font-bold text-gray-900 tracking-tight">
            Forgot Staff Password
          </h1>
          <p className="text-xs text-gray-500 mt-1">
            Request secure password reset link for your staff account
          </p>
        </div>

        <div className="bg-white rounded-2xl p-8 shadow-md border border-gray-200">
          {submitted ? (
            <div className="space-y-6">
              <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 flex items-start gap-3 text-emerald-800 text-xs">
                <CheckCircle2 className="w-5 h-5 shrink-0 mt-0.5 text-emerald-600" />
                <div className="space-y-1">
                  <p className="font-bold text-emerald-900">
                    Instructions Sent
                  </p>
                  <p>
                    If an active account exists for <span className="font-semibold">{email}</span>, a secure password reset link has been dispatched.
                  </p>
                </div>
              </div>

              <Link
                href="/login"
                className="w-full py-2.5 px-4 rounded-xl btn-primary-green text-xs font-bold flex items-center justify-center gap-2 cursor-pointer shadow-sm"
              >
                <ArrowLeft className="w-4 h-4" />
                <span>Return to Sign In</span>
              </Link>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-5">
              {error && (
                <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-800 text-xs">
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5 text-red-600" />
                  <span>{error}</span>
                </div>
              )}

              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-2">
                  Staff Email Address
                </label>
                <div className="relative">
                  <Mail className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="staff@sjewls.lk"
                    className="w-full pl-9 pr-4 py-2.5 rounded-xl bg-gray-50 border border-gray-300 text-gray-900 placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={isSubmitting}
                className="w-full py-2.5 px-4 rounded-xl btn-primary-green text-xs font-bold flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
              >
                {isSubmitting ? "Sending Link..." : "Send Reset Instructions"}
              </button>

              <div className="text-center pt-2">
                <Link
                  href="/login"
                  className="inline-flex items-center gap-1.5 text-xs text-[#0B3D0B] font-bold hover:underline"
                >
                  <ArrowLeft className="w-3.5 h-3.5" />
                  <span>Back to Sign In</span>
                </Link>
              </div>
            </form>
          )}
        </div>
      </div>
    </div>
  );
}
