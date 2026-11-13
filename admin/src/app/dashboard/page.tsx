"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "../../lib/auth-context";
import { AdminLayout } from "../../components/AdminLayout";
import { SummaryCard } from "../../components/dashboard/SummaryCard";
import { adminApi } from "../../lib/api";
import { StaffUser, Branch } from "../../types/auth";
import { CustomerMetrics } from "../../types/customer";
import { Card, CardContent, CardHeader, CardTitle } from "../../components/ui/card";
import { Badge } from "../../components/ui/badge";
import { Button } from "../../components/ui/button";
import {
  Users,
  Building2,
  ShieldCheck,
  UserPlus,
  UserCheck,
  Layers,
  TrendingUp,
  ArrowRight,
  Activity,
  Coins,
  Gem,
  Scale,
  Sparkles,
} from "lucide-react";

export default function DashboardPage() {
  const { user, isSuperAdmin, isBranchAdmin } = useAuth();

  const [staffCount, setStaffCount] = useState<number | null>(null);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [customerMetrics, setCustomerMetrics] = useState<CustomerMetrics | null>(null);
  const [recentUsers, setRecentUsers] = useState<StaffUser[]>([]);
  const [dataLoading, setDataLoading] = useState(true);

  useEffect(() => {
    if (user) {
      loadDashboardData();
    }
  }, [user]);

  const loadDashboardData = async () => {
    setDataLoading(true);
    try {
      const branchList = await adminApi.getBranches();
      setBranches(branchList);

      const metrics = await adminApi.getCustomerMetrics();
      setCustomerMetrics(metrics);

      if (isSuperAdmin || isBranchAdmin) {
        const users = await adminApi.getUsers();
        setStaffCount(users.length);
        setRecentUsers(users.slice(0, 5));
      }
    } catch (err) {
      console.error("Failed to load dashboard data", err);
    } finally {
      setDataLoading(false);
    }
  };

  const primaryBranch = user?.assignedBranches?.[0];

  return (
    <AdminLayout title="Operations Command">
      <div className="space-y-6">
        {/* Welcome Hero Banner */}
        <div className="bg-gradient-to-br from-[#051E05] via-[#092B09] to-[#041604] border border-[#C5A059]/30 text-white rounded-2xl p-6 sm:p-8 shadow-md relative overflow-hidden">
          {/* Subtle gold radial ambient glow */}
          <div className="absolute top-0 right-0 w-96 h-96 bg-[#D4AF37]/10 rounded-full blur-3xl pointer-events-none" />

          <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <span className="px-2.5 py-0.5 rounded-full text-[10.5px] font-bold tracking-wider uppercase bg-[#C5A059]/20 text-[#D4AF37] border border-[#C5A059]/35">
                  {user?.roles?.[0] || "Staff Operations"}
                </span>
                <span className="text-xs text-emerald-200/80 flex items-center gap-1.5 font-medium">
                  <Activity className="w-3.5 h-3.5 text-[#D4AF37]" />
                  Active Branch Session
                </span>
              </div>
              <h1 className="text-2xl sm:text-3xl font-serif-luxury font-bold text-white tracking-tight leading-tight">
                Welcome back, {user?.fullName}
              </h1>
              <p className="text-xs sm:text-sm text-emerald-100/75 max-w-2xl leading-relaxed">
                SJewls Operations Portal manages customer gold accumulation, Chitu savings rosters, fine jewellery schemes, and branch-level inventory.
              </p>
            </div>

            {/* Quick Actions */}
            <div className="flex flex-wrap items-center gap-3">
              <Link href="/customers">
                <Button variant="default" size="default" className="btn-gold rounded-xl text-xs font-bold px-4 py-2.5 cursor-pointer">
                  <UserCheck className="w-4 h-4" />
                  <span>Customer Directory</span>
                </Button>
              </Link>

              {isSuperAdmin && (
                <Link href="/users">
                  <Button
                    variant="outline"
                    size="default"
                    className="bg-white/10 hover:bg-white/20 border-white/20 text-white rounded-xl text-xs font-semibold px-4 py-2.5 cursor-pointer"
                  >
                    <UserPlus className="w-4 h-4" />
                    <span>Manage Staff</span>
                  </Button>
                </Link>
              )}
            </div>
          </div>
        </div>

        {/* Quick Operations Nav Strip */}
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <Link href="/chitu-plans" className="group">
            <div className="p-4 rounded-2xl bg-white border border-[#E8E5DD] hover:border-[#D8D3C5] hover:shadow-md transition-all duration-200 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-[#C5A059]/15 border border-[#C5A059]/30 flex items-center justify-center flex-shrink-0 group-hover:scale-105 transition-transform">
                  <Coins className="w-5 h-5 text-[#8C6D2D]" />
                </div>
                <div>
                  <span className="text-xs font-bold text-[#151A14] block group-hover:text-[#0B3D0B] transition-colors">
                    Chitu Savings Plans
                  </span>
                  <span className="text-[11px] text-stone-500">
                    Monthly round pools & raffles
                  </span>
                </div>
              </div>
              <ArrowRight className="w-4 h-4 text-stone-400 group-hover:text-[#0B3D0B] group-hover:translate-x-1 transition-all" />
            </div>
          </Link>

          <Link href="/jewellery-plans/plans" className="group">
            <div className="p-4 rounded-2xl bg-white border border-[#E8E5DD] hover:border-[#D8D3C5] hover:shadow-md transition-all duration-200 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-[#0B3D0B]/10 border border-[#0B3D0B]/20 flex items-center justify-center flex-shrink-0 group-hover:scale-105 transition-transform">
                  <Gem className="w-5 h-5 text-[#0B3D0B]" />
                </div>
                <div>
                  <span className="text-xs font-bold text-[#151A14] block group-hover:text-[#0B3D0B] transition-colors">
                    Jewellery Investment
                  </span>
                  <span className="text-[11px] text-stone-500">
                    Target gram accumulation
                  </span>
                </div>
              </div>
              <ArrowRight className="w-4 h-4 text-stone-400 group-hover:text-[#0B3D0B] group-hover:translate-x-1 transition-all" />
            </div>
          </Link>

          <Link href="/gold-rates" className="group">
            <div className="p-4 rounded-2xl bg-white border border-[#E8E5DD] hover:border-[#D8D3C5] hover:shadow-md transition-all duration-200 flex items-center justify-between">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-50 border border-amber-200 flex items-center justify-center flex-shrink-0 group-hover:scale-105 transition-transform">
                  <Scale className="w-5 h-5 text-amber-700" />
                </div>
                <div>
                  <span className="text-xs font-bold text-[#151A14] block group-hover:text-[#0B3D0B] transition-colors">
                    Daily Gold Rates
                  </span>
                  <span className="text-[11px] text-stone-500">
                    22K & 24K spot market pricing
                  </span>
                </div>
              </div>
              <ArrowRight className="w-4 h-4 text-stone-400 group-hover:text-[#0B3D0B] group-hover:translate-x-1 transition-all" />
            </div>
          </Link>
        </div>

        {/* Stats Grid - Modernized Summary Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <SummaryCard
            label="Total Customers"
            value={customerMetrics?.totalCustomers}
            icon={UserCheck}
            href="/customers"
            linkText="Open Directory"
            variant="default"
            isLoading={dataLoading}
          />

          <SummaryCard
            label="Active Slots"
            value={customerMetrics?.totalSlots}
            icon={Layers}
            subtext="Chitu & Jewellery allocations"
            variant="gold"
            isLoading={dataLoading}
          />

          <SummaryCard
            label="Active Investment"
            value={customerMetrics?.activeInvestment}
            icon={TrendingUp}
            subtext="Currently contributing"
            variant="emerald"
            isLoading={dataLoading}
          />

          <SummaryCard
            label="Platform Staff"
            value={staffCount}
            icon={Users}
            subtext="Assigned across branches"
            variant="blue"
            isLoading={dataLoading}
          />
        </div>

        {/* Operating Branches & Quick Staff Roster */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Operating Branches Card */}
          <Card className="bg-white p-6 shadow-xs border border-[#E8E5DD] rounded-2xl space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-stone-100">
              <div className="flex items-center gap-2">
                <Building2 className="w-4 h-4 text-[#0B3D0B]" />
                <h3 className="font-serif-luxury font-bold text-[#151A14] text-base">Active Branches</h3>
              </div>
              <Badge variant="stone" size="sm">
                {branches.length} Registered
              </Badge>
            </div>

            <div className="space-y-2.5">
              {branches.length === 0 && !dataLoading && (
                <p className="text-xs text-stone-400 py-4 text-center">
                  No branch records found.
                </p>
              )}
              {branches.map((b) => (
                <div
                  key={b.id}
                  className="p-3 rounded-xl border border-stone-100 bg-[#FAF9F6] flex items-center justify-between text-xs hover:border-[#E8E5DD] transition-colors"
                >
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-[#0B3D0B]/10 text-[#0B3D0B] font-bold text-[11px] flex items-center justify-center font-mono border border-[#0B3D0B]/20">
                      {b.code}
                    </div>
                    <div>
                      <span className="font-bold text-[#151A14] block">
                        {b.name}
                      </span>
                      <span className="text-stone-500 text-[11px]">{b.city}</span>
                    </div>
                  </div>
                  <Badge variant="success" size="sm">
                    Active
                  </Badge>
                </div>
              ))}
            </div>
          </Card>

          {/* Quick Staff Roster */}
          {(isSuperAdmin || isBranchAdmin) && (
            <Card className="bg-white p-6 shadow-xs border border-[#E8E5DD] rounded-2xl space-y-4">
              <div className="flex items-center justify-between pb-3 border-b border-stone-100">
                <div className="flex items-center gap-2">
                  <ShieldCheck className="w-4 h-4 text-[#0B3D0B]" />
                  <h3 className="font-serif-luxury font-bold text-[#151A14] text-base">Staff Accounts</h3>
                </div>
                <Link
                  href="/users"
                  className="text-xs font-bold text-[#0B3D0B] hover:text-[#124E12] flex items-center gap-1 transition-colors"
                >
                  View All <ArrowRight className="w-3.5 h-3.5 text-[#C5A059]" />
                </Link>
              </div>

              <div className="space-y-2.5">
                {recentUsers.length === 0 && !dataLoading && (
                  <p className="text-xs text-stone-400 py-4 text-center">
                    No staff records found.
                  </p>
                )}
                {recentUsers.map((u) => (
                  <div
                    key={u.id}
                    className="p-2.5 rounded-xl border border-stone-100 bg-[#FAF9F6] flex items-center justify-between text-xs hover:border-[#E8E5DD] transition-colors"
                  >
                    <div className="flex items-center gap-2.5">
                      <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#D4AF37] to-[#8C6D2D] text-[#061E06] font-bold text-[10px] flex items-center justify-center font-mono shadow-xs">
                        {u.fullName
                          .split(" ")
                          .map((n) => n[0])
                          .join("")
                          .substring(0, 2)
                          .toUpperCase()}
                      </div>
                      <div>
                        <span className="font-bold text-[#151A14] block">
                          {u.fullName}
                        </span>
                        <span className="text-stone-500 text-[11px]">
                          {u.email}
                        </span>
                      </div>
                    </div>
                    <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-[#C5A059]/15 text-[#8C6D2D] border border-[#C5A059]/30">
                      {u.roles[0] || "Staff"}
                    </span>
                  </div>
                ))}
              </div>
            </Card>
          )}
        </div>
      </div>
    </AdminLayout>
  );
}
