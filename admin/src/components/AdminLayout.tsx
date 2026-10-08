"use client";

import React, { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../lib/auth-context";
import { Sidebar } from "./Sidebar";
import { SJewlsBrand } from "./SJewlsBrand";
import { Menu, Building2, ShieldCheck, LogOut } from "lucide-react";

interface AdminLayoutProps {
  children: React.ReactNode;
}

export function AdminLayout({ children }: AdminLayoutProps) {
  const router = useRouter();
  const { user, isLoading, logout } = useAuth();
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
    if (!isLoading && !user) {
      router.push("/login");
    }
  }, [isLoading, user, router]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#FAF9F6]">
        <div className="flex flex-col items-center gap-3">
          <div className="w-10 h-10 border-3 border-[#0B3D0B] border-t-[#FFD700] rounded-full animate-spin" />
          <span className="text-sm font-semibold text-emerald-950">
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

  return (
    <div className="min-h-screen bg-[#FAF9F6] text-[#1A1F16] flex flex-col">
      {/* Adaptive Sidebar */}
      <Sidebar
        collapsed={collapsed}
        onToggleCollapse={handleToggleCollapse}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
      />

      {/* Mobile Top Header */}
      <header className="md:hidden sticky top-0 z-30 flex items-center justify-between h-16 px-4 bg-[#0B3D0B] text-white border-b border-[#082D08] shadow-sm">
        <div className="flex items-center gap-3">
          <button
            onClick={() => setMobileOpen(true)}
            aria-label="Open navigation menu"
            className="p-2 rounded-lg text-emerald-100 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
          >
            <Menu className="w-6 h-6" />
          </button>
          <SJewlsBrand collapsed={false} theme="dark" size="sm" />
        </div>

        <div className="flex items-center gap-2">
          <div className="w-8 h-8 rounded-full bg-[#FFD700] text-[#0B3D0B] font-bold text-xs flex items-center justify-center">
            {user.fullName
              ? user.fullName
                  .split(" ")
                  .map((n) => n[0])
                  .join("")
                  .substring(0, 2)
                  .toUpperCase()
              : "SJ"}
          </div>
        </div>
      </header>

      {/* Main Content Area: dynamically offset based on sidebar width */}
      <main
        className={`flex-1 transition-all duration-200 ease-in-out ${
          collapsed ? "md:pl-[72px]" : "md:pl-[260px]"
        }`}
      >
        <div className="w-full max-w-[1600px] mx-auto p-4 sm:p-6 lg:p-8">
          {children}
        </div>
      </main>
    </div>
  );
}
