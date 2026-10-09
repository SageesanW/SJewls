"use client";

import React, { useState, useEffect } from "react";
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
  Gem,
  ChevronDown,
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

  // Expandable Jewellery Plan state
  const isJewelleryPlanActive = pathname.startsWith("/jewellery-plans");
  const [jewelleryExpanded, setJewelleryExpanded] = useState<boolean>(() => {
    return pathname.startsWith("/jewellery-plans");
  });

  useEffect(() => {
    if (pathname.startsWith("/jewellery-plans")) {
      setJewelleryExpanded(true);
    }
  }, [pathname]);

  const navItems = [
    { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
    { href: "/customers", label: "Customers", icon: UserCheck },
    { href: "/branches", label: "Branches", icon: Building2 },
    ...(isSuperAdmin || isBranchAdmin
      ? [{ href: "/users", label: "Users & Staff", icon: Users }]
      : []),
  ];

  const jewellerySubItems = [
    {
      href: "/jewellery-plans/categories",
      label: "Categories",
      isActive:
        pathname === "/jewellery-plans/categories" ||
        pathname === "/jewellery-plans",
      enabled: true,
    },
    {
      href: "/jewellery-plans/plans",
      label: "Plans",
      isActive:
        pathname === "/jewellery-plans/plans" ||
        pathname.startsWith("/jewellery-plans/plans/"),
      enabled: true,
    },
    {
      href: "#plan-customers",
      label: "Plan Customers",
      isActive: pathname === "/jewellery-plans/customers",
      enabled: false,
    },
    {
      href: "#payments",
      label: "Payments",
      isActive: pathname === "/jewellery-plans/payments",
      enabled: false,
    },
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

  const renderNavLinks = (isMobile = false) => {
    const showLabel = isMobile || !collapsed;

    return (
      <nav className="flex-1 px-3 py-4 space-y-1.5 overflow-y-auto sidebar-scroll">
        {/* Dashboard & Customers */}
        {navItems.slice(0, 2).map((item) => {
          const Icon = item.icon;
          const isActive =
            pathname === item.href || pathname.startsWith(`${item.href}/`);

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

        {/* Expandable Jewellery Plan Group */}
        <div
          className="relative"
          onMouseEnter={() => setHoveredItem("jewellery-plan")}
          onMouseLeave={() => setHoveredItem(null)}
        >
          {showLabel ? (
            <div>
              <button
                type="button"
                onClick={() => setJewelleryExpanded((prev) => !prev)}
                className={`w-full flex items-center justify-between px-3.5 py-3 rounded-xl text-sm font-medium transition-all duration-150 cursor-pointer ${
                  isJewelleryPlanActive
                    ? "nav-gold-active font-bold shadow-sm"
                    : "text-emerald-100/90 hover:text-white hover:bg-white/10"
                }`}
                aria-expanded={jewelleryExpanded}
              >
                <div className="flex items-center gap-3.5 min-w-0">
                  <Gem
                    className={`w-5 h-5 flex-shrink-0 ${
                      isJewelleryPlanActive ? "text-[#0B3D0B]" : "text-[#FFD700]"
                    }`}
                  />
                  <span className="truncate tracking-wide">Jewellery Plan</span>
                </div>
                <ChevronDown
                  className={`w-4 h-4 flex-shrink-0 transition-transform duration-200 ${
                    jewelleryExpanded ? "rotate-180" : ""
                  } ${
                    isJewelleryPlanActive ? "text-[#0B3D0B]" : "text-emerald-300"
                  }`}
                />
              </button>

              {/* Collapsible Submenu */}
              {jewelleryExpanded && (
                <div className="mt-1 ml-4 pl-3.5 border-l-2 border-emerald-700/50 space-y-1 py-1 animate-fadeIn">
                  {jewellerySubItems.map((sub) => {
                    if (sub.enabled) {
                      return (
                        <Link
                          key={sub.href}
                          href={sub.href}
                          onClick={() => {
                            if (isMobile) onCloseMobile();
                          }}
                          className={`flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-medium transition-colors ${
                            sub.isActive
                              ? "bg-[#FFD700]/20 text-[#FFD700] font-bold border border-[#FFD700]/30 shadow-xs"
                              : "text-emerald-200/90 hover:text-white hover:bg-white/5"
                          }`}
                        >
                          <span
                            className={`w-1.5 h-1.5 rounded-full ${
                              sub.isActive ? "bg-[#FFD700]" : "bg-emerald-400/60"
                            }`}
                          />
                          <span className="truncate">{sub.label}</span>
                        </Link>
                      );
                    }

                    return (
                      <div
                        key={sub.label}
                        className="flex items-center justify-between px-3 py-2 text-xs text-emerald-400/50 cursor-not-allowed select-none rounded-lg"
                        title="Module coming soon"
                      >
                        <div className="flex items-center gap-2.5">
                          <span className="w-1.5 h-1.5 rounded-full bg-emerald-700/60" />
                          <span>{sub.label}</span>
                        </div>
                        <span className="text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded bg-white/5 text-emerald-400/60 border border-white/5">
                          Soon
                        </span>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          ) : (
            /* Desktop Collapsed View */
            <div>
              <Link
                href="/jewellery-plans/categories"
                className={`flex items-center justify-center p-3 rounded-xl transition-all ${
                  isJewelleryPlanActive
                    ? "nav-gold-active font-bold"
                    : "text-emerald-100/90 hover:text-white hover:bg-white/10"
                }`}
                title="Jewellery Plan Categories"
              >
                <Gem
                  className={`w-5 h-5 flex-shrink-0 ${
                    isJewelleryPlanActive ? "text-[#0B3D0B]" : "text-[#FFD700]"
                  }`}
                />
              </Link>

              {hoveredItem === "jewellery-plan" && (
                <div className="absolute left-full top-0 ml-3 w-48 bg-[#0A260A] text-white p-2 rounded-xl shadow-2xl border border-white/10 z-50">
                  <div className="px-2.5 py-1.5 text-xs font-bold text-[#FFD700] border-b border-white/10 mb-1">
                    Jewellery Plan
                  </div>
                  <Link
                    href="/jewellery-plans/categories"
                    className="flex items-center gap-2 px-2.5 py-1.5 text-xs rounded-lg text-emerald-100 hover:text-white hover:bg-white/10 font-medium"
                  >
                    <span className="w-1.5 h-1.5 rounded-full bg-[#FFD700]" />
                    Categories
                  </Link>
                  <div className="flex items-center justify-between px-2.5 py-1.5 text-xs text-emerald-400/50">
                    <span>Plans</span>
                    <span className="text-[9px] px-1 bg-white/5 rounded">Soon</span>
                  </div>
                  <div className="flex items-center justify-between px-2.5 py-1.5 text-xs text-emerald-400/50">
                    <span>Plan Customers</span>
                    <span className="text-[9px] px-1 bg-white/5 rounded">Soon</span>
                  </div>
                  <div className="flex items-center justify-between px-2.5 py-1.5 text-xs text-emerald-400/50">
                    <span>Payments</span>
                    <span className="text-[9px] px-1 bg-white/5 rounded">Soon</span>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Branches & Users */}
        {navItems.slice(2).map((item) => {
          const Icon = item.icon;
          const isActive =
            pathname === item.href || pathname.startsWith(`${item.href}/`);

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
  };

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
