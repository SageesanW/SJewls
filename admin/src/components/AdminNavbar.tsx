"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useAuth } from "../lib/auth-context";
import { Users, LayoutDashboard, LogOut, ShieldCheck, Building2 } from "lucide-react";

export function AdminNavbar() {
  const { user, logout, isSuperAdmin, isBranchAdmin } = useAuth();
  const pathname = usePathname();

  const navItems = [
    { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
    ...(isSuperAdmin || isBranchAdmin
      ? [{ href: "/users", label: "Users & Staff", icon: Users }]
      : []),
  ];

  const primaryBranch = user?.assignedBranches?.[0];

  return (
    <header className="sticky top-0 z-50 glass-panel border-b border-white/10 bg-[#0d1017]/90 backdrop-blur-md">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Logo & Brand */}
          <div className="flex items-center gap-8">
            <Link href="/dashboard" className="flex items-center gap-3 group">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-[#d4af37] via-[#f7df8b] to-[#aa820a] p-[1.5px] shadow-lg shadow-[#d4af37]/20 group-hover:scale-105 transition-transform">
                <div className="w-full h-full bg-[#0d1017] rounded-[10px] flex items-center justify-center font-bold text-lg text-[#d4af37]">
                  SJ
                </div>
              </div>
              <div>
                <span className="text-xl font-bold tracking-tight gold-gradient-text block">
                  SJewls
                </span>
                <span className="text-[10px] font-medium tracking-wider text-gray-400 uppercase block -mt-1">
                  Admin Portal
                </span>
              </div>
            </Link>

            {/* Navigation items */}
            <nav className="hidden md:flex items-center gap-1">
              {navItems.map((item) => {
                const Icon = item.icon;
                const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    className={`flex items-center gap-2 px-3.5 py-2 rounded-lg text-sm font-medium transition-all ${
                      isActive
                        ? "bg-[#d4af37]/15 text-[#f7df8b] border border-[#d4af37]/30"
                        : "text-gray-300 hover:text-white hover:bg-white/5"
                    }`}
                  >
                    <Icon className="w-4 h-4" />
                    <span>{item.label}</span>
                  </Link>
                );
              })}
            </nav>
          </div>

          {/* User profile & actions */}
          <div className="flex items-center gap-4">
            {user && (
              <div className="flex items-center gap-3">
                <div className="hidden sm:flex flex-col text-right">
                  <div className="flex items-center justify-end gap-1.5">
                    <span className="text-sm font-semibold text-gray-200">
                      {user.fullName}
                    </span>
                    {user.roles?.map((role) => (
                      <span
                        key={role}
                        className="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-0.5 rounded-full bg-[#d4af37]/20 text-[#f7df8b] border border-[#d4af37]/30"
                      >
                        <ShieldCheck className="w-3 h-3" />
                        {role}
                      </span>
                    ))}
                  </div>
                  <div className="flex items-center justify-end gap-2 text-xs text-gray-400">
                    <span>{user.email}</span>
                    {primaryBranch && (
                      <span className="inline-flex items-center gap-1 text-gray-400">
                        • <Building2 className="w-3 h-3 text-[#d4af37]" />
                        {primaryBranch.name} ({primaryBranch.code})
                      </span>
                    )}
                  </div>
                </div>

                <button
                  onClick={() => logout()}
                  title="Sign out of Admin Portal"
                  className="flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium text-gray-300 hover:text-red-400 hover:bg-red-500/10 border border-white/5 hover:border-red-500/20 transition-all cursor-pointer"
                >
                  <LogOut className="w-4 h-4" />
                  <span className="hidden sm:inline">Sign Out</span>
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </header>
  );
}
