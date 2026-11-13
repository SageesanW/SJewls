"use client";

import React, { useState, useEffect } from "react";
import { useRouter, usePathname } from "next/navigation";
import { useAuth } from "../lib/auth-context";
import { Sidebar } from "./Sidebar";
import { SJewlsBrand } from "./SJewlsBrand";
import { Badge } from "./ui/badge";
import { StaffUser } from "../types/auth";
import { Menu, Building2, ShieldCheck, LogOut, PanelLeftClose, PanelLeftOpen, Sparkles } from "lucide-react";
import { cn } from "../lib/utils";

interface AdminLayoutProps {
  children: React.ReactNode;
  title?: string;
  previewUser?: StaffUser;
  primaryAction?: React.ReactNode;
}

export function AdminLayout({ children, title, previewUser, primaryAction }: AdminLayoutProps) {
  const router = useRouter();
  const pathname = usePathname();
  const { user: authUser, isLoading, logout } = useAuth();
  const user = previewUser || authUser;
  const isEffectiveLoading = previewUser ? false : isLoading;
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  // Initialize collapse preference from localStorage
  useEffect(() => {
    try {
      const stored = localStorage.getItem("sjewls_sidebar_collapsed");
      if (stored !== null) {
        setCollapsed(stored === "true");
      }
    } catch {
      // Ignore localStorage read errors in SSR/sandboxed mode
    }
  }, []);

  const handleToggleCollapse = () => {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem("sjewls_sidebar_collapsed", String(next));
      } catch {
        // Ignore
      }
      return next;
    });
  };

  useEffect(() => {
    if (!isEffectiveLoading && !user) {
      router.push("/login");
    }
  }, [isEffectiveLoading, user, router]);

  if (isEffectiveLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#FAF9F6]">
        <div className="flex flex-col items-center gap-3">
          <div className="w-10 h-10 border-3 border-[#0B3D0B]/20 border-t-[#C5A059] rounded-full animate-spin" />
          <span className="text-xs font-bold text-[#0B3D0B] tracking-[0.16em] uppercase">
            Loading SJewls Admin...
          </span>
        </div>
      </div>
    );
  }

  if (!user) {
    return null;
  }

  const primaryBranch = user?.assignedBranches?.[0];
  const userInitials = user?.fullName
    ? user.fullName
        .split(" ")
        .map((n) => n[0])
        .join("")
        .substring(0, 2)
        .toUpperCase()
    : "SJ";

  return (
    <div className="min-h-screen bg-[#FAF9F6] text-[#151A14] flex flex-col antialiased selection:bg-[#C5A059]/30 selection:text-[#0B3D0B]">
      {/* Adaptive Responsive Sidebar */}
      <Sidebar
        collapsed={collapsed}
        onToggleCollapse={handleToggleCollapse}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
        overrideUser={user || undefined}
      />

      {/* ================= MOBILE HEADER (Narrow Viewports) ================= */}
      <header className="md:hidden sticky top-0 z-30 flex items-center justify-between h-16 px-4 bg-gradient-to-r from-[#061E06] via-[#092809] to-[#061E06] text-white border-b border-[#C5A059]/25 shadow-md">
        <div className="flex items-center gap-3">
          <button
            onClick={() => setMobileOpen(true)}
            aria-label="Open navigation menu"
            className="p-2 rounded-lg text-emerald-100 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
          >
            <Menu className="w-5 h-5 text-[#D4AF37]" />
          </button>
          <SJewlsBrand collapsed={false} theme="dark" size="sm" />
        </div>

        <div className="flex items-center gap-2">
          {primaryBranch && (
            <span className="hidden xs:inline-flex items-center gap-1 text-[11px] font-semibold text-emerald-200 bg-white/10 px-2 py-0.5 rounded-md border border-white/10">
              <Building2 className="w-3 h-3 text-[#D4AF37]" />
              {primaryBranch.code}
            </span>
          )}
          <div
            className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#D4AF37] to-[#8C6D2D] text-[#061E06] font-bold text-xs flex items-center justify-center shadow-xs"
            title={user.fullName}
          >
            {userInitials}
          </div>
        </div>
      </header>

      {/* ================= MAIN CONTENT WRAPPER ================= */}
      <div
        className={cn(
          "flex-1 flex flex-col transition-all duration-300 ease-in-out",
          collapsed ? "md:pl-[72px]" : "md:pl-[260px]"
        )}
      >
        {/* ================= DESKTOP HEADER BAR ================= */}
        <header className="hidden md:flex sticky top-0 z-20 h-16 bg-[#FAF9F6]/90 backdrop-blur-md border-b border-[#E8E5DD] px-6 sm:px-8 items-center justify-between shadow-2xs">
          {/* Left: Sidebar toggle + Page Title / Branch Context */}
          <div className="flex items-center gap-4">
            <button
              onClick={handleToggleCollapse}
              aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
              className="p-1.5 rounded-lg text-stone-500 hover:text-[#0B3D0B] hover:bg-stone-200/60 transition-colors cursor-pointer focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#0B3D0B]"
              title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            >
              {collapsed ? (
                <PanelLeftOpen className="w-4 h-4" />
              ) : (
                <PanelLeftClose className="w-4 h-4" />
              )}
            </button>

            <div className="flex items-center gap-3">
              {title && (
                <h1 className="font-serif-luxury font-bold text-[#151A14] text-xl tracking-tight leading-none">
                  {title}
                </h1>
              )}

              {/* Operating Branch Pill - Prominently Displayed */}
              {primaryBranch ? (
                <div
                  className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white border border-[#E8E5DD] text-[#151A14] text-xs font-semibold shadow-2xs"
                  title={`Operating Branch: ${primaryBranch.name}`}
                >
                  <Building2 className="w-3.5 h-3.5 text-[#C5A059] flex-shrink-0" />
                  <span className="font-medium text-stone-700">{primaryBranch.name}</span>
                  <span className="font-mono text-[10px] text-[#0B3D0B] bg-[#ECFDF5] border border-[#A7F3D0] px-1.5 py-0.2 rounded font-bold">
                    {primaryBranch.code}
                  </span>
                </div>
              ) : (
                <div className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white border border-[#E8E5DD] text-stone-600 text-xs font-medium">
                  <Building2 className="w-3.5 h-3.5 text-stone-400" />
                  <span>All Branches</span>
                </div>
              )}
            </div>
          </div>

          {/* Right: Actions & Staff Profile */}
          <div className="flex items-center gap-3">
            {primaryAction && <div>{primaryAction}</div>}

            <div className="flex items-center gap-2.5 pl-3 border-l border-[#E8E5DD]">
              <div className="flex flex-col text-right">
                <span className="text-xs font-bold text-[#151A14] leading-tight">
                  {user.fullName}
                </span>
                <span className="text-[10px] text-[#8C6D2D] font-semibold leading-tight mt-0.5">
                  {user.roles?.[0] || "Staff"}
                </span>
              </div>

              <div
                className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#D4AF37] via-[#C5A059] to-[#8C6D2D] text-[#061E06] font-bold text-xs flex items-center justify-center shadow-xs border border-white"
                title={`${user.fullName} (${user.email})`}
              >
                {userInitials}
              </div>
            </div>

            <button
              onClick={() => logout()}
              title="Sign out"
              className="p-1.5 text-stone-400 hover:text-rose-600 hover:bg-rose-50 rounded-lg transition-colors cursor-pointer focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-rose-500"
              aria-label="Sign out"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </header>

        {/* Content Body */}
        <main className="flex-1 w-full max-w-[1600px] mx-auto p-4 sm:p-6 lg:p-8 animate-fadeIn">
          {children}
        </main>
      </div>
    </div>
  );
}
