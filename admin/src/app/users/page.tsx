"use client";

import React, { useEffect, useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { useAuth } from "../../lib/auth-context";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, ApiError } from "../../lib/api";
import { StaffUser } from "../../types/auth";
import {
  Users,
  UserPlus,
  Search,
  Filter,
  ShieldCheck,
  Building2,
  Mail,
  Phone,
  RefreshCw,
  AlertCircle,
  CheckCircle2,
  XCircle,
  Power,
  RotateCcw,
  X,
  Shield,
  Info,
} from "lucide-react";

export default function UsersPage() {
  const router = useRouter();
  const { user, isSuperAdmin, isBranchAdmin } = useAuth();

  const [users, setUsers] = useState<StaffUser[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [roleFilter, setRoleFilter] = useState<string>("ALL");

  // State: Deactivation / Activation modal
  const [statusActionUser, setStatusActionUser] = useState<StaffUser | null>(null);
  const [statusReason, setStatusReason] = useState<string>("");
  const [statusReasonError, setStatusReasonError] = useState<string | null>(null);
  const [statusActionLoading, setStatusActionLoading] = useState<boolean>(false);
  const [statusActionError, setStatusActionError] = useState<string | null>(null);

  useEffect(() => {
    if (user && !isSuperAdmin && !isBranchAdmin) {
      router.push("/dashboard");
      return;
    }

    if (user) {
      fetchUsers();
    }
  }, [user, isSuperAdmin, isBranchAdmin, router]);

  const fetchUsers = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.getUsers();
      setUsers(data);
    } catch (err: any) {
      setError(err?.message || "Failed to load staff accounts.");
    } finally {
      setLoading(false);
    }
  };

  // Count active Super Admins to protect the last active Super Admin
  const activeSuperAdminCount = useMemo(() => {
    return users.filter(
      (u) =>
        u.isActive &&
        u.roles?.some((r) => r.toUpperCase() === "SUPER ADMIN")
    ).length;
  }, [users]);

  // Check if current user has permission to manage status for a target staff user
  const getPermissionStatus = (target: StaffUser): { canManage: boolean; reason?: string } => {
    // 1. Cannot deactivate self
    if (user && target.id === user.id) {
      return {
        canManage: false,
        reason: "You cannot deactivate or alter the status of your own account.",
      };
    }

    const targetIsSuperAdmin = target.roles?.some(
      (r) => r.toUpperCase() === "SUPER ADMIN"
    );

    // 2. Only Super Admin can manage Super Admins
    if (targetIsSuperAdmin && !isSuperAdmin) {
      return {
        canManage: false,
        reason: "Only a Super Admin can manage Super Admin accounts.",
      };
    }

    // 3. Last active Super Admin protection
    if (targetIsSuperAdmin && target.isActive && activeSuperAdminCount <= 1) {
      return {
        canManage: false,
        reason: "Cannot deactivate the last active Super Admin on the platform.",
      };
    }

    // 4. Branch Admin can only manage staff in their assigned branch
    if (!isSuperAdmin && isBranchAdmin) {
      const myBranchIds = new Set(user?.assignedBranches?.map((b) => b.branchId) || []);
      const targetHasOverlap = target.assignedBranches?.some((b) =>
        myBranchIds.has(b.branchId)
      );
      if (!targetHasOverlap) {
        return {
          canManage: false,
          reason: "You can only manage staff assigned to your permitted branch.",
        };
      }
    }

    return { canManage: true };
  };

  const filteredUsers = users.filter((u) => {
    const matchesSearch =
      u.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      u.email.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (u.username && u.username.toLowerCase().includes(searchTerm.toLowerCase())) ||
      u.phoneNumber.includes(searchTerm);

    const matchesRole =
      roleFilter === "ALL" ||
      u.roles?.some((r) => r.toLowerCase() === roleFilter.toLowerCase());

    return matchesSearch && matchesRole;
  });

  // Open status modal
  const handleOpenStatusModal = (target: StaffUser) => {
    setStatusActionUser(target);
    setStatusReason("");
    setStatusReasonError(null);
    setStatusActionError(null);
  };

  // Submit status update
  const handleStatusSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!statusActionUser) return;

    const isDeactivating = statusActionUser.isActive;

    if (isDeactivating && !statusReason.trim()) {
      setStatusReasonError("A deactivation reason is required for security and audit compliance.");
      return;
    }

    setStatusActionLoading(true);
    setStatusActionError(null);

    try {
      await adminApi.updateUserStatus(statusActionUser.id, {
        isActive: !isDeactivating,
        reason: statusReason.trim() || undefined,
      });

      const staffName = statusActionUser.fullName;
      setStatusActionUser(null);
      setSuccessMessage(
        isDeactivating
          ? `Staff account for "${staffName}" has been deactivated. Active sessions and access tokens were revoked immediately.`
          : `Staff account for "${staffName}" has been reactivated successfully.`
      );
      setTimeout(() => setSuccessMessage(null), 6000);

      fetchUsers();
    } catch (err: any) {
      const msg =
        err instanceof ApiError
          ? err.message
          : err?.message || "Failed to update staff status.";
      setStatusActionError(msg);
    } finally {
      setStatusActionLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-6">
        {/* Header Bar */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-gray-200">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl sm:text-3xl font-extrabold text-[#0B3D0B] tracking-tight">
                Staff & User Management
              </h1>
            </div>
            <p className="text-sm text-gray-600 mt-1">
              Manage platform staff roles, branch assignments, and system access permissions.
            </p>
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={fetchUsers}
              disabled={loading}
              className="p-2.5 rounded-xl border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 transition-colors cursor-pointer shadow-2xs disabled:opacity-50"
              title="Refresh staff list"
            >
              <RefreshCw
                className={`w-4 h-4 text-[#0B3D0B] ${
                  loading ? "animate-spin" : ""
                }`}
              />
            </button>

            {isSuperAdmin && (
              <Link
                href="/users/create"
                className="px-4 py-2.5 rounded-xl btn-primary-green text-xs font-bold flex items-center gap-2 cursor-pointer shadow-sm"
              >
                <UserPlus className="w-4 h-4 text-[#FFD700]" />
                <span>Create Staff User</span>
              </Link>
            )}
          </div>
        </div>

        {/* Success Alert Banner */}
        {successMessage && (
          <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 flex items-start gap-3 transition-all animate-fadeIn">
            <CheckCircle2 className="w-5 h-5 text-emerald-600 flex-shrink-0 mt-0.5" />
            <div className="flex-1 text-xs font-semibold text-emerald-900">
              {successMessage}
            </div>
            <button
              onClick={() => setSuccessMessage(null)}
              className="text-emerald-500 hover:text-emerald-700 text-xs cursor-pointer"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Filter and Search Bar */}
        <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-xs flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="relative w-full md:max-w-md">
            <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Search by name, email, username or phone..."
              className="w-full pl-10 pr-4 py-2 rounded-xl border border-gray-300 text-sm text-gray-900 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
            />
          </div>

          <div className="flex items-center gap-3 w-full md:w-auto">
            <Filter className="w-4 h-4 text-gray-400 hidden sm:block" />
            <select
              value={roleFilter}
              onChange={(e) => setRoleFilter(e.target.value)}
              className="w-full sm:w-44 px-3 py-2 rounded-xl border border-gray-300 text-sm text-gray-800 bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] cursor-pointer"
            >
              <option value="ALL">All Roles</option>
              <option value="SUPER ADMIN">Super Admin</option>
              <option value="BRANCH ADMIN">Branch Admin</option>
              <option value="STAFF">Staff</option>
            </select>
          </div>
        </div>

        {/* Error Alert */}
        {error && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 flex items-start gap-3">
            <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
            <div className="text-sm text-red-700">
              <span className="font-bold">Error:</span> {error}
            </div>
          </div>
        )}

        {/* Users Table */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-xs overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-gray-50/80 border-b border-gray-200 text-[11px] font-bold text-gray-500 uppercase tracking-wider">
                  <th className="py-3.5 px-6">User</th>
                  <th className="py-3.5 px-4">Contact</th>
                  <th className="py-3.5 px-4">Role</th>
                  <th className="py-3.5 px-4">Assigned Branch</th>
                  <th className="py-3.5 px-4">Status</th>
                  <th className="py-3.5 px-4">Created</th>
                  <th className="py-3.5 px-6 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-sm">
                {loading ? (
                  <>
                    {[1, 2, 3].map((i) => (
                      <tr key={i} className="animate-pulse">
                        <td className="py-4 px-6">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-full bg-gray-200" />
                            <div className="space-y-1.5">
                              <div className="w-28 h-3.5 bg-gray-200 rounded" />
                              <div className="w-20 h-2.5 bg-gray-100 rounded" />
                            </div>
                          </div>
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-32 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-20 h-5 bg-gray-200 rounded-full" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-24 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-16 h-5 bg-gray-200 rounded-full" />
                        </td>
                        <td className="py-4 px-4">
                          <div className="w-16 h-3 bg-gray-200 rounded" />
                        </td>
                        <td className="py-4 px-6 text-right">
                          <div className="w-20 h-7 bg-gray-200 rounded-lg ml-auto" />
                        </td>
                      </tr>
                    ))}
                  </>
                ) : filteredUsers.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-12 px-4 text-center">
                      <div className="max-w-sm mx-auto flex flex-col items-center">
                        <Users className="w-10 h-10 text-gray-300 mb-2" />
                        <h3 className="text-sm font-bold text-gray-900">
                          No staff accounts found
                        </h3>
                        <p className="text-xs text-gray-500 mt-1">
                          No users matched your search criteria.
                        </p>
                      </div>
                    </td>
                  </tr>
                ) : (
                  filteredUsers.map((u) => {
                    const initials = u.fullName
                      ? u.fullName
                          .split(" ")
                          .map((n) => n[0])
                          .join("")
                          .substring(0, 2)
                          .toUpperCase()
                      : "ST";

                    const { canManage, reason: disabledReason } = getPermissionStatus(u);
                    const isSelf = user?.id === u.id;

                    return (
                      <tr
                        key={u.id}
                        className="hover:bg-gray-50/70 transition-colors"
                      >
                        {/* User Identity */}
                        <td className="py-3.5 px-6">
                          <div className="flex items-center gap-3">
                            <div className="w-9 h-9 rounded-full bg-[#0B3D0B] text-[#FFD700] font-bold text-xs flex items-center justify-center flex-shrink-0 shadow-2xs">
                              {initials}
                            </div>
                            <div className="flex flex-col min-w-0">
                              <div className="flex items-center gap-1.5">
                                <span className="font-bold text-gray-900 truncate">
                                  {u.fullName}
                                </span>
                                {isSelf && (
                                  <span className="px-1.5 py-0.2 rounded text-[10px] font-bold bg-[#FFD700]/30 text-[#0B3D0B]">
                                    You
                                  </span>
                                )}
                              </div>
                              {u.username && (
                                <span className="text-[11px] text-gray-500">
                                  @{u.username}
                                </span>
                              )}
                            </div>
                          </div>
                        </td>

                        {/* Contact */}
                        <td className="py-3.5 px-4 text-xs text-gray-600">
                          <div className="space-y-0.5">
                            <div className="flex items-center gap-1.5 truncate">
                              <Mail className="w-3.5 h-3.5 text-gray-400" />
                              <span className="truncate">{u.email}</span>
                            </div>
                            <div className="flex items-center gap-1.5 font-mono text-[11px] text-gray-500">
                              <Phone className="w-3.5 h-3.5 text-gray-400" />
                              <span>{u.phoneNumber}</span>
                            </div>
                          </div>
                        </td>

                        {/* Role */}
                        <td className="py-3.5 px-4">
                          <div className="flex flex-wrap gap-1">
                            {u.roles?.map((r) => (
                              <span
                                key={r}
                                className="inline-flex items-center gap-1 text-[11px] font-bold px-2.5 py-0.5 rounded-full bg-[#FFD700]/25 text-[#0B3D0B]"
                              >
                                <ShieldCheck className="w-3 h-3 text-[#0B3D0B]" />
                                {r}
                              </span>
                            ))}
                          </div>
                        </td>

                        {/* Branch */}
                        <td className="py-3.5 px-4 text-xs text-gray-700">
                          {(() => {
                            const uniqueBranches = Array.from(
                              new Map(
                                (u.assignedBranches || [])
                                  .filter((b) => b && b.branchId)
                                  .map((b) => [b.branchId, b])
                              ).values()
                            );

                            return uniqueBranches.length > 0 ? (
                              <div className="flex flex-wrap gap-1.5">
                                {uniqueBranches.map((b) => (
                                  <span
                                    key={b.branchId}
                                    className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-gray-100 text-gray-700 text-[11px]"
                                  >
                                    <Building2 className="w-3 h-3 text-[#0B3D0B]" />
                                    {b.name}
                                  </span>
                                ))}
                              </div>
                            ) : (
                              <span className="text-gray-400 italic">
                                All Branches (Super)
                              </span>
                            );
                          })()}
                        </td>

                        {/* Status */}
                        <td className="py-3.5 px-4">
                          {u.isActive ? (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-green-100 text-green-800">
                              <span className="w-1.5 h-1.5 rounded-full bg-green-600" />
                              Active
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-red-100 text-red-800">
                              <span className="w-1.5 h-1.5 rounded-full bg-red-600" />
                              Disabled
                            </span>
                          )}
                        </td>

                        {/* Created Date */}
                        <td className="py-3.5 px-4 text-xs text-gray-500">
                          {new Date(u.createdAtUtc).toLocaleDateString()}
                        </td>

                        {/* Actions */}
                        <td className="py-3.5 px-6 text-right">
                          {canManage ? (
                            <button
                              onClick={() => handleOpenStatusModal(u)}
                              className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer border ${
                                u.isActive
                                  ? "text-red-700 bg-red-50 hover:bg-red-600 hover:text-white border-red-200"
                                  : "text-emerald-700 bg-emerald-50 hover:bg-emerald-700 hover:text-white border-emerald-200"
                              }`}
                              title={
                                u.isActive
                                  ? "Deactivate staff account"
                                  : "Reactivate staff account"
                              }
                            >
                              <Power className="w-3.5 h-3.5" />
                              <span>{u.isActive ? "Deactivate" : "Activate"}</span>
                            </button>
                          ) : (
                            <span
                              className="text-xs text-gray-400 cursor-not-allowed italic"
                              title={disabledReason || "Permission restricted"}
                            >
                              {isSelf ? "Self Account" : "Restricted"}
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* ================= STAFF ACTIVATE / DEACTIVATE MODAL ================= */}
        {statusActionUser && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
            <div
              className="fixed inset-0 bg-black/50 backdrop-blur-xs transition-opacity"
              onClick={() => !statusActionLoading && setStatusActionUser(null)}
            />

            <div className="relative w-full max-w-md bg-white rounded-2xl shadow-2xl border border-gray-200 overflow-hidden z-50 flex flex-col">
              {/* Header */}
              <div
                className={`px-6 py-4 text-white flex items-center justify-between ${
                  statusActionUser.isActive ? "bg-red-800" : "bg-[#0B3D0B]"
                }`}
              >
                <div className="flex items-center gap-2">
                  <Shield className="w-5 h-5 text-[#FFD700]" />
                  <h2 className="text-base font-bold tracking-tight">
                    {statusActionUser.isActive
                      ? "Deactivate Staff Account"
                      : "Reactivate Staff Account"}
                  </h2>
                </div>
                <button
                  onClick={() => !statusActionLoading && setStatusActionUser(null)}
                  className="p-1 rounded-lg hover:bg-white/10 transition-colors cursor-pointer"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Form Body */}
              <form onSubmit={handleStatusSubmit} className="p-6 space-y-4">
                {statusActionError && (
                  <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 flex items-start gap-2 text-xs text-red-700">
                    <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0 mt-0.5" />
                    <span>{statusActionError}</span>
                  </div>
                )}

                <div className="space-y-1">
                  <p className="text-xs text-gray-700">
                    Target account:
                  </p>
                  <div className="p-3 bg-gray-50 rounded-xl border border-gray-200 text-xs">
                    <div className="font-bold text-gray-900 text-sm">
                      {statusActionUser.fullName}
                    </div>
                    <div className="text-gray-500 mt-0.5">
                      {statusActionUser.email} • Role: {statusActionUser.roles?.join(", ") || "Staff"}
                    </div>
                  </div>
                </div>

                {statusActionUser.isActive ? (
                  <>
                    <div className="p-3 rounded-xl bg-amber-50 border border-amber-200 text-[11px] text-amber-900 leading-relaxed">
                      <strong>Access Revocation:</strong> Deactivating will immediately invalidate all active sessions, refresh tokens, and JWT bearer tokens. The staff member will be immediately barred from the portal. Staff profile and audit records will be retained.
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-gray-800 mb-1">
                        Reason for Deactivation <span className="text-red-600">*</span>
                      </label>
                      <textarea
                        rows={2}
                        value={statusReason}
                        onChange={(e) => {
                          setStatusReason(e.target.value);
                          if (statusReasonError) setStatusReasonError(null);
                        }}
                        placeholder="Provide reason for audit logging (e.g. Employee departure, temporary suspension, security review)..."
                        className={`w-full px-3 py-2 rounded-xl border text-xs text-gray-900 focus:outline-none focus:ring-2 focus:ring-red-600 ${
                          statusReasonError ? "border-red-400 bg-red-50/30" : "border-gray-300"
                        }`}
                      />
                      {statusReasonError && (
                        <p className="text-[11px] text-red-600 mt-1">
                          {statusReasonError}
                        </p>
                      )}
                    </div>
                  </>
                ) : (
                  <>
                    <div className="p-3 rounded-xl bg-emerald-50 border border-emerald-200 text-[11px] text-emerald-900 leading-relaxed">
                      <strong>Reactivation Notice:</strong> Reactivating this staff account will allow the user to authenticate again with their existing credentials and perform authorized administration actions.
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-gray-800 mb-1">
                        Remarks or Notes (Optional)
                      </label>
                      <input
                        type="text"
                        value={statusReason}
                        onChange={(e) => setStatusReason(e.target.value)}
                        placeholder="e.g. Access reinstated by Super Admin"
                        className="w-full px-3 py-2 rounded-xl border border-gray-300 text-xs text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                      />
                    </div>
                  </>
                )}

                {/* Footer Buttons */}
                <div className="pt-3 flex items-center justify-end gap-3 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setStatusActionUser(null)}
                    disabled={statusActionLoading}
                    className="px-4 py-2 rounded-xl text-xs font-bold text-gray-700 bg-white border border-gray-300 hover:bg-gray-100 transition-colors cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={statusActionLoading}
                    className={`px-5 py-2 rounded-xl text-xs font-bold text-white flex items-center gap-2 cursor-pointer disabled:opacity-50 shadow-sm ${
                      statusActionUser.isActive
                        ? "bg-red-700 hover:bg-red-800"
                        : "btn-primary-green"
                    }`}
                  >
                    {statusActionLoading ? (
                      <>
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                        <span>Updating...</span>
                      </>
                    ) : statusActionUser.isActive ? (
                      <>
                        <Power className="w-3.5 h-3.5" />
                        <span>Confirm Deactivation</span>
                      </>
                    ) : (
                      <>
                        <RotateCcw className="w-3.5 h-3.5" />
                        <span>Reactivate Account</span>
                      </>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
