"use client";

import React, { useEffect, useState, useMemo, useRef } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "../../../lib/auth-context";
import { AdminLayout } from "../../../components/AdminLayout";
import { adminApi, ApiError } from "../../../lib/api";
import {
  JewelleryCategory,
  CreateJewelleryCategoryRequest,
  UpdateJewelleryCategoryRequest,
} from "../../../types/jewelleryCategory";
import { Branch } from "../../../types/branch";
import {
  Gem,
  Plus,
  Search,
  CheckCircle2,
  AlertCircle,
  XCircle,
  Edit2,
  Trash2,
  Power,
  RefreshCw,
  Building2,
  X,
  UploadCloud,
  ImageIcon,
  MoreVertical,
  Eye,
  ChevronLeft,
  ChevronRight,
  Filter,
} from "lucide-react";

export default function JewelleryCategoriesPage() {
  const router = useRouter();
  const { user, isSuperAdmin } = useAuth();

  // Categories list & pagination state
  const [categories, setCategories] = useState<JewelleryCategory[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [totalPages, setTotalPages] = useState<number>(1);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(10);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Search & Filters state
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [statusFilter, setStatusFilter] = useState<string>("ALL"); // 'ALL', 'ACTIVE', 'INACTIVE'
  const [selectedBranchId, setSelectedBranchId] = useState<string>("ALL");

  // Branches list (for Super Admin selector)
  const [branches, setBranches] = useState<Branch[]>([]);

  // Action menu popover state
  const [activeMenuId, setActiveMenuId] = useState<string | null>(null);

  // Modal states
  const [showCreateModal, setShowCreateModal] = useState<boolean>(false);
  const [viewingCategory, setViewingCategory] = useState<JewelleryCategory | null>(null);
  const [editingCategory, setEditingCategory] = useState<JewelleryCategory | null>(null);
  const [statusActionCategory, setStatusActionCategory] = useState<JewelleryCategory | null>(null);
  const [deleteActionCategory, setDeleteActionCategory] = useState<JewelleryCategory | null>(null);

  // Form states: Create
  const [createName, setCreateName] = useState<string>("");
  const [createDescription, setCreateDescription] = useState<string>("");
  const [createImageUrl, setCreateImageUrl] = useState<string>("");
  const [createBranchId, setCreateBranchId] = useState<string>("");
  const [createIsActive, setCreateIsActive] = useState<boolean>(true);
  const [createImageFile, setCreateImageFile] = useState<File | null>(null);
  const [createImagePreview, setCreateImagePreview] = useState<string | null>(null);
  const [createUploading, setCreateUploading] = useState<boolean>(false);
  const [createSubmitting, setCreateSubmitting] = useState<boolean>(false);
  const [createError, setCreateError] = useState<string | null>(null);
  const [createFieldErrors, setCreateFieldErrors] = useState<Record<string, string>>({});

  // Form states: Edit
  const [editName, setEditName] = useState<string>("");
  const [editDescription, setEditDescription] = useState<string>("");
  const [editImageUrl, setEditImageUrl] = useState<string>("");
  const [editIsActive, setEditIsActive] = useState<boolean>(true);
  const [editImageFile, setEditImageFile] = useState<File | null>(null);
  const [editImagePreview, setEditImagePreview] = useState<string | null>(null);
  const [editUploading, setEditUploading] = useState<boolean>(false);
  const [editSubmitting, setEditSubmitting] = useState<boolean>(false);
  const [editError, setEditError] = useState<string | null>(null);
  const [editFieldErrors, setEditFieldErrors] = useState<Record<string, string>>({});

  // Action loading states
  const [statusLoading, setStatusLoading] = useState<boolean>(false);
  const [deleteLoading, setDeleteLoading] = useState<boolean>(false);

  // File input refs
  const createFileInputRef = useRef<HTMLInputElement>(null);
  const editFileInputRef = useRef<HTMLInputElement>(null);

  // Load branches for Super Admin
  useEffect(() => {
    if (user && isSuperAdmin) {
      adminApi.getAdminBranches().then((list) => {
        setBranches(list);
      }).catch((e) => {
        console.error("Failed to load branches for selector:", e);
      });
    }
  }, [user, isSuperAdmin]);

  // Load categories whenever page, filters, or search change
  useEffect(() => {
    if (user) {
      loadCategories();
    }
  }, [user, currentPage, pageSize, statusFilter, selectedBranchId]);

  // Debounced search
  useEffect(() => {
    const handler = setTimeout(() => {
      setCurrentPage(1);
      loadCategories();
    }, 350);

    return () => clearTimeout(handler);
  }, [searchTerm]);

  // Close active dropdown menu when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      if (!target.closest(".action-menu-container")) {
        setActiveMenuId(null);
      }
    };
    document.addEventListener("click", handleClickOutside);
    return () => document.removeEventListener("click", handleClickOutside);
  }, []);

  const loadCategories = async () => {
    setLoading(true);
    setError(null);
    try {
      const branchIdParam =
        selectedBranchId !== "ALL" ? selectedBranchId : undefined;
      const isActiveParam =
        statusFilter === "ACTIVE"
          ? true
          : statusFilter === "INACTIVE"
          ? false
          : undefined;

      const res = await adminApi.getJewelleryCategories({
        page: currentPage,
        pageSize,
        search: searchTerm.trim() || undefined,
        branchId: branchIdParam,
        isActive: isActiveParam,
      });

      setCategories(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages);
    } catch (err: any) {
      setError(err?.message || "Failed to load jewellery categories.");
    } finally {
      setLoading(false);
    }
  };

  const showNotification = (msg: string) => {
    setSuccessMessage(msg);
    setTimeout(() => setSuccessMessage(null), 4000);
  };

  // ------------------ IMAGE VALIDATION & UPLOAD ------------------
  const validateImageFile = (file: File): string | null => {
    const maxSizeBytes = 2 * 1024 * 1024; // 2MB
    if (file.size > maxSizeBytes) {
      return "Image file size exceeds 2MB. Please choose a smaller image.";
    }
    const allowed = ["image/jpeg", "image/png", "image/webp"];
    if (!allowed.includes(file.type)) {
      return "Only PNG, JPG, and WEBP formats are supported.";
    }
    return null;
  };

  const handleCreateImageSelected = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const validationError = validateImageFile(file);
    if (validationError) {
      setCreateError(validationError);
      return;
    }

    setCreateError(null);
    setCreateImageFile(file);
    const objectUrl = URL.createObjectURL(file);
    setCreateImagePreview(objectUrl);
  };

  const handleEditImageSelected = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const validationError = validateImageFile(file);
    if (validationError) {
      setEditError(validationError);
      return;
    }

    setEditError(null);
    setEditImageFile(file);
    const objectUrl = URL.createObjectURL(file);
    setEditImagePreview(objectUrl);
  };

  // ------------------ CREATE CATEGORY ------------------
  const openCreateModal = () => {
    setCreateName("");
    setCreateDescription("");
    setCreateImageUrl("");
    setCreateBranchId(
      isSuperAdmin && branches.length > 0 ? branches[0].id : user?.assignedBranches?.[0]?.branchId || ""
    );
    setCreateIsActive(true);
    setCreateImageFile(null);
    setCreateImagePreview(null);
    setCreateError(null);
    setCreateFieldErrors({});
    setShowCreateModal(true);
  };

  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreateError(null);

    // Frontend validation
    const errors: Record<string, string> = {};
    if (!createName.trim()) {
      errors.name = "Category name is required.";
    }
    if (isSuperAdmin && !createBranchId) {
      errors.branchId = "Please select a branch.";
    }

    if (Object.keys(errors).length > 0) {
      setCreateFieldErrors(errors);
      return;
    }
    setCreateFieldErrors({});
    setCreateSubmitting(true);

    try {
      let finalImageUrl = createImageUrl.trim() || undefined;

      // Upload file to Supabase Storage if user selected one
      if (createImageFile) {
        setCreateUploading(true);
        const uploadRes = await adminApi.uploadJewelleryCategoryImage(createImageFile);
        finalImageUrl = uploadRes.url;
        setCreateUploading(false);
      }

      const payload: CreateJewelleryCategoryRequest = {
        name: createName.trim(),
        description: createDescription.trim() || undefined,
        imageUrl: finalImageUrl,
        isActive: createIsActive,
        branchId: isSuperAdmin ? createBranchId : undefined,
      };

      await adminApi.createJewelleryCategory(payload);
      setShowCreateModal(false);
      showNotification(`Category "${createName.trim()}" created successfully!`);
      loadCategories();
    } catch (err: any) {
      if (err instanceof ApiError && err.data?.errors) {
        setCreateFieldErrors(err.data.errors);
      }
      setCreateError(err?.message || "Failed to create category. Please check your inputs.");
    } finally {
      setCreateSubmitting(false);
      setCreateUploading(false);
    }
  };

  // ------------------ EDIT CATEGORY ------------------
  const openEditModal = (cat: JewelleryCategory) => {
    setEditingCategory(cat);
    setEditName(cat.name);
    setEditDescription(cat.description || "");
    setEditImageUrl(cat.imageUrl || "");
    setEditIsActive(cat.isActive);
    setEditImageFile(null);
    setEditImagePreview(cat.imageUrl || null);
    setEditError(null);
    setEditFieldErrors({});
    setActiveMenuId(null);
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingCategory) return;

    const errors: Record<string, string> = {};
    if (!editName.trim()) {
      errors.name = "Category name is required.";
    }
    if (Object.keys(errors).length > 0) {
      setEditFieldErrors(errors);
      return;
    }
    setEditFieldErrors({});
    setEditSubmitting(true);
    setEditError(null);

    try {
      let finalImageUrl = editImageUrl.trim() || undefined;

      if (editImageFile) {
        setEditUploading(true);
        const uploadRes = await adminApi.uploadJewelleryCategoryImage(editImageFile);
        finalImageUrl = uploadRes.url;
        setEditUploading(false);
      }

      const payload: UpdateJewelleryCategoryRequest = {
        name: editName.trim(),
        description: editDescription.trim() || undefined,
        imageUrl: finalImageUrl,
        isActive: editIsActive,
      };

      await adminApi.updateJewelleryCategory(editingCategory.id, payload);
      setEditingCategory(null);
      showNotification(`Category "${editName.trim()}" updated successfully!`);
      loadCategories();
    } catch (err: any) {
      if (err instanceof ApiError && err.data?.errors) {
        setEditFieldErrors(err.data.errors);
      }
      setEditError(err?.message || "Failed to update category.");
    } finally {
      setEditSubmitting(false);
      setEditUploading(false);
    }
  };

  // ------------------ STATUS TOGGLE ------------------
  const handleConfirmStatusChange = async () => {
    if (!statusActionCategory) return;
    setStatusLoading(true);
    try {
      const newStatus = !statusActionCategory.isActive;
      await adminApi.updateJewelleryCategoryStatus(statusActionCategory.id, newStatus);
      setStatusActionCategory(null);
      showNotification(
        `Category "${statusActionCategory.name}" is now ${
          newStatus ? "Active" : "Inactive"
        }.`
      );
      loadCategories();
    } catch (err: any) {
      alert(err?.message || "Failed to change status.");
    } finally {
      setStatusLoading(false);
    }
  };

  // ------------------ DELETE CATEGORY ------------------
  const handleConfirmDelete = async () => {
    if (!deleteActionCategory) return;
    if (deleteActionCategory.planCount > 0) {
      alert("Cannot delete a category with associated plans. Deactivate it instead.");
      setDeleteActionCategory(null);
      return;
    }
    setDeleteLoading(true);
    try {
      await adminApi.deleteJewelleryCategory(deleteActionCategory.id);
      showNotification(`Category "${deleteActionCategory.name}" deleted successfully.`);
      setDeleteActionCategory(null);
      loadCategories();
    } catch (err: any) {
      alert(err?.message || "Failed to delete category.");
    } finally {
      setDeleteLoading(false);
    }
  };

  // Generate pleasant fallback background colors for category icons matching screenshot
  const getCategoryColor = (name: string): string => {
    const colors = [
      "bg-[#691B24]", // deep maroon (like bridal in screenshot)
      "bg-[#D97706]", // warm amber / gold (like daily wear in screenshot)
      "bg-[#1E293B]", // deep slate / custom in screenshot
      "bg-[#0B3D0B]", // SJewls signature deep green
      "bg-[#0369A1]", // sapphire blue
      "bg-[#4D1D4D]", // plum purple
    ];
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const index = Math.abs(hash) % colors.length;
    return colors[index];
  };

  return (
    <AdminLayout>
      <div className="p-4 md:p-8 max-w-7xl mx-auto space-y-6">
        {/* ================= PAGE HEADER ================= */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-[#FFD700] text-[#0B3D0B] flex items-center justify-center shadow-md flex-shrink-0">
              <Gem className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-2xl font-bold text-gray-900 tracking-tight">
                Jewellery Categories
              </h1>
              <p className="text-xs text-gray-500">
                Group and manage jewellery savings plans across branches
              </p>
            </div>
          </div>

          <button
            onClick={openCreateModal}
            className="inline-flex items-center justify-center gap-2 px-5 py-2.5 rounded-xl font-bold text-sm bg-[#691B24] hover:bg-[#52141c] text-white shadow-md hover:shadow-lg transition-all duration-150 cursor-pointer"
          >
            <Plus className="w-4 h-4 stroke-[2.5]" />
            <span>Add Category</span>
          </button>
        </div>

        {/* ================= NOTIFICATIONS ================= */}
        {successMessage && (
          <div className="p-4 rounded-xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-sm flex items-center gap-3 shadow-xs animate-fadeIn">
            <CheckCircle2 className="w-5 h-5 text-emerald-600 flex-shrink-0" />
            <span className="font-medium">{successMessage}</span>
          </div>
        )}

        {error && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-sm flex items-center justify-between shadow-xs">
            <div className="flex items-center gap-3">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
            <button
              onClick={loadCategories}
              className="text-xs font-semibold text-red-700 underline hover:no-underline cursor-pointer"
            >
              Retry
            </button>
          </div>
        )}

        {/* ================= MAIN CARD CONTAINER ================= */}
        <div className="bg-white rounded-2xl border border-gray-200/80 shadow-sm overflow-hidden">
          {/* Card Top: Counts, Heading, and Search Bar */}
          <div className="p-5 md:p-6 border-b border-gray-100 flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <div>
              <div className="text-xs font-semibold text-amber-600 tracking-wide uppercase">
                Total Categories{" "}
                <span className="text-amber-700 font-extrabold text-sm">
                  {totalCount}
                </span>
              </div>
              <h2 className="text-xl font-extrabold text-gray-900 mt-0.5">
                Manage Categories
              </h2>
            </div>

            {/* Filter and Search controls */}
            <div className="flex flex-wrap items-center gap-3">
              {/* Branch Selector for Super Admin */}
              {isSuperAdmin && branches.length > 0 && (
                <div className="flex items-center gap-2">
                  <Building2 className="w-4 h-4 text-gray-400" />
                  <select
                    value={selectedBranchId}
                    onChange={(e) => {
                      setSelectedBranchId(e.target.value);
                      setCurrentPage(1);
                    }}
                    className="text-xs font-medium bg-gray-50 border border-gray-200 rounded-xl px-3 py-2 text-gray-700 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]"
                  >
                    <option value="ALL">All Branches</option>
                    {branches.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name} ({b.code})
                      </option>
                    ))}
                  </select>
                </div>
              )}

              {/* Status Filter Tabs */}
              <div className="flex items-center bg-gray-100 p-1 rounded-xl text-xs font-semibold text-gray-600">
                <button
                  type="button"
                  onClick={() => {
                    setStatusFilter("ALL");
                    setCurrentPage(1);
                  }}
                  className={`px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${
                    statusFilter === "ALL"
                      ? "bg-white text-gray-900 shadow-xs font-bold"
                      : "hover:text-gray-900"
                  }`}
                >
                  All
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setStatusFilter("ACTIVE");
                    setCurrentPage(1);
                  }}
                  className={`px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${
                    statusFilter === "ACTIVE"
                      ? "bg-white text-emerald-800 shadow-xs font-bold"
                      : "hover:text-gray-900"
                  }`}
                >
                  Active
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setStatusFilter("INACTIVE");
                    setCurrentPage(1);
                  }}
                  className={`px-3 py-1.5 rounded-lg transition-colors cursor-pointer ${
                    statusFilter === "INACTIVE"
                      ? "bg-white text-gray-700 shadow-xs font-bold"
                      : "hover:text-gray-900"
                  }`}
                >
                  Inactive
                </button>
              </div>

              {/* Search Box */}
              <div className="relative min-w-[220px] sm:min-w-[260px]">
                <Search className="w-4 h-4 text-gray-400 absolute left-3.5 top-1/2 -translate-y-1/2 pointer-events-none" />
                <input
                  type="text"
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  placeholder="Search categories"
                  className="w-full text-xs font-medium pl-9 pr-4 py-2 bg-gray-50 border border-gray-200 rounded-xl text-gray-800 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                />
                {searchTerm && (
                  <button
                    onClick={() => setSearchTerm("")}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                  >
                    <X className="w-3.5 h-3.5" />
                  </button>
                )}
              </div>
            </div>
          </div>

          {/* ================= TABLE ================= */}
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-gray-50/80 border-b border-gray-200/80 text-[11px] font-bold text-gray-500 uppercase tracking-wider">
                  <th className="py-3.5 px-6 w-24">Image</th>
                  <th className="py-3.5 px-6">Category Name</th>
                  <th className="py-3.5 px-6">Description</th>
                  <th className="py-3.5 px-6 text-center">Plans</th>
                  <th className="py-3.5 px-6 text-center">Status</th>
                  <th className="py-3.5 px-6 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-sm font-normal">
                {loading ? (
                  <tr>
                    <td colSpan={6} className="py-16 text-center">
                      <div className="flex flex-col items-center justify-center gap-3">
                        <div className="w-8 h-8 border-3 border-[#0B3D0B] border-t-[#FFD700] rounded-full animate-spin" />
                        <span className="text-xs font-semibold text-gray-500">
                          Loading categories...
                        </span>
                      </div>
                    </td>
                  </tr>
                ) : categories.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-16 text-center">
                      <div className="flex flex-col items-center justify-center max-w-sm mx-auto">
                        <div className="w-14 h-14 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center mb-3">
                          <Gem className="w-7 h-7" />
                        </div>
                        <h3 className="text-base font-bold text-gray-800">
                          No categories found
                        </h3>
                        <p className="text-xs text-gray-500 mt-1 mb-4 text-center">
                          {searchTerm
                            ? `No categories match "${searchTerm}". Try a different search.`
                            : "There are no jewellery plan categories created for this branch yet."}
                        </p>
                        <button
                          onClick={openCreateModal}
                          className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold bg-[#691B24] text-white hover:bg-[#52141c] shadow-sm transition-colors cursor-pointer"
                        >
                          <Plus className="w-4 h-4" />
                          Create New Category
                        </button>
                      </div>
                    </td>
                  </tr>
                ) : (
                  categories.map((cat) => {
                    const fallbackColor = getCategoryColor(cat.name);

                    return (
                      <tr
                        key={cat.id}
                        className="hover:bg-amber-50/20 transition-colors group"
                      >
                        {/* Image Thumbnail */}
                        <td className="py-4 px-6">
                          <div className="w-12 h-12 rounded-xl overflow-hidden flex items-center justify-center shadow-xs border border-black/5 flex-shrink-0">
                            {cat.imageUrl ? (
                              <img
                                src={cat.imageUrl}
                                alt={cat.name}
                                className="w-full h-full object-cover"
                              />
                            ) : (
                              <div
                                className={`w-full h-full ${fallbackColor} text-white flex flex-col items-center justify-center p-1 text-center font-bold text-[10px] uppercase tracking-tighter`}
                              >
                                <span className="text-xs font-black truncate max-w-full px-0.5">
                                  {cat.name.substring(0, 3)}
                                </span>
                              </div>
                            )}
                          </div>
                        </td>

                        {/* Category Name */}
                        <td className="py-4 px-6">
                          <div className="font-bold text-gray-900 group-hover:text-[#691B24] transition-colors">
                            {cat.name}
                          </div>
                          {isSuperAdmin && (
                            <div className="text-[11px] text-gray-400 flex items-center gap-1 mt-0.5">
                              <Building2 className="w-3 h-3 text-gray-400" />
                              <span>{cat.branchName}</span>
                            </div>
                          )}
                        </td>

                        {/* Description */}
                        <td className="py-4 px-6 max-w-md">
                          <p className="text-xs text-gray-500 line-clamp-2">
                            {cat.description || (
                              <span className="text-gray-300 italic">
                                No description provided
                              </span>
                            )}
                          </p>
                        </td>

                        {/* Plans Count */}
                        <td className="py-4 px-6 text-center">
                          <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-extrabold text-gray-700 bg-gray-100">
                            {cat.planCount}{" "}
                            <span className="ml-1 text-[10px] text-gray-500 font-semibold uppercase">
                              Plans
                            </span>
                          </span>
                        </td>

                        {/* Status */}
                        <td className="py-4 px-6 text-center">
                          {cat.isActive ? (
                            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-emerald-50 text-emerald-700 border border-emerald-200/60">
                              Active
                            </span>
                          ) : (
                            <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-gray-100 text-gray-600 border border-gray-200">
                              Inactive
                            </span>
                          )}
                        </td>

                        {/* Actions Menu */}
                        <td className="py-4 px-6 text-right">
                          <div className="relative inline-block text-left action-menu-container">
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                setActiveMenuId(
                                  activeMenuId === cat.id ? null : cat.id
                                );
                              }}
                              className="p-2 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors cursor-pointer"
                              title="Actions"
                            >
                              <MoreVertical className="w-4 h-4" />
                            </button>

                            {/* Dropdown Popover */}
                            {activeMenuId === cat.id && (
                              <div className="absolute right-0 top-full mt-1 w-44 bg-white rounded-xl shadow-xl border border-gray-100 py-1.5 z-30 animate-fadeIn">
                                <button
                                  type="button"
                                  onClick={() => {
                                    setViewingCategory(cat);
                                    setActiveMenuId(null);
                                  }}
                                  className="w-full flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 hover:text-gray-900 transition-colors cursor-pointer"
                                >
                                  <Eye className="w-3.5 h-3.5 text-blue-600" />
                                  <span>View Details</span>
                                </button>

                                <button
                                  type="button"
                                  onClick={() => openEditModal(cat)}
                                  className="w-full flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 hover:text-gray-900 transition-colors cursor-pointer"
                                >
                                  <Edit2 className="w-3.5 h-3.5 text-amber-600" />
                                  <span>Edit Category</span>
                                </button>

                                <button
                                  type="button"
                                  onClick={() => {
                                    setStatusActionCategory(cat);
                                    setActiveMenuId(null);
                                  }}
                                  className="w-full flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 hover:text-gray-900 transition-colors cursor-pointer"
                                >
                                  <Power
                                    className={`w-3.5 h-3.5 ${
                                      cat.isActive
                                        ? "text-red-500"
                                        : "text-emerald-600"
                                    }`}
                                  />
                                  <span>
                                    {cat.isActive
                                      ? "Deactivate"
                                      : "Activate"}
                                  </span>
                                </button>

                                <div className="border-t border-gray-100 my-1" />

                                <button
                                  type="button"
                                  onClick={() => {
                                    setDeleteActionCategory(cat);
                                    setActiveMenuId(null);
                                  }}
                                  disabled={cat.planCount > 0}
                                  className={`w-full flex items-center gap-2.5 px-3.5 py-2 text-xs font-medium transition-colors cursor-pointer ${
                                    cat.planCount > 0
                                      ? "text-gray-300 cursor-not-allowed"
                                      : "text-red-600 hover:bg-red-50"
                                  }`}
                                  title={
                                    cat.planCount > 0
                                      ? "Cannot delete category with associated plans"
                                      : "Delete category"
                                  }
                                >
                                  <Trash2 className="w-3.5 h-3.5" />
                                  <span>
                                    Delete{" "}
                                    {cat.planCount > 0 ? "(Has Plans)" : ""}
                                  </span>
                                </button>
                              </div>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* ================= PAGINATION ================= */}
          {!loading && categories.length > 0 && (
            <div className="p-4 sm:p-5 border-t border-gray-100 flex flex-col sm:flex-row sm:items-center justify-between gap-4 text-xs text-gray-500">
              <div>
                Showing results{" "}
                <span className="font-bold text-gray-800">
                  {Math.min((currentPage - 1) * pageSize + 1, totalCount)}
                </span>{" "}
                to{" "}
                <span className="font-bold text-gray-800">
                  {Math.min(currentPage * pageSize, totalCount)}
                </span>{" "}
                of{" "}
                <span className="font-bold text-gray-800">{totalCount}</span>
              </div>

              <div className="flex items-center gap-3">
                <div className="flex items-center gap-1.5">
                  <span>Go To Page</span>
                  <input
                    type="number"
                    min={1}
                    max={totalPages}
                    value={currentPage}
                    onChange={(e) => {
                      const p = parseInt(e.target.value);
                      if (p >= 1 && p <= totalPages) {
                        setCurrentPage(p);
                      }
                    }}
                    className="w-12 text-center py-1 bg-gray-50 border border-gray-200 rounded-lg text-gray-800 font-bold focus:outline-none focus:ring-1 focus:ring-[#0B3D0B]"
                  />
                </div>

                <div className="flex items-center gap-1">
                  <button
                    onClick={() => setCurrentPage((p) => Math.max(p - 1, 1))}
                    disabled={currentPage <= 1}
                    className="p-1.5 rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                    title="Previous page"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>

                  <span className="w-8 h-8 rounded-full bg-[#691B24] text-white font-bold flex items-center justify-center text-xs shadow-xs">
                    {currentPage}
                  </span>

                  <button
                    onClick={() =>
                      setCurrentPage((p) => Math.min(p + 1, totalPages))
                    }
                    disabled={currentPage >= totalPages}
                    className="p-1.5 rounded-lg border border-gray-200 text-gray-600 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer"
                    title="Next page"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* ================= MODAL: CREATE CATEGORY ================= */}
        {showCreateModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl max-w-lg w-full shadow-2xl overflow-hidden border border-gray-100 flex flex-col max-h-[90vh]">
              {/* Header */}
              <div className="px-6 py-5 border-b border-gray-100 flex items-center justify-between">
                <h3 className="text-lg font-bold text-gray-900">
                  Create New Category
                </h3>
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="w-8 h-8 rounded-full bg-red-800 text-white flex items-center justify-center hover:bg-red-900 transition-colors cursor-pointer shadow-xs"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              {/* Form Body */}
              <form onSubmit={handleCreateSubmit} className="p-6 overflow-y-auto space-y-6">
                {createError && (
                  <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-red-700 text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 flex-shrink-0" />
                    <span>{createError}</span>
                  </div>
                )}

                {/* Section 1: CATEGORY VISUALS */}
                <div>
                  <div className="flex items-center gap-2 text-xs font-bold text-gray-700 uppercase tracking-wider mb-2.5">
                    <ImageIcon className="w-4 h-4 text-amber-600" />
                    <span>Category Visuals</span>
                  </div>

                  {/* Dashed Dropzone */}
                  <div className="border-2 border-dashed border-gray-200 hover:border-amber-500/50 rounded-2xl p-6 text-center transition-colors bg-gray-50/50">
                    {createImagePreview ? (
                      <div className="flex flex-col items-center">
                        <div className="w-24 h-24 rounded-2xl overflow-hidden border border-gray-200 shadow-md mb-3 relative group">
                          <img
                            src={createImagePreview}
                            alt="Preview"
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <button
                          type="button"
                          onClick={() => {
                            setCreateImageFile(null);
                            setCreateImagePreview(null);
                            setCreateImageUrl("");
                            if (createFileInputRef.current) {
                              createFileInputRef.current.value = "";
                            }
                          }}
                          className="text-xs font-semibold text-red-600 hover:text-red-700 cursor-pointer"
                        >
                          Remove Image
                        </button>
                      </div>
                    ) : (
                      <div className="flex flex-col items-center">
                        <input
                          type="file"
                          ref={createFileInputRef}
                          onChange={handleCreateImageSelected}
                          accept="image/png,image/jpeg,image/webp"
                          className="hidden"
                        />
                        <button
                          type="button"
                          onClick={() => createFileInputRef.current?.click()}
                          className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold bg-white border border-gray-200 text-gray-700 hover:bg-gray-50 shadow-xs transition-colors cursor-pointer"
                        >
                          <UploadCloud className="w-4 h-4 text-gray-500" />
                          <span>Upload files</span>
                        </button>
                        <p className="text-[11px] text-gray-400 mt-2.5">
                          • PNG, JPG, WEBP • Max 2MB
                        </p>
                      </div>
                    )}
                  </div>
                </div>

                {/* Section 2: BASIC INFORMATION */}
                <div className="space-y-4">
                  <div className="flex items-center gap-2 text-xs font-bold text-gray-700 uppercase tracking-wider">
                    <Edit2 className="w-4 h-4 text-amber-600" />
                    <span>Basic Information</span>
                  </div>

                  {/* Category Name */}
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Category Name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={createName}
                      onChange={(e) => setCreateName(e.target.value)}
                      placeholder="e.g. Bridal Collection"
                      required
                      className={`w-full text-xs font-medium px-3.5 py-2.5 bg-gray-50 border rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all ${
                        createFieldErrors.name
                          ? "border-red-400"
                          : "border-gray-200"
                      }`}
                    />
                    {createFieldErrors.name && (
                      <p className="text-[11px] text-red-500 mt-1">
                        {createFieldErrors.name}
                      </p>
                    )}
                  </div>

                  {/* Branch selector (if Super Admin) */}
                  {isSuperAdmin && (
                    <div>
                      <label className="block text-xs font-bold text-gray-700 mb-1.5">
                        Branch <span className="text-red-500">*</span>
                      </label>
                      <select
                        value={createBranchId}
                        onChange={(e) => setCreateBranchId(e.target.value)}
                        className="w-full text-xs font-medium px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                      >
                        {branches.map((b) => (
                          <option key={b.id} value={b.id}>
                            {b.name} ({b.code})
                          </option>
                        ))}
                      </select>
                      {createFieldErrors.branchId && (
                        <p className="text-[11px] text-red-500 mt-1">
                          {createFieldErrors.branchId}
                        </p>
                      )}
                    </div>
                  )}

                  {/* Description */}
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Description
                    </label>
                    <textarea
                      rows={3}
                      value={createDescription}
                      onChange={(e) => setCreateDescription(e.target.value)}
                      placeholder="Describe the purpose of this category..."
                      className="w-full text-xs font-medium px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                    />
                    <p className="text-[11px] text-gray-400 mt-1">
                      A brief description helps in identifying the category during plan creation.
                    </p>
                  </div>

                  {/* Status Toggle */}
                  <div className="flex items-center justify-between p-3 rounded-xl bg-gray-50 border border-gray-200">
                    <div>
                      <div className="text-xs font-bold text-gray-800">
                        Initial Status
                      </div>
                      <div className="text-[11px] text-gray-500">
                        {createIsActive
                          ? "Category will be active immediately"
                          : "Category will be saved as inactive"}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => setCreateIsActive((prev) => !prev)}
                      className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors cursor-pointer ${
                        createIsActive ? "bg-[#0B3D0B]" : "bg-gray-300"
                      }`}
                    >
                      <span
                        className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                          createIsActive ? "translate-x-6" : "translate-x-1"
                        }`}
                      />
                    </button>
                  </div>
                </div>

                {/* Footer Buttons */}
                <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setShowCreateModal(false)}
                    disabled={createSubmitting}
                    className="px-5 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 transition-colors cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={createSubmitting}
                    className="inline-flex items-center gap-2 px-6 py-2.5 rounded-xl text-xs font-bold bg-[#691B24] hover:bg-[#52141c] text-white shadow-md disabled:opacity-50 cursor-pointer transition-all"
                  >
                    {createSubmitting ? (
                      <>
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                        <span>
                          {createUploading ? "Uploading visual..." : "Creating..."}
                        </span>
                      </>
                    ) : (
                      <span>Create Category</span>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ================= MODAL: EDIT CATEGORY ================= */}
        {editingCategory && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl max-w-lg w-full shadow-2xl overflow-hidden border border-gray-100 flex flex-col max-h-[90vh]">
              {/* Header */}
              <div className="px-6 py-5 border-b border-gray-100 flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-bold text-gray-900">
                    Edit Category
                  </h3>
                  <p className="text-xs text-gray-500">
                    Updating "{editingCategory.name}"
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setEditingCategory(null)}
                  className="w-8 h-8 rounded-full bg-gray-100 text-gray-500 flex items-center justify-center hover:bg-gray-200 transition-colors cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              {/* Form Body */}
              <form onSubmit={handleEditSubmit} className="p-6 overflow-y-auto space-y-6">
                {editError && (
                  <div className="p-3.5 rounded-xl bg-red-50 border border-red-200 text-red-700 text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 flex-shrink-0" />
                    <span>{editError}</span>
                  </div>
                )}

                {/* Section 1: CATEGORY VISUALS */}
                <div>
                  <div className="flex items-center gap-2 text-xs font-bold text-gray-700 uppercase tracking-wider mb-2.5">
                    <ImageIcon className="w-4 h-4 text-amber-600" />
                    <span>Category Visuals</span>
                  </div>

                  <div className="border-2 border-dashed border-gray-200 rounded-2xl p-6 text-center bg-gray-50/50">
                    {editImagePreview ? (
                      <div className="flex flex-col items-center">
                        <div className="w-24 h-24 rounded-2xl overflow-hidden border border-gray-200 shadow-md mb-3">
                          <img
                            src={editImagePreview}
                            alt="Preview"
                            className="w-full h-full object-cover"
                          />
                        </div>
                        <div className="flex items-center gap-3">
                          <button
                            type="button"
                            onClick={() => editFileInputRef.current?.click()}
                            className="text-xs font-semibold text-blue-600 hover:text-blue-700 cursor-pointer"
                          >
                            Change Visual
                          </button>
                          <span className="text-gray-300">•</span>
                          <button
                            type="button"
                            onClick={() => {
                              setEditImageFile(null);
                              setEditImagePreview(null);
                              setEditImageUrl("");
                              if (editFileInputRef.current) {
                                editFileInputRef.current.value = "";
                              }
                            }}
                            className="text-xs font-semibold text-red-600 hover:text-red-700 cursor-pointer"
                          >
                            Remove
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="flex flex-col items-center">
                        <input
                          type="file"
                          ref={editFileInputRef}
                          onChange={handleEditImageSelected}
                          accept="image/png,image/jpeg,image/webp"
                          className="hidden"
                        />
                        <button
                          type="button"
                          onClick={() => editFileInputRef.current?.click()}
                          className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold bg-white border border-gray-200 text-gray-700 hover:bg-gray-50 shadow-xs transition-colors cursor-pointer"
                        >
                          <UploadCloud className="w-4 h-4 text-gray-500" />
                          <span>Upload replacement</span>
                        </button>
                        <p className="text-[11px] text-gray-400 mt-2.5">
                          • PNG, JPG, WEBP • Max 2MB
                        </p>
                      </div>
                    )}
                    <input
                      type="file"
                      ref={editFileInputRef}
                      onChange={handleEditImageSelected}
                      accept="image/png,image/jpeg,image/webp"
                      className="hidden"
                    />
                  </div>
                </div>

                {/* Section 2: BASIC INFORMATION */}
                <div className="space-y-4">
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Category Name <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="text"
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      required
                      className={`w-full text-xs font-medium px-3.5 py-2.5 bg-gray-50 border rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all ${
                        editFieldErrors.name
                          ? "border-red-400"
                          : "border-gray-200"
                      }`}
                    />
                    {editFieldErrors.name && (
                      <p className="text-[11px] text-red-500 mt-1">
                        {editFieldErrors.name}
                      </p>
                    )}
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Description
                    </label>
                    <textarea
                      rows={3}
                      value={editDescription}
                      onChange={(e) => setEditDescription(e.target.value)}
                      placeholder="Describe the purpose of this category..."
                      className="w-full text-xs font-medium px-3.5 py-2.5 bg-gray-50 border border-gray-200 rounded-xl focus:outline-none focus:ring-2 focus:ring-[#0B3D0B] focus:bg-white transition-all"
                    />
                  </div>

                  <div className="flex items-center justify-between p-3 rounded-xl bg-gray-50 border border-gray-200">
                    <div>
                      <div className="text-xs font-bold text-gray-800">
                        Category Status
                      </div>
                      <div className="text-[11px] text-gray-500">
                        {editIsActive ? "Active" : "Inactive"}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => setEditIsActive((prev) => !prev)}
                      className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors cursor-pointer ${
                        editIsActive ? "bg-[#0B3D0B]" : "bg-gray-300"
                      }`}
                    >
                      <span
                        className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${
                          editIsActive ? "translate-x-6" : "translate-x-1"
                        }`}
                      />
                    </button>
                  </div>
                </div>

                {/* Footer Buttons */}
                <div className="pt-2 flex items-center justify-end gap-3 border-t border-gray-100">
                  <button
                    type="button"
                    onClick={() => setEditingCategory(null)}
                    disabled={editSubmitting}
                    className="px-5 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 transition-colors cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={editSubmitting}
                    className="inline-flex items-center gap-2 px-6 py-2.5 rounded-xl text-xs font-bold bg-[#691B24] hover:bg-[#52141c] text-white shadow-md disabled:opacity-50 cursor-pointer transition-all"
                  >
                    {editSubmitting ? (
                      <>
                        <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                        <span>
                          {editUploading ? "Uploading visual..." : "Saving..."}
                        </span>
                      </>
                    ) : (
                      <span>Save Changes</span>
                    )}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ================= MODAL: VIEW DETAILS ================= */}
        {viewingCategory && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl max-w-md w-full shadow-2xl overflow-hidden border border-gray-100 p-6 space-y-5">
              <div className="flex items-center justify-between border-b border-gray-100 pb-4">
                <div className="flex items-center gap-3">
                  <div className="w-12 h-12 rounded-2xl overflow-hidden border border-gray-200 shadow-sm flex items-center justify-center">
                    {viewingCategory.imageUrl ? (
                      <img
                        src={viewingCategory.imageUrl}
                        alt={viewingCategory.name}
                        className="w-full h-full object-cover"
                      />
                    ) : (
                      <div
                        className={`w-full h-full ${getCategoryColor(
                          viewingCategory.name
                        )} text-white flex items-center justify-center font-bold text-sm`}
                      >
                        {viewingCategory.name.substring(0, 2).toUpperCase()}
                      </div>
                    )}
                  </div>
                  <div>
                    <h3 className="text-base font-bold text-gray-900">
                      {viewingCategory.name}
                    </h3>
                    <p className="text-xs text-gray-500">
                      Branch: {viewingCategory.branchName}
                    </p>
                  </div>
                </div>
                <button
                  onClick={() => setViewingCategory(null)}
                  className="w-7 h-7 rounded-full bg-gray-100 text-gray-500 flex items-center justify-center hover:bg-gray-200 cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="space-y-3 text-xs">
                <div>
                  <span className="font-bold text-gray-500 block mb-1">
                    Description:
                  </span>
                  <p className="text-gray-800 bg-gray-50 p-3 rounded-xl border border-gray-100">
                    {viewingCategory.description || "No description provided."}
                  </p>
                </div>

                <div className="grid grid-cols-2 gap-3 pt-2">
                  <div className="bg-gray-50 p-3 rounded-xl border border-gray-100">
                    <span className="text-[10px] uppercase font-bold text-gray-400 block">
                      Associated Plans
                    </span>
                    <span className="text-sm font-extrabold text-gray-800">
                      {viewingCategory.planCount} Active Plans
                    </span>
                  </div>

                  <div className="bg-gray-50 p-3 rounded-xl border border-gray-100">
                    <span className="text-[10px] uppercase font-bold text-gray-400 block">
                      Status
                    </span>
                    <span
                      className={`inline-block mt-0.5 font-bold ${
                        viewingCategory.isActive
                          ? "text-emerald-700"
                          : "text-gray-600"
                      }`}
                    >
                      {viewingCategory.isActive ? "Active" : "Inactive"}
                    </span>
                  </div>
                </div>

                <div className="text-[11px] text-gray-400 pt-2 border-t border-gray-100 flex justify-between">
                  <span>Created: {new Date(viewingCategory.createdAtUtc).toLocaleDateString()}</span>
                  <span>Branch Code: {viewingCategory.branchCode}</span>
                </div>
              </div>

              <div className="pt-2 flex justify-end">
                <button
                  onClick={() => setViewingCategory(null)}
                  className="px-5 py-2 rounded-xl text-xs font-bold bg-gray-900 text-white hover:bg-black transition-colors cursor-pointer"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}

        {/* ================= MODAL: STATUS CONFIRMATION ================= */}
        {statusActionCategory && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl max-w-md w-full shadow-2xl overflow-hidden border border-gray-100 p-6 space-y-4">
              <div className="flex items-center gap-3">
                <div
                  className={`w-11 h-11 rounded-2xl flex items-center justify-center ${
                    statusActionCategory.isActive
                      ? "bg-red-50 text-red-600"
                      : "bg-emerald-50 text-emerald-600"
                  }`}
                >
                  <Power className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-gray-900">
                    {statusActionCategory.isActive
                      ? "Deactivate Category?"
                      : "Activate Category?"}
                  </h3>
                  <p className="text-xs text-gray-500">
                    {statusActionCategory.name}
                  </p>
                </div>
              </div>

              <p className="text-xs text-gray-600 leading-relaxed">
                {statusActionCategory.isActive
                  ? "Deactivating this category will hide it from future plan creation and customer browsing. Existing customer enrolments and payment history will remain intact and will NOT be affected."
                  : "Activating this category makes it available for plan creation and customer browsing."}
              </p>

              <div className="pt-3 flex items-center justify-end gap-3 border-t border-gray-100">
                <button
                  type="button"
                  onClick={() => setStatusActionCategory(null)}
                  disabled={statusLoading}
                  className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleConfirmStatusChange}
                  disabled={statusLoading}
                  className={`inline-flex items-center gap-2 px-5 py-2.5 rounded-xl text-xs font-bold text-white shadow-md transition-all cursor-pointer ${
                    statusActionCategory.isActive
                      ? "bg-red-600 hover:bg-red-700"
                      : "bg-emerald-700 hover:bg-emerald-800"
                  }`}
                >
                  {statusLoading ? (
                    <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  ) : (
                    <span>
                      {statusActionCategory.isActive
                        ? "Deactivate Category"
                        : "Activate Category"}
                    </span>
                  )}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* ================= MODAL: DELETE CONFIRMATION ================= */}
        {deleteActionCategory && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl max-w-md w-full shadow-2xl overflow-hidden border border-gray-100 p-6 space-y-4">
              <div className="flex items-center gap-3">
                <div className="w-11 h-11 rounded-2xl bg-red-50 text-red-600 flex items-center justify-center">
                  <Trash2 className="w-5 h-5" />
                </div>
                <div>
                  <h3 className="text-base font-bold text-gray-900">
                    Delete Category?
                  </h3>
                  <p className="text-xs text-gray-500">
                    {deleteActionCategory.name}
                  </p>
                </div>
              </div>

              {deleteActionCategory.planCount > 0 ? (
                <div className="p-3 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs flex items-center gap-2">
                  <AlertCircle className="w-4 h-4 flex-shrink-0" />
                  <span>
                    This category has {deleteActionCategory.planCount} plan(s) associated with it. You cannot delete it. Deactivate it instead.
                  </span>
                </div>
              ) : (
                <p className="text-xs text-gray-600 leading-relaxed">
                  Are you sure you want to permanently delete "{deleteActionCategory.name}"? This action cannot be undone.
                </p>
              )}

              <div className="pt-3 flex items-center justify-end gap-3 border-t border-gray-100">
                <button
                  type="button"
                  onClick={() => setDeleteActionCategory(null)}
                  disabled={deleteLoading}
                  className="px-4 py-2 rounded-xl text-xs font-bold text-gray-600 hover:bg-gray-100 transition-colors cursor-pointer"
                >
                  Cancel
                </button>
                {deleteActionCategory.planCount === 0 && (
                  <button
                    type="button"
                    onClick={handleConfirmDelete}
                    disabled={deleteLoading}
                    className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl text-xs font-bold bg-red-600 hover:bg-red-700 text-white shadow-md transition-all cursor-pointer"
                  >
                    {deleteLoading ? (
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                    ) : (
                      <span>Delete Category</span>
                    )}
                  </button>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
