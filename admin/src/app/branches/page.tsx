"use client";

import React, { useEffect, useState, useMemo } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../lib/auth-context";
import { AdminLayout } from "../../components/AdminLayout";
import { adminApi, ApiError } from "../../lib/api";
import { Branch, CreateBranchRequest, UpdateBranchRequest } from "../../types/branch";
import {
  Building2,
  Plus,
  Search,
  Filter,
  CheckCircle2,
  AlertCircle,
  XCircle,
  Edit2,
  Trash2,
  Power,
  RotateCcw,
  RefreshCw,
  Users,
  UserCheck,
  MapPin,
  Globe,
  Clock,
  X,
  ShieldAlert,
} from "lucide-react";

export default function BranchesPage() {
  const router = useRouter();
  const { user, isSuperAdmin, isBranchAdmin } = useAuth();

  // State: Branches data
  const [branches, setBranches] = useState<Branch[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // State: Search & Filters
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<string>("ALL"); // 'ALL', 'ACTIVE', 'INACTIVE'

  // State: Create Branch Modal
  const [showCreateModal, setShowCreateModal] = useState<boolean>(false);
  const [createForm, setCreateForm] = useState<CreateBranchRequest>({
    code: "",
    name: "",
    address: "",
    city: "Jaffna",
    country: "Sri Lanka",
    currency: "LKR",
    timezone: "Asia/Colombo",
    isActive: true,
  });
  const [createErrors, setCreateErrors] = useState<Record<string, string>>({});
  const [createLoading, setCreateLoading] = useState<boolean>(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // State: Edit Branch Modal
  const [editingBranch, setEditingBranch] = useState<Branch | null>(null);
  const [editForm, setEditForm] = useState<UpdateBranchRequest>({
    name: "",
    address: "",
    city: "",
    country: "Sri Lanka",
    currency: "LKR",
    timezone: "Asia/Colombo",
    isActive: true,
  });
  const [editErrors, setEditErrors] = useState<Record<string, string>>({});
  const [editLoading, setEditLoading] = useState<boolean>(false);
  const [editError, setEditError] = useState<string | null>(null);

  // State: Status Change Confirmation Modal
  const [statusActionBranch, setStatusActionBranch] = useState<Branch | null>(null);
  const [statusLoading, setStatusLoading] = useState<boolean>(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  // State: Delete Confirmation Modal
  const [deleteActionBranch, setDeleteActionBranch] = useState<Branch | null>(null);
  const [deleteLoading, setDeleteLoading] = useState<boolean>(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      loadBranches();
    }
  }, [user]);

  const loadBranches = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await adminApi.getAdminBranches();
      setBranches(data);
    } catch (err: any) {
      setError(err?.message || "Failed to load branches.");
    } finally {
      setLoading(false);
    }
  };

  // Filtered branches
  const filteredBranches = useMemo(() => {
    return branches.filter((b) => {
      const matchesSearch =
        searchTerm === "" ||
        b.code.toLowerCase().includes(searchTerm.toLowerCase()) ||
        b.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        b.city.toLowerCase().includes(searchTerm.toLowerCase()) ||
        b.address.toLowerCase().includes(searchTerm.toLowerCase());

      const matchesStatus =
        statusFilter === "ALL" ||
        (statusFilter === "ACTIVE" && b.isActive) ||
        (statusFilter === "INACTIVE" && !b.isActive);

      return matchesSearch && matchesStatus;
    });
  }, [branches, searchTerm, statusFilter]);

  // Statistics
  const stats = useMemo(() => {
    const total = branches.length;
    const active = branches.filter((b) => b.isActive).length;
    const inactive = total - active;
    const totalStaff = branches.reduce((acc, b) => acc + (b.assignedStaffCount || 0), 0);
    const totalCustomers = branches.reduce((acc, b) => acc + (b.customerCount || 0), 0);
    return { total, active, inactive, totalStaff, totalCustomers };
  }, [branches]);

  // Handle Create Submit
  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const errors: Record<string, string> = {};

    if (!createForm.code.trim()) errors.code = "Branch code is required (e.g. COL-01).";
    if (!createForm.name.trim()) errors.name = "Branch name is required.";
    if (!createForm.address.trim()) errors.address = "Address is required.";

    if (Object.keys(errors).length > 0) {
      setCreateErrors(errors);
      return;
    }

    setCreateLoading(true);
    setCreateError(null);
    try {
      const newBranch = await adminApi.createBranch({
        ...createForm,
        code: createForm.code.trim().toUpperCase(),
        name: createForm.name.trim(),
        address: createForm.address.trim(),
      });
      setSuccessMessage(`Branch '${newBranch.name}' (${newBranch.code}) created successfully.`);
      setShowCreateModal(false);
      setCreateForm({
        code: "",
        name: "",
        address: "",
        city: "Jaffna",
        country: "Sri Lanka",
        currency: "LKR",
        timezone: "Asia/Colombo",
        isActive: true,
      });
      loadBranches();
    } catch (err: any) {
      setCreateError(err?.message || "Failed to create branch.");
    } finally {
      setCreateLoading(false);
    }
  };

  // Open Edit Modal
  const openEditModal = (branch: Branch) => {
    setEditingBranch(branch);
    setEditForm({
      name: branch.name,
      address: branch.address,
      city: branch.city,
      country: branch.country,
      currency: branch.currency,
      timezone: branch.timezone,
      isActive: branch.isActive,
    });
    setEditErrors({});
    setEditError(null);
  };

  // Handle Edit Submit
  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingBranch) return;

    const errors: Record<string, string> = {};
    if (!editForm.name.trim()) errors.name = "Branch name is required.";
    if (!editForm.address.trim()) errors.address = "Address is required.";

    if (Object.keys(errors).length > 0) {
      setEditErrors(errors);
      return;
    }

    setEditLoading(true);
    setEditError(null);
    try {
      const updated = await adminApi.updateBranch(editingBranch.id, {
        ...editForm,
        name: editForm.name.trim(),
        address: editForm.address.trim(),
      });
      setSuccessMessage(`Branch '${updated.name}' updated successfully.`);
      setEditingBranch(null);
      loadBranches();
    } catch (err: any) {
      setEditError(err?.message || "Failed to update branch.");
    } finally {
      setEditLoading(false);
    }
  };

  // Handle Status Toggle Submit
  const handleStatusToggleSubmit = async () => {
    if (!statusActionBranch) return;
    setStatusLoading(true);
    setStatusError(null);
    try {
      const nextStatus = !statusActionBranch.isActive;
      const updated = await adminApi.updateBranchStatus(statusActionBranch.id, nextStatus);
      setSuccessMessage(
        `Branch '${updated.name}' has been ${nextStatus ? "activated" : "deactivated"} successfully.`
      );
      setStatusActionBranch(null);
      loadBranches();
    } catch (err: any) {
      setStatusError(err?.message || "Failed to update branch status.");
    } finally {
      setStatusLoading(false);
    }
  };

  // Handle Delete Submit
  const handleDeleteSubmit = async () => {
    if (!deleteActionBranch) return;
    setDeleteLoading(true);
    setDeleteError(null);
    try {
      await adminApi.deleteBranch(deleteActionBranch.id);
      setSuccessMessage(`Branch '${deleteActionBranch.name}' deleted successfully.`);
      setDeleteActionBranch(null);
      loadBranches();
    } catch (err: any) {
      setDeleteError(err?.message || "Failed to delete branch.");
    } finally {
      setDeleteLoading(false);
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-6">
        {/* Page Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold text-[#0B3D0B] tracking-tight">Branches Management</h1>
            <p className="text-sm text-gray-500 mt-1">
              Configure and oversee physical store branches, currency, and local operations.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={loadBranches}
              disabled={loading}
              className="inline-flex items-center gap-2 px-3 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 shadow-sm transition"
              title="Refresh branches list"
            >
              <RefreshCw className={`w-4 h-4 ${loading ? "animate-spin text-[#0B3D0B]" : ""}`} />
              Refresh
            </button>
            {isSuperAdmin && (
              <button
                onClick={() => {
                  setCreateErrors({});
                  setCreateError(null);
                  setShowCreateModal(true);
                }}
                className="inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold text-white bg-[#0B3D0B] rounded-lg hover:bg-[#082d08] shadow-sm transition"
              >
                <Plus className="w-4 h-4" />
                Add New Branch
              </button>
            )}
          </div>
        </div>

        {/* Global Alerts */}
        {successMessage && (
          <div className="p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-xl flex items-center justify-between shadow-sm">
            <div className="flex items-center gap-2">
              <CheckCircle2 className="w-5 h-5 text-emerald-600 flex-shrink-0" />
              <span className="text-sm font-medium">{successMessage}</span>
            </div>
            <button onClick={() => setSuccessMessage(null)} className="text-emerald-500 hover:text-emerald-700">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {error && (
          <div className="p-4 bg-red-50 border border-red-200 text-red-800 rounded-xl flex items-center justify-between shadow-sm">
            <div className="flex items-center gap-2">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <span className="text-sm font-medium">{error}</span>
            </div>
            <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Summary Stats Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Total Branches</p>
              <p className="text-2xl font-bold text-[#0B3D0B] mt-1">{stats.total}</p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-emerald-50 border border-emerald-100 flex items-center justify-center text-[#0B3D0B]">
              <Building2 className="w-6 h-6" />
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-emerald-600 uppercase tracking-wider">Active Stores</p>
              <p className="text-2xl font-bold text-emerald-700 mt-1">{stats.active}</p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-emerald-50 border border-emerald-100 flex items-center justify-center text-emerald-600">
              <CheckCircle2 className="w-6 h-6" />
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-amber-600 uppercase tracking-wider">Inactive Stores</p>
              <p className="text-2xl font-bold text-amber-700 mt-1">{stats.inactive}</p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-amber-50 border border-amber-100 flex items-center justify-center text-amber-600">
              <AlertCircle className="w-6 h-6" />
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-blue-600 uppercase tracking-wider">Staff Assigned</p>
              <p className="text-2xl font-bold text-blue-700 mt-1">{stats.totalStaff}</p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-blue-50 border border-blue-100 flex items-center justify-center text-blue-600">
              <Users className="w-6 h-6" />
            </div>
          </div>
        </div>

        {/* Filter and Search Bar */}
        <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="relative w-full md:w-96">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input
              type="text"
              placeholder="Search by code, name, city, address..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full pl-9 pr-4 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:border-transparent"
            />
          </div>

          <div className="flex items-center gap-3 w-full md:w-auto">
            <div className="flex items-center gap-2">
              <Filter className="w-4 h-4 text-gray-400" />
              <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">Status:</span>
            </div>
            <div className="inline-flex rounded-lg border border-gray-200 p-1 bg-gray-50">
              <button
                onClick={() => setStatusFilter("ALL")}
                className={`px-3 py-1 text-xs font-medium rounded-md transition ${
                  statusFilter === "ALL" ? "bg-white text-[#0B3D0B] shadow-sm font-semibold" : "text-gray-600 hover:text-gray-900"
                }`}
              >
                All ({branches.length})
              </button>
              <button
                onClick={() => setStatusFilter("ACTIVE")}
                className={`px-3 py-1 text-xs font-medium rounded-md transition ${
                  statusFilter === "ACTIVE" ? "bg-white text-emerald-700 shadow-sm font-semibold" : "text-gray-600 hover:text-gray-900"
                }`}
              >
                Active ({stats.active})
              </button>
              <button
                onClick={() => setStatusFilter("INACTIVE")}
                className={`px-3 py-1 text-xs font-medium rounded-md transition ${
                  statusFilter === "INACTIVE" ? "bg-white text-amber-700 shadow-sm font-semibold" : "text-gray-600 hover:text-gray-900"
                }`}
              >
                Inactive ({stats.inactive})
              </button>
            </div>
          </div>
        </div>

        {/* Branches Table */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-gray-50/75 border-b border-gray-200 text-xs font-semibold text-gray-500 uppercase tracking-wider">
                  <th className="py-3.5 px-4">Code</th>
                  <th className="py-3.5 px-4">Branch Name & City</th>
                  <th className="py-3.5 px-4">Address</th>
                  <th className="py-3.5 px-4">Currency & TZ</th>
                  <th className="py-3.5 px-4 text-center">Staff</th>
                  <th className="py-3.5 px-4 text-center">Customers</th>
                  <th className="py-3.5 px-4">Status</th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 text-sm">
                {loading ? (
                  <tr>
                    <td colSpan={8} className="py-12 text-center text-gray-500">
                      <div className="flex flex-col items-center justify-center gap-2">
                        <RefreshCw className="w-6 h-6 animate-spin text-[#0B3D0B]" />
                        <span>Loading branches...</span>
                      </div>
                    </td>
                  </tr>
                ) : filteredBranches.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="py-12 text-center text-gray-500">
                      <div className="flex flex-col items-center justify-center gap-2">
                        <Building2 className="w-8 h-8 text-gray-300" />
                        <span className="font-medium text-gray-700">No branches found</span>
                        <span className="text-xs text-gray-400">
                          {searchTerm ? "Try adjusting your search criteria" : "Click 'Add New Branch' to create your first store."}
                        </span>
                      </div>
                    </td>
                  </tr>
                ) : (
                  filteredBranches.map((branch) => (
                    <tr key={branch.id} className="hover:bg-gray-50/60 transition">
                      <td className="py-3.5 px-4">
                        <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-bold font-mono bg-emerald-50 text-[#0B3D0B] border border-emerald-200">
                          {branch.code}
                        </span>
                      </td>
                      <td className="py-3.5 px-4">
                        <div className="font-semibold text-gray-900">{branch.name}</div>
                        <div className="text-xs text-gray-500 flex items-center gap-1 mt-0.5">
                          <MapPin className="w-3 h-3 text-gray-400" />
                          <span>{branch.city}, {branch.country}</span>
                        </div>
                      </td>
                      <td className="py-3.5 px-4 text-gray-600 max-w-xs truncate" title={branch.address}>
                        {branch.address}
                      </td>
                      <td className="py-3.5 px-4">
                        <div className="text-xs font-semibold text-gray-800">{branch.currency}</div>
                        <div className="text-xs text-gray-400 flex items-center gap-1 mt-0.5">
                          <Clock className="w-3 h-3 text-gray-400" />
                          <span>{branch.timezone}</span>
                        </div>
                      </td>
                      <td className="py-3.5 px-4 text-center">
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700 border border-blue-150">
                          <Users className="w-3 h-3" />
                          {branch.assignedStaffCount ?? 0}
                        </span>
                      </td>
                      <td className="py-3.5 px-4 text-center">
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-purple-50 text-purple-700 border border-purple-150">
                          <UserCheck className="w-3 h-3" />
                          {branch.customerCount ?? 0}
                        </span>
                      </td>
                      <td className="py-3.5 px-4">
                        {branch.isActive ? (
                          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                            Active
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-600 border border-gray-200">
                            <span className="w-1.5 h-1.5 rounded-full bg-gray-400"></span>
                            Inactive
                          </span>
                        )}
                      </td>
                      <td className="py-3.5 px-4 text-right">
                        <div className="flex items-center justify-end gap-1.5">
                          {isSuperAdmin && (
                            <>
                              <button
                                onClick={() => openEditModal(branch)}
                                className="p-1.5 text-gray-500 hover:text-[#0B3D0B] hover:bg-emerald-50 rounded-lg transition"
                                title="Edit Branch"
                              >
                                <Edit2 className="w-4 h-4" />
                              </button>
                              <button
                                onClick={() => {
                                  setStatusActionBranch(branch);
                                  setStatusError(null);
                                }}
                                className={`p-1.5 rounded-lg transition ${
                                  branch.isActive
                                    ? "text-amber-600 hover:text-amber-800 hover:bg-amber-50"
                                    : "text-emerald-600 hover:text-emerald-800 hover:bg-emerald-50"
                                }`}
                                title={branch.isActive ? "Deactivate Branch" : "Activate Branch"}
                              >
                                {branch.isActive ? <Power className="w-4 h-4" /> : <RotateCcw className="w-4 h-4" />}
                              </button>
                              <button
                                onClick={() => {
                                  setDeleteActionBranch(branch);
                                  setDeleteError(null);
                                }}
                                className="p-1.5 text-gray-400 hover:text-red-600 hover:bg-red-50 rounded-lg transition"
                                title="Delete Branch"
                              >
                                <Trash2 className="w-4 h-4" />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* CREATE BRANCH MODAL */}
        {showCreateModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
            <div className="bg-white rounded-2xl shadow-xl border border-gray-100 max-w-lg w-full overflow-hidden">
              <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between bg-gray-50/50">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-lg bg-emerald-100 text-[#0B3D0B] flex items-center justify-center">
                    <Building2 className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="font-bold text-gray-900">Add New Branch</h3>
                    <p className="text-xs text-gray-500">Register a new store location to SJewls</p>
                  </div>
                </div>
                <button
                  onClick={() => setShowCreateModal(false)}
                  className="text-gray-400 hover:text-gray-600 p-1 rounded-lg hover:bg-gray-100"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleCreateSubmit} className="p-6 space-y-4">
                {createError && (
                  <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 flex-shrink-0" />
                    <span>{createError}</span>
                  </div>
                )}

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">
                      Branch Code <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      placeholder="e.g. COL-01"
                      value={createForm.code}
                      onChange={(e) => setCreateForm({ ...createForm, code: e.target.value.toUpperCase() })}
                      className="w-full px-3 py-2 text-sm font-mono border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                    {createErrors.code && <p className="text-xs text-red-600 mt-1">{createErrors.code}</p>}
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">
                      Branch Name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      placeholder="e.g. Colombo Flagship"
                      value={createForm.name}
                      onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                    {createErrors.name && <p className="text-xs text-red-600 mt-1">{createErrors.name}</p>}
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">
                    Store Address <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    placeholder="e.g. 45 Galle Road, Kollupitiya"
                    value={createForm.address}
                    onChange={(e) => setCreateForm({ ...createForm, address: e.target.value })}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  />
                  {createErrors.address && <p className="text-xs text-red-600 mt-1">{createErrors.address}</p>}
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">City</label>
                    <input
                      type="text"
                      placeholder="e.g. Colombo"
                      value={createForm.city}
                      onChange={(e) => setCreateForm({ ...createForm, city: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">Country</label>
                    <input
                      type="text"
                      value={createForm.country}
                      onChange={(e) => setCreateForm({ ...createForm, country: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">Currency</label>
                    <input
                      type="text"
                      value={createForm.currency}
                      onChange={(e) => setCreateForm({ ...createForm, currency: e.target.value.toUpperCase() })}
                      className="w-full px-3 py-2 text-sm font-mono border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">Timezone</label>
                    <input
                      type="text"
                      value={createForm.timezone}
                      onChange={(e) => setCreateForm({ ...createForm, timezone: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>
                </div>

                <div className="flex items-center gap-2 pt-2">
                  <input
                    type="checkbox"
                    id="createIsActive"
                    checked={createForm.isActive}
                    onChange={(e) => setCreateForm({ ...createForm, isActive: e.target.checked })}
                    className="w-4 h-4 rounded text-[#0B3D0B] focus:ring-[#0B3D0B] border-gray-300"
                  />
                  <label htmlFor="createIsActive" className="text-sm font-medium text-gray-700">
                    Immediately activate this branch for customer registrations
                  </label>
                </div>

                <div className="flex items-center justify-end gap-3 pt-4 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setShowCreateModal(false)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={createLoading}
                    className="inline-flex items-center gap-2 px-5 py-2 text-sm font-semibold text-white bg-[#0B3D0B] rounded-lg hover:bg-[#082d08] shadow-sm disabled:opacity-50"
                  >
                    {createLoading && <RefreshCw className="w-4 h-4 animate-spin" />}
                    Create Branch
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* EDIT BRANCH MODAL */}
        {editingBranch && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
            <div className="bg-white rounded-2xl shadow-xl border border-gray-100 max-w-lg w-full overflow-hidden">
              <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between bg-gray-50/50">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-lg bg-emerald-100 text-[#0B3D0B] flex items-center justify-center">
                    <Edit2 className="w-4 h-4" />
                  </div>
                  <div>
                    <h3 className="font-bold text-gray-900">Edit Branch</h3>
                    <p className="text-xs text-gray-500 font-mono">Code: {editingBranch.code}</p>
                  </div>
                </div>
                <button
                  onClick={() => setEditingBranch(null)}
                  className="text-gray-400 hover:text-gray-600 p-1 rounded-lg hover:bg-gray-100"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleEditSubmit} className="p-6 space-y-4">
                {editError && (
                  <div className="p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 flex-shrink-0" />
                    <span>{editError}</span>
                  </div>
                )}

                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">
                    Branch Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={editForm.name}
                    onChange={(e) => setEditForm({ ...editForm, name: e.target.value })}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  />
                  {editErrors.name && <p className="text-xs text-red-600 mt-1">{editErrors.name}</p>}
                </div>

                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">
                    Store Address <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={editForm.address}
                    onChange={(e) => setEditForm({ ...editForm, address: e.target.value })}
                    className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  />
                  {editErrors.address && <p className="text-xs text-red-600 mt-1">{editErrors.address}</p>}
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">City</label>
                    <input
                      type="text"
                      value={editForm.city}
                      onChange={(e) => setEditForm({ ...editForm, city: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">Country</label>
                    <input
                      type="text"
                      value={editForm.country}
                      onChange={(e) => setEditForm({ ...editForm, country: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">Currency</label>
                    <input
                      type="text"
                      value={editForm.currency}
                      onChange={(e) => setEditForm({ ...editForm, currency: e.target.value.toUpperCase() })}
                      className="w-full px-3 py-2 text-sm font-mono border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-semibold text-gray-700 mb-1">Timezone</label>
                    <input
                      type="text"
                      value={editForm.timezone}
                      onChange={(e) => setEditForm({ ...editForm, timezone: e.target.value })}
                      className="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                    />
                  </div>
                </div>

                <div className="flex items-center gap-2 pt-2">
                  <input
                    type="checkbox"
                    id="editIsActive"
                    checked={editForm.isActive}
                    onChange={(e) => setEditForm({ ...editForm, isActive: e.target.checked })}
                    className="w-4 h-4 rounded text-[#0B3D0B] focus:ring-[#0B3D0B] border-gray-300"
                  />
                  <label htmlFor="editIsActive" className="text-sm font-medium text-gray-700">
                    Active operational status
                  </label>
                </div>

                <div className="flex items-center justify-end gap-3 pt-4 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setEditingBranch(null)}
                    className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={editLoading}
                    className="inline-flex items-center gap-2 px-5 py-2 text-sm font-semibold text-white bg-[#0B3D0B] rounded-lg hover:bg-[#082d08] shadow-sm disabled:opacity-50"
                  >
                    {editLoading && <RefreshCw className="w-4 h-4 animate-spin" />}
                    Save Changes
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* STATUS TOGGLE CONFIRMATION MODAL */}
        {statusActionBranch && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
            <div className="bg-white rounded-2xl shadow-xl border border-gray-100 max-w-md w-full p-6 text-center">
              <div
                className={`w-12 h-12 rounded-full mx-auto flex items-center justify-center mb-4 ${
                  statusActionBranch.isActive ? "bg-amber-100 text-amber-600" : "bg-emerald-100 text-emerald-600"
                }`}
              >
                {statusActionBranch.isActive ? <Power className="w-6 h-6" /> : <RotateCcw className="w-6 h-6" />}
              </div>

              <h3 className="text-lg font-bold text-gray-900">
                {statusActionBranch.isActive ? "Deactivate Branch?" : "Activate Branch?"}
              </h3>

              <p className="text-sm text-gray-600 mt-2">
                Are you sure you want to {statusActionBranch.isActive ? "deactivate" : "activate"}{" "}
                <span className="font-semibold text-gray-900">{statusActionBranch.name}</span> (
                <span className="font-mono text-xs">{statusActionBranch.code}</span>)?
              </p>

              {statusActionBranch.isActive && (
                <p className="text-xs text-amber-700 bg-amber-50 p-2.5 rounded-lg border border-amber-200 mt-3 text-left">
                  Note: Inactive branches will not be available for new customer registrations in the mobile app.
                </p>
              )}

              {statusError && (
                <div className="mt-3 p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-xs flex items-center gap-2 text-left">
                  <AlertCircle className="w-4 h-4 flex-shrink-0" />
                  <span>{statusError}</span>
                </div>
              )}

              <div className="flex items-center justify-center gap-3 mt-6">
                <button
                  type="button"
                  onClick={() => setStatusActionBranch(null)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleStatusToggleSubmit}
                  disabled={statusLoading}
                  className={`inline-flex items-center gap-2 px-5 py-2 text-sm font-semibold text-white rounded-lg shadow-sm disabled:opacity-50 ${
                    statusActionBranch.isActive ? "bg-amber-600 hover:bg-amber-700" : "bg-emerald-600 hover:bg-emerald-700"
                  }`}
                >
                  {statusLoading && <RefreshCw className="w-4 h-4 animate-spin" />}
                  Confirm {statusActionBranch.isActive ? "Deactivate" : "Activate"}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* DELETE CONFIRMATION MODAL */}
        {deleteActionBranch && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm animate-in fade-in duration-200">
            <div className="bg-white rounded-2xl shadow-xl border border-gray-100 max-w-md w-full p-6 text-center">
              <div className="w-12 h-12 rounded-full mx-auto flex items-center justify-center mb-4 bg-red-100 text-red-600">
                <ShieldAlert className="w-6 h-6" />
              </div>

              <h3 className="text-lg font-bold text-gray-900">Delete Branch?</h3>

              <p className="text-sm text-gray-600 mt-2">
                You are about to permanently delete branch{" "}
                <span className="font-semibold text-gray-900">{deleteActionBranch.name}</span> (
                <span className="font-mono text-xs">{deleteActionBranch.code}</span>).
              </p>

              <p className="text-xs text-gray-500 bg-gray-50 p-2.5 rounded-lg border border-gray-200 mt-3 text-left">
                Safety Rule: Deletion is only permitted if no customers, staff members, or active plans are associated with this branch. If records exist, please deactivate the branch instead.
              </p>

              {deleteError && (
                <div className="mt-3 p-3 bg-red-50 border border-red-200 text-red-700 rounded-lg text-xs flex items-center gap-2 text-left">
                  <AlertCircle className="w-4 h-4 flex-shrink-0" />
                  <span>{deleteError}</span>
                </div>
              )}

              <div className="flex items-center justify-center gap-3 mt-6">
                <button
                  type="button"
                  onClick={() => setDeleteActionBranch(null)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleDeleteSubmit}
                  disabled={deleteLoading}
                  className="inline-flex items-center gap-2 px-5 py-2 text-sm font-semibold text-white bg-red-600 rounded-lg hover:bg-red-700 shadow-sm disabled:opacity-50"
                >
                  {deleteLoading && <RefreshCw className="w-4 h-4 animate-spin" />}
                  Delete Branch
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
