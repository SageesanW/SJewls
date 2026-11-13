"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "../lib/auth-context";
import { SJewlsBrand } from "./SJewlsBrand";
import { Badge } from "./ui/badge";
import { cn } from "../lib/utils";
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
  Coins,
  ChevronDown,
  Scale,
  Sparkles,
  MapPin,
} from "lucide-react";

import { StaffUser } from "../types/auth";

interface SidebarProps {
  collapsed: boolean;
  onToggleCollapse: () => void;
  mobileOpen: boolean;
  onCloseMobile: () => void;
  overrideUser?: StaffUser;
}

export function Sidebar({
  collapsed,
  onToggleCollapse,
  mobileOpen,
  onCloseMobile,
  overrideUser,
}: SidebarProps) {
  const pathname = usePathname();
  const { user: authUser, logout, isSuperAdmin: authIsSuperAdmin, isBranchAdmin: authIsBranchAdmin } = useAuth();
  const user = overrideUser || authUser;
  const isSuperAdmin = Boolean(user?.roles?.some((r) => r.toLowerCase().includes("super admin"))) || authIsSuperAdmin;
  const isBranchAdmin = Boolean(user?.roles?.some((r) => r.toLowerCase().includes("branch admin"))) || authIsBranchAdmin;
  const [hoveredItem, setHoveredItem] = useState<string | null>(null);

  const isJewelleryPlanActive = pathname.startsWith("/jewellery-plans");
  const isChituPlanActive = pathname.startsWith("/chitu-plans");
  const isGoldRateActive = pathname.startsWith("/gold-rates");

  const [jewelleryExpanded, setJewelleryExpanded] = useState<boolean>(() => {
    return pathname.startsWith("/jewellery-plans");
  });

  useEffect(() => {
    if (pathname.startsWith("/jewellery-plans") && !jewelleryExpanded) {
      setJewelleryExpanded(true);
    }
  }, [pathname, jewelleryExpanded]);

  const overviewNav = [
    { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  ];

  const jewellerySubNav = [
    {
      href: "/jewellery-plans/categories",
      label: "Categories",
      isActive:
        pathname === "/jewellery-plans/categories" ||
        pathname === "/jewellery-plans",
    },
    {
      href: "/jewellery-plans/plans",
      label: "Plans",
      isActive:
        pathname === "/jewellery-plans/plans" ||
        pathname.startsWith("/jewellery-plans/plans/"),
    },
    {
      href: "/jewellery-plans/customers",
      label: "Plan Customers",
      isActive: pathname === "/jewellery-plans/customers",
    },
    {
      href: "/jewellery-plans/payments",
      label: "Payments",
      isActive: pathname === "/jewellery-plans/payments",
    },
  ];

  const operationsNav = [
    {
      type: "group" as const,
      id: "jewellery-plans",
      label: "Jewellery Plan",
      icon: Gem,
      isActive: isJewelleryPlanActive,
      subItems: jewellerySubNav,
    },
    { href: "/chitu-plans", label: "Chitu Plans", icon: Coins, isActive: isChituPlanActive },
    { href: "/gold-rates", label: "Gold Rates", icon: Scale, isActive: isGoldRateActive },
  ];

  const managementNav = [
    { href: "/customers", label: "Customers", icon: UserCheck },
    { href: "/branches", label: "Branches", icon: Building2 },
    ...(isSuperAdmin || isBranchAdmin
      ? [{ href: "/users", label: "Staff & Users", icon: Users }]
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

  const renderNavLinks = (isMobile = false) => {
    const showLabel = isMobile || !collapsed;

    return (
      <nav
        aria-label="Main Navigation"
        className="flex-1 px-3 py-4 space-y-6 overflow-y-auto sidebar-scroll"
      >
        {/* SECTION 1: OVERVIEW */}
        <div>
          {showLabel && (
            <div className="px-3 pb-2 text-[10px] font-bold uppercase tracking-[0.16em] text-[#C5A059]/75 select-none flex items-center gap-1.5">
              <span>Overview</span>
            </div>
          )}
          <div className="space-y-1">
            {overviewNav.map((item) => {
              const Icon = item.icon;
              const isActive =
                pathname === item.href ||
                (item.href !== "/dashboard" && pathname.startsWith(`${item.href}/`));

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
                    className={cn(
                      "group flex items-center gap-3 px-3 py-2.5 rounded-xl text-xs font-medium transition-all duration-200 focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#C5A059]",
                      isActive
                        ? "nav-gold-active"
                        : "text-emerald-100/75 hover:text-white hover:bg-white/8",
                      !showLabel && "justify-center px-0"
                    )}
                    aria-current={isActive ? "page" : undefined}
                  >
                    <Icon
                      className={cn(
                        "w-4 h-4 flex-shrink-0 transition-transform duration-200 group-hover:scale-110",
                        isActive ? "text-[#D4AF37]" : "text-emerald-200/80 group-hover:text-white"
                      )}
                    />
                    {showLabel && (
                      <span className="truncate tracking-wide">{item.label}</span>
                    )}
                  </Link>

                  {!showLabel && hoveredItem === item.href && (
                    <div className="absolute left-full top-1/2 -translate-y-1/2 ml-3 px-3 py-1.5 bg-[#061E06] text-white text-xs font-semibold rounded-lg shadow-xl border border-[#C5A059]/30 whitespace-nowrap z-50 pointer-events-none animate-fadeIn">
                      {item.label}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* SECTION 2: SCHEMES & OPERATIONS */}
        <div>
          {showLabel && (
            <div className="px-3 pb-2 text-[10px] font-bold uppercase tracking-[0.16em] text-[#C5A059]/75 select-none flex items-center gap-1.5">
              <span>Schemes & Catalog</span>
            </div>
          )}
          <div className="space-y-1">
            {operationsNav.map((item) => {
              if (item.type === "group") {
                const Icon = item.icon;
                const isGroupActive = item.isActive;

                return (
                  <div key={item.id} className="space-y-1">
                    <button
                      type="button"
                      onClick={() => {
                        if (!collapsed) {
                          setJewelleryExpanded(!jewelleryExpanded);
                        }
                      }}
                      className={cn(
                        "w-full group flex items-center justify-between px-3 py-2.5 rounded-xl text-xs font-medium transition-all duration-200 cursor-pointer focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#C5A059]",
                        isGroupActive
                          ? "bg-white/10 text-white font-semibold"
                          : "text-emerald-100/75 hover:text-white hover:bg-white/8",
                        !showLabel && "justify-center px-0"
                      )}
                      aria-expanded={jewelleryExpanded}
                      aria-label="Jewellery Plan Menu"
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <Icon
                          className={cn(
                            "w-4 h-4 flex-shrink-0 transition-transform duration-200 group-hover:scale-110",
                            isGroupActive ? "text-[#D4AF37]" : "text-emerald-200/80 group-hover:text-white"
                          )}
                        />
                        {showLabel && (
                          <span className="truncate tracking-wide">{item.label}</span>
                        )}
                      </div>

                      {showLabel && (
                        <ChevronDown
                          className={cn(
                            "w-3.5 h-3.5 text-emerald-300/70 transition-transform duration-200",
                            jewelleryExpanded ? "rotate-180 text-[#D4AF37]" : ""
                          )}
                        />
                      )}
                    </button>

                    {/* Submenu links */}
                    {showLabel && jewelleryExpanded && (
                      <div className="pl-6 pr-1 py-1 space-y-1 border-l border-[#C5A059]/20 ml-5">
                        {item.subItems.map((sub) => (
                          <Link
                            key={sub.href}
                            href={sub.href}
                            onClick={() => {
                              if (isMobile) onCloseMobile();
                            }}
                            className={cn(
                              "block px-3 py-1.5 rounded-lg text-xs font-medium transition-all duration-150 focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-[#C5A059]",
                              sub.isActive
                                ? "text-white bg-white/12 font-bold border-l-2 border-[#D4AF37]"
                                : "text-emerald-200/70 hover:text-white hover:bg-white/5"
                            )}
                            aria-current={sub.isActive ? "page" : undefined}
                          >
                            <span className="truncate">{sub.label}</span>
                          </Link>
                        ))}
                      </div>
                    )}
                  </div>
                );
              }

              // Standard Link (Chitu Plans, Gold Rates)
              const Icon = item.icon;
              const isActive = item.isActive;

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
                    className={cn(
                      "group flex items-center gap-3 px-3 py-2.5 rounded-xl text-xs font-medium transition-all duration-200 focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#C5A059]",
                      isActive
                        ? "nav-gold-active"
                        : "text-emerald-100/75 hover:text-white hover:bg-white/8",
                      !showLabel && "justify-center px-0"
                    )}
                    aria-current={isActive ? "page" : undefined}
                  >
                    <Icon
                      className={cn(
                        "w-4 h-4 flex-shrink-0 transition-transform duration-200 group-hover:scale-110",
                        isActive ? "text-[#D4AF37]" : "text-emerald-200/80 group-hover:text-white"
                      )}
                    />
                    {showLabel && (
                      <span className="truncate tracking-wide">{item.label}</span>
                    )}
                  </Link>

                  {!showLabel && hoveredItem === item.href && (
                    <div className="absolute left-full top-1/2 -translate-y-1/2 ml-3 px-3 py-1.5 bg-[#061E06] text-white text-xs font-semibold rounded-lg shadow-xl border border-[#C5A059]/30 whitespace-nowrap z-50 pointer-events-none animate-fadeIn">
                      {item.label}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* SECTION 3: MANAGEMENT */}
        <div>
          {showLabel && (
            <div className="px-3 pb-2 text-[10px] font-bold uppercase tracking-[0.16em] text-[#C5A059]/75 select-none flex items-center gap-1.5">
              <span>Management & Operations</span>
            </div>
          )}
          <div className="space-y-1">
            {managementNav.map((item) => {
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
                    className={cn(
                      "group flex items-center gap-3 px-3 py-2.5 rounded-xl text-xs font-medium transition-all duration-200 focus-visible:outline-hidden focus-visible:ring-2 focus-visible:ring-[#C5A059]",
                      isActive
                        ? "nav-gold-active"
                        : "text-emerald-100/75 hover:text-white hover:bg-white/8",
                      !showLabel && "justify-center px-0"
                    )}
                    aria-current={isActive ? "page" : undefined}
                  >
                    <Icon
                      className={cn(
                        "w-4 h-4 flex-shrink-0 transition-transform duration-200 group-hover:scale-110",
                        isActive ? "text-[#D4AF37]" : "text-emerald-200/80 group-hover:text-white"
                      )}
                    />
                    {showLabel && (
                      <span className="truncate tracking-wide">{item.label}</span>
                    )}
                  </Link>

                  {!showLabel && hoveredItem === item.href && (
                    <div className="absolute left-full top-1/2 -translate-y-1/2 ml-3 px-3 py-1.5 bg-[#061E06] text-white text-xs font-semibold rounded-lg shadow-xl border border-[#C5A059]/30 whitespace-nowrap z-50 pointer-events-none animate-fadeIn">
                      {item.label}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      </nav>
    );
  };

  return (
    <>
      {/* ================= DESKTOP SIDEBAR ================= */}
      <aside
        className={cn(
          "hidden md:flex flex-col fixed top-0 left-0 bottom-0 z-30 transition-all duration-300 ease-in-out bg-gradient-to-b from-[#061E06] via-[#082808] to-[#041604] border-r border-[#C5A059]/25 shadow-2xl",
          collapsed ? "w-[72px]" : "w-[260px]"
        )}
        aria-label="Sidebar Navigation"
      >
        {/* Brand Header */}
        <div className="h-16 px-4 flex items-center justify-between border-b border-[#C5A059]/20 flex-shrink-0 bg-black/10">
          <Link href="/dashboard" className="focus:outline-hidden focus-visible:ring-2 focus-visible:ring-[#C5A059] rounded-lg">
            <SJewlsBrand
              collapsed={collapsed}
              theme="dark"
              size={collapsed ? "sm" : "md"}
            />
          </Link>
        </div>

        {/* Selected Operating Branch Context Badge */}
        {!collapsed && primaryBranch && (
          <div className="px-3 pt-3.5 pb-1 flex-shrink-0">
            <div className="p-2.5 rounded-xl bg-gradient-to-br from-white/8 to-white/4 border border-[#C5A059]/25 shadow-xs backdrop-blur-xs flex items-center justify-between gap-2">
              <div className="flex items-center gap-2.5 min-w-0">
                <div className="w-7 h-7 rounded-lg bg-[#C5A059]/15 border border-[#C5A059]/30 flex items-center justify-center flex-shrink-0">
                  <Building2 className="w-3.5 h-3.5 text-[#D4AF37]" />
                </div>
                <div className="min-w-0">
                  <span className="text-[10px] text-[#C5A059] font-bold uppercase tracking-wider block leading-none">
                    Active Branch
                  </span>
                  <span className="text-xs font-semibold text-white truncate block mt-0.5" title={primaryBranch.name}>
                    {primaryBranch.name}
                  </span>
                </div>
              </div>
              <span className="text-[9.5px] font-mono font-bold px-1.5 py-0.5 rounded bg-white/10 text-emerald-200 border border-white/15 flex-shrink-0">
                {primaryBranch.code}
              </span>
            </div>
          </div>
        )}

        {/* Navigation Links Scroll Area */}
        {renderNavLinks(false)}

        {/* Bottom User Card & Collapse Toggle */}
        <div className="p-3 border-t border-[#C5A059]/20 bg-black/20 flex-shrink-0 space-y-2">
          {/* User info */}
          <div
            className={cn(
              "flex items-center gap-2.5 p-2 rounded-xl bg-white/6 border border-white/10 transition-colors",
              collapsed ? "justify-center" : "justify-between"
            )}
          >
            <div className="flex items-center gap-2.5 min-w-0">
              <div
                className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#D4AF37] to-[#8C6D2D] text-[#061E06] font-bold text-xs flex items-center justify-center shadow-xs flex-shrink-0"
                title={user?.fullName || "Staff"}
              >
                {userInitials}
              </div>

              {!collapsed && (
                <div className="flex flex-col min-w-0">
                  <span
                    className="text-xs font-bold text-white truncate"
                    title={user?.fullName}
                  >
                    {user?.fullName || "Staff User"}
                  </span>
                  <div className="flex items-center gap-1.5 mt-0.5">
                    <span className="text-[9.5px] font-semibold text-[#D4AF37] bg-[#C5A059]/15 border border-[#C5A059]/30 px-1.5 py-0.2 rounded">
                      {user?.roles?.[0] || "Staff"}
                    </span>
                  </div>
                </div>
              )}
            </div>

            {!collapsed && (
              <button
                type="button"
                onClick={() => logout()}
                title="Sign out of SJewls Admin"
                className="p-1.5 text-emerald-200/60 hover:text-rose-400 hover:bg-rose-500/15 rounded-lg transition-colors cursor-pointer focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-rose-400"
                aria-label="Sign out"
              >
                <LogOut className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          {/* Quick Collapse Arrow Button */}
          <button
            type="button"
            onClick={onToggleCollapse}
            className="w-full flex items-center justify-center gap-2 py-1.5 rounded-lg text-emerald-200/60 hover:text-white hover:bg-white/8 text-[11px] font-medium transition-colors cursor-pointer focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-[#C5A059]"
            title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
            aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
          >
            {collapsed ? (
              <ChevronRight className="w-4 h-4 text-[#D4AF37]" />
            ) : (
              <>
                <ChevronLeft className="w-4 h-4" />
                <span className="text-[10px] uppercase tracking-wider text-emerald-200/70 font-semibold">Collapse Sidebar</span>
              </>
            )}
          </button>
        </div>
      </aside>

      {/* ================= MOBILE DRAWER (Narrow Viewports) ================= */}
      {mobileOpen && (
        <div className="md:hidden fixed inset-0 z-50 flex">
          {/* Overlay backdrop */}
          <div
            className="fixed inset-0 bg-black/70 backdrop-blur-xs transition-opacity"
            onClick={onCloseMobile}
            aria-hidden="true"
          />

          {/* Offcanvas panel */}
          <div className="relative w-4/5 max-w-xs flex-1 flex flex-col bg-gradient-to-b from-[#061E06] via-[#082808] to-[#041604] border-r border-[#C5A059]/25 text-white shadow-2xl z-10 animate-fadeIn">
            <div className="h-16 px-4 flex items-center justify-between border-b border-[#C5A059]/20 flex-shrink-0">
              <SJewlsBrand theme="dark" size="sm" />
              <button
                type="button"
                onClick={onCloseMobile}
                className="p-1.5 rounded-lg text-emerald-200 hover:text-white hover:bg-white/10 cursor-pointer"
                aria-label="Close navigation menu"
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
            </div>

            {/* Operating Branch Context */}
            {primaryBranch && (
              <div className="p-3 border-b border-[#C5A059]/15 bg-white/4">
                <div className="flex items-center gap-2">
                  <Building2 className="w-4 h-4 text-[#D4AF37]" />
                  <div className="min-w-0">
                    <span className="text-[10px] text-[#C5A059] font-bold uppercase tracking-wider block">
                      Branch
                    </span>
                    <span className="text-xs font-semibold text-white truncate block">
                      {primaryBranch.name} ({primaryBranch.code})
                    </span>
                  </div>
                </div>
              </div>
            )}

            {renderNavLinks(true)}

            {/* Mobile Footer */}
            <div className="p-3 border-t border-[#C5A059]/20 bg-black/20 flex-shrink-0 flex items-center justify-between">
              <div className="flex items-center gap-2 min-w-0">
                <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#D4AF37] to-[#8C6D2D] text-[#061E06] font-bold text-xs flex items-center justify-center shadow-xs">
                  {userInitials}
                </div>
                <div className="min-w-0">
                  <span className="text-xs font-bold text-white truncate block">
                    {user?.fullName}
                  </span>
                  <span className="text-[10px] text-[#D4AF37] block">
                    {user?.roles?.[0] || "Staff"}
                  </span>
                </div>
              </div>

              <button
                type="button"
                onClick={() => {
                  onCloseMobile();
                  logout();
                }}
                className="p-2 text-rose-300 hover:text-rose-100 hover:bg-rose-500/20 rounded-lg cursor-pointer"
                aria-label="Sign out"
              >
                <LogOut className="w-4 h-4" />
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
