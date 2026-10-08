"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "../../lib/auth-context";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi } from "../../lib/api";
import { StaffUser, Branch } from "../../types/auth";
import { CustomerMetrics } from "../../types/customer";
import {
  Users,
  Building2,
  ShieldCheck,
  UserPlus,
  UserCheck,
  Layers,
  TrendingUp,
  Clock,
  ArrowRight,
  Activity,
  Gem,
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
    <AdminLayout>
      <div className="space-y-6">
        {/* Welcome Hero Banner */}
        <div className="bg-[#0B3D0B] text-white rounded-2xl p-6 sm:p-8 shadow-sm relative overflow-hidden">
          {/* Subtle gold radial background glow */}
          <div className="absolute top-0 right-0 w-96 h-96 bg-[#FFD700]/10 rounded-full blur-3xl pointer-events-none" />

          <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-6">
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <span className="px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-[#FFD700] text-[#0B3D0B]">
                  {user?.roles?.[0] || "Staff"}
                </span>
                <span className="text-xs text-emerald-200 flex items-center gap-1">
                  <Activity className="w-3.5 h-3.5 text-emerald-300" />
                  Authenticated Session
                </span>
              </div>
              <h1 className="text-2xl sm:text-3xl font-black text-white tracking-tight">
                Welcome back, <span>{user?.fullName}</span>
              </h1>
              <p className="text-sm text-emerald-100/90 max-w-2xl leading-relaxed">
                SJewls Admin Portal allows management of staff accounts, branch operations, customer savings slots, and fine jewellery investments.
              </p>
            </div>

            {/* Quick Actions */}
            <div className="flex flex-wrap items-center gap-3">
              <Link
                href="/customers"
                className="px-4 py-2.5 rounded-xl btn-gold text-xs font-bold flex items-center gap-2 cursor-pointer shadow-sm"
              >
                <UserCheck className="w-4 h-4" />
                <span>View Customers</span>
              </Link>

              {isSuperAdmin && (
                <Link
                  href="/users"
                  className="px-4 py-2.5 rounded-xl bg-white/10 hover:bg-white/20 border border-white/20 text-xs font-bold text-white flex items-center gap-2 transition-all cursor-pointer"
                >
                  <UserPlus className="w-4 h-4" />
                  <span>Manage Staff</span>
                </Link>
              )}
            </div>
          </div>
        </div>

        {/* Stats Grid */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* 1. Total Customers */}
          <div className="bg-white p-5 rounded-2xl border border-gray-200 shadow-xs space-y-2">
            <div className="flex items-center justify-between text-gray-500">
              <span className="text-xs font-bold uppercase tracking-wider">
                Total Customers
              </span>
              <div className="p-2 rounded-xl bg-emerald-50 text-[#0B3D0B]">
                <UserCheck className="w-4 h-4" />
              </div>
            </div>
            <div className="text-2xl font-black text-[#0B3D0B]">
              {customerMetrics !== null ? customerMetrics.totalCustomers : "—"}
            </div>
            <div className="text-xs text-gray-500 flex items-center gap-1">
              <Link
                href="/customers"
                className="text-[#0B3D0B] font-semibold hover:underline flex items-center gap-1"
              >
                Open Customer Directory <ArrowRight className="w-3 h-3" />
              </Link>
            </div>
          </div>

          {/* 2. Total Slots */}
          <div className="bg-white p-5 rounded-2xl border border-gray-200 shadow-xs space-y-2">
            <div className="flex items-center justify-between text-gray-500">
              <span className="text-xs font-bold uppercase tracking-wider">
                Total Slots
              </span>
              <div className="p-2 rounded-xl bg-amber-50 text-amber-700">
                <Layers className="w-4 h-4" />
              </div>
            </div>
            <div className="text-2xl font-black text-gray-900">
              {customerMetrics !== null ? customerMetrics.totalSlots : "—"}
            </div>
            <div className="text-xs text-gray-500">
              Chitu and Jewellery plan slots
            </div>
          </div>

          {/* 3. Active Plans */}
          <div className="bg-white p-5 rounded-2xl border border-gray-200 shadow-xs space-y-2">
            <div className="flex items-center justify-between text-gray-500">
              <span className="text-xs font-bold uppercase tracking-wider">
                Active Investment
              </span>
              <div className="p-2 rounded-xl bg-green-50 text-green-700">
                <TrendingUp className="w-4 h-4" />
              </div>
            </div>
            <div className="text-2xl font-black text-green-700">
              {customerMetrics !== null ? customerMetrics.activeInvestment : "—"}
            </div>
            <div className="text-xs text-gray-500">
              Currently contributing
            </div>
          </div>

          {/* 4. Staff Users */}
          <div className="bg-white p-5 rounded-2xl border border-gray-200 shadow-xs space-y-2">
            <div className="flex items-center justify-between text-gray-500">
              <span className="text-xs font-bold uppercase tracking-wider">
                Platform Staff
              </span>
              <div className="p-2 rounded-xl bg-blue-50 text-blue-700">
                <Users className="w-4 h-4" />
              </div>
            </div>
            <div className="text-2xl font-black text-gray-900">
              {staffCount !== null ? staffCount : "—"}
            </div>
            <div className="text-xs text-gray-500">
              Assigned across branches
            </div>
          </div>
        </div>

        {/* Operating Branches & Quick Overview */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Operating Branches Card */}
          <div className="bg-white p-6 rounded-2xl border border-gray-200 shadow-xs space-y-4">
            <div className="flex items-center justify-between pb-3 border-b border-gray-100">
              <div className="flex items-center gap-2">
                <Building2 className="w-5 h-5 text-[#0B3D0B]" />
                <h3 className="font-bold text-gray-900">Active Branches</h3>
              </div>
              <span className="text-xs font-semibold px-2.5 py-0.5 rounded-full bg-emerald-50 text-[#0B3D0B]">
                {branches.length} Registered
              </span>
            </div>

            <div className="space-y-3">
              {branches.map((b) => (
                <div
                  key={b.id}
                  className="p-3.5 rounded-xl border border-gray-100 bg-gray-50/70 flex items-center justify-between text-xs"
                >
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-emerald-100 text-[#0B3D0B] font-bold text-[11px] flex items-center justify-center">
                      {b.code}
                    </div>
                    <div>
                      <span className="font-bold text-gray-900 block">
                        {b.name}
                      </span>
                      <span className="text-gray-500">{b.city}</span>
                    </div>
                  </div>
                  <span className="font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-0.5 rounded-full">
                    Active
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* Quick Staff Roster */}
          {(isSuperAdmin || isBranchAdmin) && (
            <div className="bg-white p-6 rounded-2xl border border-gray-200 shadow-xs space-y-4">
              <div className="flex items-center justify-between pb-3 border-b border-gray-100">
                <div className="flex items-center gap-2">
                  <ShieldCheck className="w-5 h-5 text-[#0B3D0B]" />
                  <h3 className="font-bold text-gray-900">Staff Accounts</h3>
                </div>
                <Link
                  href="/users"
                  className="text-xs font-bold text-[#0B3D0B] hover:underline flex items-center gap-1"
                >
                  View All <ArrowRight className="w-3.5 h-3.5" />
                </Link>
              </div>

              <div className="space-y-3">
                {recentUsers.map((u) => (
                  <div
                    key={u.id}
                    className="p-3 rounded-xl border border-gray-100 bg-gray-50/70 flex items-center justify-between text-xs"
                  >
                    <div className="flex items-center gap-2.5">
                      <div className="w-8 h-8 rounded-full bg-[#0B3D0B] text-[#FFD700] font-bold text-[10px] flex items-center justify-center">
                        {u.fullName
                          .split(" ")
                          .map((n) => n[0])
                          .join("")
                          .substring(0, 2)
                          .toUpperCase()}
                      </div>
                      <div>
                        <span className="font-bold text-gray-900 block">
                          {u.fullName}
                        </span>
                        <span className="text-gray-500 text-[11px]">
                          {u.email}
                        </span>
                      </div>
                    </div>
                    <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-[#FFD700] text-[#0B3D0B]">
                      {u.roles[0] || "Staff"}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </AdminLayout>
  );
}
