"use client";

import React, { useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "../lib/auth-context";
import { SJewlsBrand } from "./SJewlsBrand";
import {
  LayoutDashboard,
  Users,
  UserCheck,
  LogOut,
  ChevronLeft,
  ChevronRight,
  ShieldCheck,
  Building2,
} from "lucide-react";

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  mobileOpen: boolean;
  onCloseMobile: () => void;
}

export function Sidebar({
  collapsed,
  onToggleCollapse,
  mobileOpen,
  onCloseMobile,
}: SidebarProps) {
  const pathname = usePathname();
  const { user, logout, isSuperAdmin, isBranchAdmin } = useAuth();
  const [hoveredItem, setHoveredItem] = useState<string | null>(null);

  const navItems = [
    { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
    { href: "/customers", label: "Customers", icon: UserCheck },
    { href: "/branches", label: "Branches", icon: Building2 },
    ...(isSuperAdmin || isBranchAdmin
      ? [{ href: "/users", label: "Users & Staff", icon: Users }]
      : []),
  ];

  const primaryBranch = user?.assignedBranches?.[0];
  const userInitials = user?.fullName
    ? user.fullName
        .split(" ")
        .map((n) => n[0])
        .join("")
        .substring(0, 2)
        .toUpperCase()
    : "SJ";

  const renderNavLinks = (isMobile = false) => (
    <nav className="flex-1 px-3 py-4 space-y-1.5 overflow-y-auto sidebar-scroll">
      {navItems.map((item) => {
        const Icon = item.icon;
        const isActive =
          pathname === item.href || pathname.startsWith(`${item.href}/`);
        const showLabel = isMobile || !collapsed;

        return (
          <div
            key={item.href}
            className="relative"
            onMouseEnter={() => setHoveredItem(item.href)}
            onMouseLeave={() => setHoveredItem(null)}
          >
            <Link
              href={item.href}
              onClick={() => {
                if (isMobile) onCloseMobile();
              }}
              className={`flex items-center gap-3.5 px-3.5 py-3 rounded-xl text-sm font-medium transition-all duration-150 ${
                isActive
                  ? "nav-gold-active font-bold"
                  : "text-emerald-100/90 hover:text-white hover:bg-white/10"
              } ${!showLabel ? "justify-center px-0" : ""}`}
              aria-current={isActive ? "page" : undefined}
            >
              <Icon
                className={`w-5 h-5 flex-shrink-0 transition-transform ${
                  isActive ? "text-[#0B3D0B]" : "text-[#FFD700]"
                }`}
              />
              {showLabel && (
                <span className="truncate tracking-wide">{item.label}</span>
              )}
            </Link>

            {/* Tooltip for desktop collapsed mode */}
            {!showLabel && hoveredItem === item.href && (
              <div className="absolute left-full top-1/2 -translate-y-1/2 ml-3 px-2.5 py-1.5 bg-[#0A260A] text-white text-xs font-semibold rounded-lg shadow-xl border border-white/10 whitespace-nowrap z-50 pointer-events-none">
                {item.label}
              </div>
            )}
          </div>
        );
      })}
    </nav>
  );

  return (
    <>
      {/* ================= DESKTOP SIDEBAR ================= */}
      <aside
        className={`hidden md:flex flex-col fixed inset-y-0 left-0 z-40 bg-[#0B3D0B] text-white border-r border-[#082D08] transition-all duration-200 ease-in-out ${
          collapsed ? "w-[72px]" : "w-[260px]"
        }`}
      >
        {/* Brand Header & Toggle */}
        <div className="h-18 flex items-center justify-between px-4 border-b border-white/10">
          <Link
            href="/dashboard"
            className="flex items-center gap-3 overflow-hidden"
            title="SJewls Admin"
          >
            <SJewlsBrand collapsed={collapsed} theme="dark" size="md" />
          </Link>

          <button
            onClick={onToggleCollapse}
            aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            className="p-1.5 rounded-lg text-emerald-200 hover:text-white hover:bg-white/10 transition-colors cursor-pointer"
            title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          >
            {collapsed ? (
              <ChevronRight className="w-5 h-5" />
            ) : (
              <ChevronLeft className="w-5 h-5" />
            )}
          </button>
        </div>

        {/* Navigation Items */}
        {renderNavLinks(false)}

        {/* User Card & Sign Out at Bottom */}
        <div className="p-3 border-t border-white/10 bg-[#082B08]/80">
          {!collapsed ? (
            <div className="flex items-center justify-between gap-2 p-2 rounded-xl bg-white/5 border border-white/5">
              <div className="flex items-center gap-2.5 min-w-0">
                <div className="w-9 h-9 rounded-full bg-[#FFD700] text-[#0B3D0B] font-bold text-xs flex items-center justify-center flex-shrink-0 shadow-sm">
                  {userInitials}
                </div>
                <div className="flex flex-col min-w-0">
                  <span className="text-xs font-bold text-white truncate">
                    {user?.fullName || "Staff Member"}
                  </span>
                  <div className="flex items-center gap-1 text-[11px] text-emerald-200/80 truncate">
                    <ShieldCheck className="w-3 h-3 text-[#FFD700] flex-shrink-0" />
                    <span className="truncate">
                      {user?.roles?.[0] || "Staff"}
                    </span>
                  </div>
                </div>
              </div>

              <button
                onClick={() => logout()}
                title="Sign out"
                className="p-1.5 text-emerald-200 hover:text-red-300 hover:bg-red-500/15 rounded-lg transition-colors cursor-pointer flex-shrink-0"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </div>
          ) : (
            <div className="flex flex-col items-center gap-2">
              <div
                className="w-9 h-9 rounded-full bg-[#FFD700] text-[#0B3D0B] font-bold text-xs flex items-center justify-center shadow-sm"
                title={`${user?.fullName} (${user?.roles?.[0]})`}
              >
                {userInitials}
              </div>
              <button
                onClick={() => logout()}
                title="Sign out"
                className="p-2 text-emerald-200 hover:text-red-300 hover:bg-red-500/15 rounded-lg transition-colors cursor-pointer"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </div>
          )}
        </div>
      </aside>

      {/* ================= MOBILE DRAWER & BACKDROP ================= */}
      {mobileOpen && (
        <div className="md:hidden fixed inset-0 z-50 flex">
          {/* Backdrop overlay */}
          <div
            className="fixed inset-0 bg-black/60 backdrop-blur-xs transition-opacity"
            onClick={onCloseMobile}
            aria-hidden="true"
          />

          {/* Drawer content */}
          <div className="relative flex-1 flex flex-col max-w-xs w-full bg-[#0B3D0B] text-white shadow-2xl z-50">
            {/* Drawer Header */}
            <div className="h-18 flex items-center justify-between px-5 border-b border-white/10">
              <SJewlsBrand collapsed={false} theme="dark" size="md" />
              <button
                onClick={onCloseMobile}
                aria-label="Close menu"
                className="p-2 rounded-lg text-emerald-200 hover:text-white hover:bg-white/10 cursor-pointer"
              >
                <ChevronLeft className="w-6 h-6" />
              </button>
            </div>

            {/* Mobile Nav Links */}
            {renderNavLinks(true)}

            {/* Mobile User Card */}
            <div className="p-4 border-t border-white/10 bg-[#082B08]">
              <div className="flex items-center justify-between gap-3 p-2.5 rounded-xl bg-white/5 border border-white/5 mb-3">
                <div className="flex items-center gap-3 min-w-0">
                  <div className="w-10 h-10 rounded-full bg-[#FFD700] text-[#0B3D0B] font-bold text-sm flex items-center justify-center flex-shrink-0">
                    {userInitials}
                  </div>
                  <div className="flex flex-col min-w-0">
                    <span className="text-sm font-bold text-white truncate">
                      {user?.fullName || "Staff Member"}
                    </span>
                    <span className="text-xs text-emerald-300">
                      {user?.roles?.[0] || "Staff"}
                    </span>
                    {primaryBranch && (
                      <span className="text-[11px] text-gray-300 flex items-center gap-1 mt-0.5">
                        <Building2 className="w-3 h-3 text-[#FFD700]" />
                        {primaryBranch.name}
                      </span>
                    )}
                  </div>
                </div>
              </div>

              <button
                onClick={() => {
                  onCloseMobile();
                  logout();
                }}
                className="w-full flex items-center justify-center gap-2 py-2.5 px-4 rounded-xl text-sm font-semibold bg-red-600/20 text-red-200 hover:bg-red-600/30 border border-red-500/30 transition-colors cursor-pointer"
              >
                <LogOut className="w-4 h-4" />
                Sign Out
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
