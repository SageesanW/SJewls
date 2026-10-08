"use client";

import React, { useState, useEffect, Suspense } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { adminApi } from "../../lib/api";
import { SJewlsBrand } from "../../components/SJewlsBrand";
import { Lock, Eye, EyeOff, AlertCircle, CheckCircle2, ArrowLeft } from "lucide-react";

function ResetPasswordForm() {
  const router = useRouter();
  const searchParams = useSearchParams();

  const [email, setEmail] = useState("");
  const [token, setToken] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    const qEmail = searchParams.get("email");
    const qToken = searchParams.get("token");
    if (qEmail) setEmail(qEmail);
    if (qToken) setToken(qToken);
  }, [searchParams]);

  // Password policy checklist
  const hasMinLength = newPassword.length >= 8;
  const hasUpper = /[A-Z]/.test(newPassword);
  const hasLower = /[a-z]/.test(newPassword);
  const hasDigit = /[0-9]/.test(newPassword);
  const isMatching = newPassword === confirmPassword && confirmPassword.length > 0;
  const isPolicyMet = hasMinLength && hasUpper && hasLower && hasDigit && isMatching;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!email || !token) {
      setError("Password reset token or email is missing. Please use the link sent to your email.");
      return;
    }

    if (!isPolicyMet) {
      setError("Please ensure your new password satisfies all security requirements.");
      return;
    }

    setIsSubmitting(true);
    try {
      await adminApi.resetPassword({
        email: email.trim(),
        token: token.trim(),
        newPassword,
        confirmPassword,
      });

      router.push("/login?reset=success");
    } catch (err: any) {
      setError(err?.message || "Failed to reset password. Link may be invalid or expired.");
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
            Create New Staff Password
          </h1>
          <p className="text-xs text-gray-500 mt-1">
            Choose a secure password for your administrator account
          </p>
        </div>

        <div className="bg-white rounded-2xl p-8 shadow-md border border-gray-200">
          {error && (
            <div className="mb-6 p-3.5 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3 text-red-800 text-xs">
              <AlertCircle className="w-4 h-4 shrink-0 mt-0.5 text-red-600" />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* New Password */}
            <div>
              <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-2">
                New Password
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-gray-400">
                  <Lock className="w-4 h-4" />
                </div>
                <input
                  type={showPassword ? "text" : "password"}
                  required
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full pl-10 pr-10 py-2.5 rounded-xl bg-gray-50 border border-gray-300 text-gray-900 placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute inset-y-0 right-0 pr-3.5 flex items-center text-gray-400 hover:text-gray-600 cursor-pointer"
                >
                  {showPassword ? <EyeOff className="w-4 h-4" /> : <Eye className="w-4 h-4" />}
                </button>
              </div>
            </div>

            {/* Confirm Password */}
            <div>
              <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-2">
                Confirm Password
              </label>
              <div className="relative">
                <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-gray-400">
                  <Lock className="w-4 h-4" />
                </div>
                <input
                  type={showPassword ? "text" : "password"}
                  required
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="••••••••••••"
                  className="w-full pl-10 pr-4 py-2.5 rounded-xl bg-gray-50 border border-gray-300 text-gray-900 placeholder-gray-400 text-sm focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                />
              </div>
            </div>

            {/* Checklist */}
            <div className="p-3.5 rounded-xl bg-gray-50 border border-gray-200 space-y-1.5 text-xs">
              <span className="font-bold text-gray-700 block mb-1">
                Security Policy Checklist:
              </span>
              <div className="grid grid-cols-2 gap-1.5 text-[11px]">
                <span className={hasMinLength ? "text-emerald-700 font-semibold" : "text-gray-400"}>
                  ✓ 8+ Characters
                </span>
                <span className={hasUpper ? "text-emerald-700 font-semibold" : "text-gray-400"}>
                  ✓ Uppercase letter
                </span>
                <span className={hasLower ? "text-emerald-700 font-semibold" : "text-gray-400"}>
                  ✓ Lowercase letter
                </span>
                <span className={hasDigit ? "text-emerald-700 font-semibold" : "text-gray-400"}>
                  ✓ Digit (0-9)
                </span>
              </div>
            </div>

            <button
              type="submit"
              disabled={isSubmitting || !isPolicyMet}
              className="w-full py-2.5 px-4 rounded-xl btn-primary-green text-xs font-bold flex items-center justify-center gap-2 cursor-pointer disabled:opacity-50"
            >
              {isSubmitting ? "Updating..." : "Reset Password"}
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
        </div>
      </div>
    </div>
  );
}

export default function ResetPasswordPage() {
  return (
    <Suspense
      fallback={
        <div className="min-h-screen bg-[#FAF9F6] flex items-center justify-center">
          <div className="w-10 h-10 border-3 border-[#0B3D0B] border-t-[#FFD700] rounded-full animate-spin" />
        </div>
      }
    >
      <ResetPasswordForm />
    </Suspense>
  );
}
