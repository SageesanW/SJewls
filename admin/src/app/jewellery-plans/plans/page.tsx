"use client";

import React, { useEffect, useState, useMemo, useRef, useCallback } from "react";
import { useRouter } from "next/navigation";
import Image from "next/image";
import Link from "next/link";
import { useAuth } from "../../../lib/auth-context";
import { AdminLayout } from "../../../components/AdminLayout";
import { adminApi, ApiError } from "../../../lib/api";
import {
  JewelleryPlan,
  CreateJewelleryPlanRequest,
  UpdateJewelleryPlanRequest,
  PlanCustomerEnrolment,
  PlanCustomersPagedResponse,
} from "../../../types/jewelleryPlan";
import { JewelleryCategory } from "../../../types/jewelleryCategory";
import { Branch } from "../../../types/auth";
import {
  Gem,
  Plus,
  Search,
  CheckCircle2,
  AlertCircle,
  XCircle,
  Edit2,
  Power,
  RefreshCw,
  Building2,
  X,
  UploadCloud,
  ImageIcon,
  MoreVertical,
  Users,
  Calendar,
  Clock,
  ArrowRight,
  TrendingUp,
  AlertTriangle,
  History,
  Phone,
  Mail,
  ChevronLeft,
  ChevronRight,
  Filter,
} from "lucide-react";

export default function JewelleryPlansPage() {
  const router = useRouter();
  const { user, isSuperAdmin } = useAuth();

  // Plans state
  const [plans, setPlans] = useState<JewelleryPlan[]>([]);
  const [totalCount, setTotalCount] = useState<number>(0);
  const [totalPages, setTotalPages] = useState<number>(1);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(12);
  const [activeCount, setActiveCount] = useState<number>(0);
  const [scheduledCount, setScheduledCount] = useState<number>(0);
  const [deactivatedCount, setDeactivatedCount] = useState<number>(0);

  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Filters state
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState<string>("ALL");
  const [statusFilter, setStatusFilter] = useState<string>("ALL"); // 'ALL', 'Active', 'Scheduled', 'Deactivated'
  const [selectedBranchId, setSelectedBranchId] = useState<string>("ALL");

  // Metadata
  const [categories, setCategories] = useState<JewelleryCategory[]>([]);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [categoriesLoading, setCategoriesLoading] = useState<boolean>(false);

  // Action popover
  const [activeMenuId, setActiveMenuId] = useState<string | null>(null);

  // Modal states
  const [showCreateModal, setShowCreateModal] = useState<boolean>(false);
  const [editingPlan, setEditingPlan] = useState<JewelleryPlan | null>(null);
  const [statusActionPlan, setStatusActionPlan] = useState<{ plan: JewelleryPlan; action: "Deactivate" | "Reopen" } | null>(null);
  const [statusReason, setStatusReason] = useState<string>("");
  const [statusLoading, setStatusLoading] = useState<boolean>(false);

  // Customers panel/modal
  const [viewCustomersPlan, setViewCustomersPlan] = useState<JewelleryPlan | null>(null);
  const [planCustomersData, setPlanCustomersData] = useState<PlanCustomersPagedResponse | null>(null);
  const [customersLoading, setCustomersLoading] = useState<boolean>(false);
  const [customersSearch, setCustomersSearch] = useState<string>("");
  const [customersPage, setCustomersPage] = useState<number>(1);
  const [selectedCustomerContributions, setSelectedCustomerContributions] = useState<PlanCustomerEnrolment | null>(null);

  // Form states: Create / Edit
  const [formBranchId, setFormBranchId] = useState<string>("");
  const [formCategoryId, setFormCategoryId] = useState<string>("");
  const [formName, setFormName] = useState<string>("");
  const [formDescription, setFormDescription] = useState<string>("");
  const [formImageUrl, setFormImageUrl] = useState<string>("");
  const [formDurations, setFormDurations] = useState<number[]>([6, 12, 18]);
  const [customMonthInput, setCustomMonthInput] = useState<string>("");
  const [formStartDate, setFormStartDate] = useState<string>("");
  const [formTargetGrams, setFormTargetGrams] = useState<string>("25");
  const [formImageFile, setFormImageFile] = useState<File | null>(null);
  const [formImagePreview, setFormImagePreview] = useState<string | null>(null);
  const [formUploading, setFormUploading] = useState<boolean>(false);
  const [formSubmitting, setFormSubmitting] = useState<boolean>(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [formFieldErrors, setFormFieldErrors] = useState<Record<string, string>>({});

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Fetch active categories with full branch support
  const fetchCategories = useCallback(async () => {
    if (!user) return;
    setCategoriesLoading(true);
    try {
      const catRes = await adminApi.getJewelleryCategories({ isActive: true, pageSize: 100 });
      setCategories(catRes.items || []);
    } catch (err) {
      console.error("Failed to load active jewellery categories", err);
    } finally {
      setCategoriesLoading(false);
    }
  }, [user]);

  // Load initial branch and categories once user is authenticated
  useEffect(() => {
    if (!user) return;

    if (isSuperAdmin) {
      adminApi.getBranches().then((branchRes) => {
        setBranches(branchRes);
      }).catch((err) => {
        console.error("Failed to load branches", err);
      });
    }

    fetchCategories();
    setFormStartDate(new Date().toISOString().split("T")[0]);
  }, [user, isSuperAdmin, fetchCategories]);

  // Load plans
  const fetchPlans = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    setError(null);
    try {
      const res = await adminApi.getJewelleryPlans({
        page: currentPage,
        pageSize,
        search: searchTerm.trim() || undefined,
        categoryId: selectedCategoryFilter !== "ALL" ? selectedCategoryFilter : undefined,
        status: statusFilter !== "ALL" ? statusFilter : undefined,
        branchId: selectedBranchId !== "ALL" ? selectedBranchId : undefined,
      });

      setPlans(res.items);
      setTotalCount(res.totalCount);
      setTotalPages(res.totalPages || 1);
      setActiveCount(res.activeCount);
      setScheduledCount(res.scheduledCount);
      setDeactivatedCount(res.deactivatedCount);
    } catch (err: any) {
      console.error("Error fetching jewellery plans:", err);
      setError(err?.message || "Failed to load jewellery plans. Please try again.");
    } finally {
      setLoading(false);
    }
  }, [user, currentPage, pageSize, searchTerm, selectedCategoryFilter, statusFilter, selectedBranchId]);

  useEffect(() => {
    if (user) {
      fetchPlans();
    }
    // Close any open popovers on filter change
    setActiveMenuId(null);
  }, [user, fetchPlans]);

  // Debounced search
  useEffect(() => {
    const handler = setTimeout(() => {
      setCurrentPage(1);
      if (user) fetchPlans();
    }, 350);
    return () => clearTimeout(handler);
  }, [searchTerm, user, fetchPlans]);

  // Load customers for selected plan
  const fetchPlanCustomers = async (planId: string, page = 1, search = "") => {
    setCustomersLoading(true);
    try {
      const res = await adminApi.getJewelleryPlanCustomers(planId, {
        page,
        pageSize: 8,
        search: search.trim() || undefined,
      });
      setPlanCustomersData(res);
    } catch (err: any) {
      console.error("Failed to fetch plan customers", err);
    } finally {
      setCustomersLoading(false);
    }
  };

  useEffect(() => {
    if (viewCustomersPlan) {
      fetchPlanCustomers(viewCustomersPlan.id, customersPage, customersSearch);
    }
  }, [viewCustomersPlan, customersPage]);

  const handleCustomersSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (viewCustomersPlan) {
      setCustomersPage(1);
      fetchPlanCustomers(viewCustomersPlan.id, 1, customersSearch);
    }
  };

  // Close popover when clicking outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as HTMLElement;
      if (!target.closest(".action-menu-container")) {
        setActiveMenuId(null);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  // Duration toggle helper
  const handleToggleDuration = (months: number) => {
    setFormDurations((prev) => {
      if (prev.includes(months)) {
        if (prev.length === 1) return prev; // Keep at least one
        return prev.filter((m) => m !== months);
      } else {
        return [...prev, months].sort((a, b) => a - b);
      }
    });
  };

  const handleAddCustomMonth = () => {
    const val = parseInt(customMonthInput.trim(), 10);
    if (isNaN(val) || val <= 0) return;
    if (!formDurations.includes(val)) {
      setFormDurations((prev) => [...prev, val].sort((a, b) => a - b));
    }
    setCustomMonthInput("");
  };

  const handleRemoveDuration = (months: number) => {
    if (formDurations.length <= 1) return;
    setFormDurations((prev) => prev.filter((m) => m !== months));
  };

  // Image upload handling
  const handleImageFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!["image/png", "image/jpeg", "image/jpg", "image/webp"].includes(file.type)) {
      setFormFieldErrors((prev) => ({ ...prev, image: "Only PNG, JPG, or WEBP images are supported." }));
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      setFormFieldErrors((prev) => ({ ...prev, image: "Image file size must not exceed 5MB." }));
      return;
    }

    setFormImageFile(file);
    const objectUrl = URL.createObjectURL(file);
    setFormImagePreview(objectUrl);
    setFormFieldErrors((prev) => {
      const next = { ...prev };
      delete next.image;
      return next;
    });

    // Auto upload to Supabase storage
    setFormUploading(true);
    try {
      const uploadRes = await adminApi.uploadJewelleryPlanImage(file);
      setFormImageUrl(uploadRes.url);
    } catch (err: any) {
      setFormFieldErrors((prev) => ({
        ...prev,
        image: err?.message || "Failed to upload jewellery image to storage.",
      }));
    } finally {
      setFormUploading(false);
    }
  };

  // Open Create Modal
  const openCreateModal = async () => {
    let defaultBranchId = "";
    if (selectedBranchId && selectedBranchId !== "ALL") {
      defaultBranchId = selectedBranchId;
    } else if (user?.assignedBranches?.[0]?.branchId) {
      defaultBranchId = user.assignedBranches[0].branchId;
    } else if (branches.length > 0) {
      defaultBranchId = branches[0].id;
    }

    setFormBranchId(defaultBranchId);

    // Refresh active categories directly from API to ensure freshest database list
    let latestCats = categories;
    try {
      const catRes = await adminApi.getJewelleryCategories({ isActive: true, pageSize: 100 });
      if (catRes.items && catRes.items.length > 0) {
        latestCats = catRes.items;
        setCategories(catRes.items);
      }
    } catch (err) {
      console.error("Failed to refresh categories on openCreateModal:", err);
    }

    // Match categories for target branch (case-insensitive UUID comparison)
    const matching = latestCats.filter(
      (c) => !defaultBranchId || defaultBranchId === "ALL" || c.branchId?.toLowerCase() === defaultBranchId.toLowerCase()
    );

    if (matching.length > 0) {
      setFormCategoryId(matching[0].id);
    } else if (latestCats.length > 0 && isSuperAdmin) {
      // If default branch has no categories but another branch does, intelligently point to that branch
      const firstBranchWithCats = latestCats[0].branchId;
      if (firstBranchWithCats) {
        setFormBranchId(firstBranchWithCats);
        setFormCategoryId(latestCats[0].id);
      } else {
        setFormCategoryId("");
      }
    } else {
      setFormCategoryId("");
    }

    setFormName("");
    setFormDescription("");
    setFormImageUrl("");
    setFormDurations([6, 12, 18]);
    setCustomMonthInput("");
    setFormStartDate(new Date().toISOString().split("T")[0]);
    setFormTargetGrams("25");
    setFormImageFile(null);
    setFormImagePreview(null);
    setFormError(null);
    setFormFieldErrors({});
    setShowCreateModal(true);
  };

  // Open Edit Modal
  const openEditModal = (plan: JewelleryPlan) => {
    setEditingPlan(plan);
    setFormBranchId(plan.branchId);
    setFormCategoryId(plan.categoryId || "");
    setFormName(plan.name);
    setFormDescription(plan.description || "");
    setFormImageUrl(plan.imageUrl);
    setFormDurations(plan.allowedDurationsMonths || [6, 12, 18]);
    setCustomMonthInput("");
    setFormStartDate(plan.startDate);
    setFormTargetGrams(plan.targetProductGoldWeightGrams.toString());
    setFormImageFile(null);
    setFormImagePreview(plan.imageUrl || null);
    setFormError(null);
    setFormFieldErrors({});
    setActiveMenuId(null);
    fetchCategories();
  };

  // Branch selector in modal (Super Admin)
  const handleModalBranchChange = (newBranchId: string) => {
    setFormBranchId(newBranchId);
    const matching = categories.filter(
      (c) => !newBranchId || newBranchId === "ALL" || c.branchId?.toLowerCase() === newBranchId.toLowerCase()
    );
    setFormCategoryId(matching[0]?.id || "");
    if (formFieldErrors.categoryId) {
      setFormFieldErrors((prev) => {
        const n = { ...prev };
        delete n.categoryId;
        return n;
      });
    }
  };

  // Submit Create Plan
  const handleCreateSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    const errors: Record<string, string> = {};

    if (!formCategoryId) errors.categoryId = "Please select a category.";
    if (!formName.trim()) errors.name = "Plan name is required.";
    else if (formName.trim().length < 2) errors.name = "Plan name must be at least 2 characters.";

    if (!formImageUrl.trim()) {
      errors.image = "A jewellery product image is required.";
    }

    if (formDurations.length === 0) {
      errors.durations = "At least one duration option is required.";
    }

    if (!formStartDate) {
      errors.startDate = "Start date is required.";
    }

    const gramsVal = parseFloat(formTargetGrams);
    if (isNaN(gramsVal) || gramsVal <= 0) {
      errors.targetGrams = "Target gold weight must be a positive number.";
    }

    if (Object.keys(errors).length > 0) {
      setFormFieldErrors(errors);
      return;
    }

    setFormSubmitting(true);
    try {
      const payload: CreateJewelleryPlanRequest = {
        branchId: isSuperAdmin && formBranchId ? formBranchId : undefined,
        categoryId: formCategoryId,
        name: formName.trim(),
        description: formDescription.trim() || undefined,
        imageUrl: formImageUrl.trim(),
        allowedDurationsMonths: formDurations,
        startDate: formStartDate,
        targetProductGoldWeightGrams: gramsVal,
        karat: 24,
      };

      await adminApi.createJewelleryPlan(payload);
      setShowCreateModal(false);
      setSuccessMessage(`Plan "${formName.trim()}" created successfully!`);
      setTimeout(() => setSuccessMessage(null), 5000);
      fetchPlans();
    } catch (err: any) {
      console.error("Create plan failed:", err);
      setFormError(err?.message || "Failed to create plan. Please verify all fields.");
    } finally {
      setFormSubmitting(false);
    }
  };

  // Submit Edit Plan
  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingPlan) return;
    setFormError(null);
    const errors: Record<string, string> = {};

    if (!formCategoryId) errors.categoryId = "Please select a category.";
    if (!formName.trim()) errors.name = "Plan name is required.";
    if (!formImageUrl.trim()) errors.image = "A jewellery image is required.";
    if (formDurations.length === 0) errors.durations = "At least one duration option is required.";
    if (!formStartDate) errors.startDate = "Start date is required.";

    const gramsVal = parseFloat(formTargetGrams);
    if (isNaN(gramsVal) || gramsVal <= 0) {
      errors.targetGrams = "Target gold weight must be a positive number.";
    }

    if (Object.keys(errors).length > 0) {
      setFormFieldErrors(errors);
      return;
    }

    setFormSubmitting(true);
    try {
      const payload: UpdateJewelleryPlanRequest = {
        categoryId: formCategoryId,
        name: formName.trim(),
        description: formDescription.trim() || undefined,
        imageUrl: formImageUrl.trim(),
        allowedDurationsMonths: formDurations,
        startDate: formStartDate,
        targetProductGoldWeightGrams: gramsVal,
        karat: 24,
      };

      await adminApi.updateJewelleryPlan(editingPlan.id, payload);
      setEditingPlan(null);
      setSuccessMessage(`Plan "${formName.trim()}" updated successfully!`);
      setTimeout(() => setSuccessMessage(null), 5000);
      fetchPlans();
    } catch (err: any) {
      console.error("Update plan failed:", err);
      setFormError(err?.message || "Failed to update plan. Please verify all fields.");
    } finally {
      setFormSubmitting(false);
    }
  };

  // Submit Status Change (Deactivate / Reopen)
  const handleStatusSubmit = async () => {
    if (!statusActionPlan) return;
    setStatusLoading(true);
    try {
      await adminApi.updateJewelleryPlanStatus(statusActionPlan.plan.id, {
        action: statusActionPlan.action,
        reason: statusReason.trim() || undefined,
      });

      const actionText = statusActionPlan.action === "Deactivate" ? "deactivated" : "reopened";
      setSuccessMessage(`Plan "${statusActionPlan.plan.name}" has been ${actionText}.`);
      setTimeout(() => setSuccessMessage(null), 5000);
      setStatusActionPlan(null);
      setStatusReason("");
      fetchPlans();
    } catch (err: any) {
      setError(err?.message || "Failed to update plan status.");
    } finally {
      setStatusLoading(false);
    }
  };

  // Filter categories matching branch with case-insensitive UUID comparison
  const activeCategoriesForBranch = useMemo(() => {
    if (!formBranchId || formBranchId === "ALL") return categories;
    return categories.filter((c) => c.branchId?.toLowerCase() === formBranchId.toLowerCase());
  }, [categories, formBranchId]);

  // Category filter pills on top toolbar: deduplicated by name if All Branches, or scoped to selected branch
  const displayedCategoryPills = useMemo(() => {
    if (!selectedBranchId || selectedBranchId === "ALL") {
      const seen = new Set<string>();
      return categories.filter((c) => {
        const lower = c.name.trim().toLowerCase();
        if (seen.has(lower)) return false;
        seen.add(lower);
        return true;
      });
    }
    return categories.filter((c) => c.branchId?.toLowerCase() === selectedBranchId.toLowerCase());
  }, [categories, selectedBranchId]);

  return (
    <AdminLayout>
      <div className="space-y-6 max-w-7xl mx-auto pb-12">
        {/* Top Header & Category Filter Pills */}
        <div className="space-y-4">
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center shadow-md">
                <Gem className="w-6 h-6" />
              </div>
              <div>
                <h1 className="text-2xl font-bold tracking-tight text-gray-900">
                  Jewellery Plans
                </h1>
                <p className="text-sm text-gray-500">
                  Manage fixed gold weight savings plans, durations, and enrolled customer balances.
                </p>
              </div>
            </div>

            {/* Branch selector if Super Admin */}
            {isSuperAdmin && branches.length > 0 && (
              <div className="flex items-center gap-2 self-start sm:self-auto bg-white px-3 py-1.5 rounded-xl border border-gray-200 shadow-sm">
                <Building2 className="w-4 h-4 text-emerald-800" />
                <span className="text-xs font-semibold text-gray-600">Branch:</span>
                <select
                  value={selectedBranchId}
                  onChange={(e) => {
                    setSelectedBranchId(e.target.value);
                    setCurrentPage(1);
                  }}
                  className="text-xs font-medium bg-transparent border-none text-gray-800 focus:outline-none cursor-pointer"
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
          </div>

          {/* Horizontal Category Filter Pills (matches Screenshot 1) */}
          <div className="flex items-center gap-2 overflow-x-auto pb-1 scrollbar-none">
            <button
              type="button"
              onClick={() => {
                setSelectedCategoryFilter("ALL");
                setCurrentPage(1);
              }}
              className={`px-4 py-2 rounded-full text-xs font-bold transition-all whitespace-nowrap shadow-sm cursor-pointer ${
                selectedCategoryFilter === "ALL"
                  ? "bg-[#0B3D0B] text-white shadow-emerald-950/20"
                  : "bg-white text-gray-700 hover:bg-gray-100 border border-gray-200"
              }`}
            >
              All Categories
            </button>
            {displayedCategoryPills.map((cat) => (
              <button
                key={cat.id}
                type="button"
                onClick={() => {
                  setSelectedCategoryFilter(cat.id);
                  setCurrentPage(1);
                }}
                className={`px-4 py-2 rounded-full text-xs font-bold transition-all whitespace-nowrap shadow-sm cursor-pointer ${
                  selectedCategoryFilter === cat.id
                    ? "bg-[#0B3D0B] text-white shadow-emerald-950/20"
                    : "bg-white text-gray-700 hover:bg-gray-100 border border-gray-200"
                }`}
              >
                {cat.name}
              </button>
            ))}
          </div>
        </div>

        {/* Global Notifications */}
        {successMessage && (
          <div className="flex items-center gap-3 p-4 bg-emerald-50 border border-emerald-200 rounded-xl text-emerald-900 text-sm animate-fadeIn">
            <CheckCircle2 className="w-5 h-5 text-emerald-700 flex-shrink-0" />
            <span className="font-medium">{successMessage}</span>
          </div>
        )}

        {error && (
          <div className="flex items-center justify-between p-4 bg-red-50 border border-red-200 rounded-xl text-red-900 text-sm">
            <div className="flex items-center gap-3">
              <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
            <button
              onClick={fetchPlans}
              className="text-xs font-semibold text-red-700 hover:text-red-900 underline flex items-center gap-1 cursor-pointer"
            >
              <RefreshCw className="w-3.5 h-3.5" /> Retry
            </button>
          </div>
        )}

        {/* Toolbar & Subheader */}
        <div className="bg-white p-4 sm:p-5 rounded-2xl border border-gray-200/90 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex flex-col">
            <span className="text-xs font-bold text-amber-600 tracking-wider uppercase">
              Total Plans {totalCount}
            </span>
            <h2 className="text-lg font-bold text-gray-900">
              {selectedCategoryFilter === "ALL"
                ? "All Jewellery Plans"
                : categories.find((c) => c.id === selectedCategoryFilter)?.name + " Plans"}
            </h2>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            {/* Search Input */}
            <div className="relative min-w-[220px] flex-1 sm:flex-initial">
              <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                type="text"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                placeholder="Search plans..."
                className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-gray-200 bg-gray-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
              />
              {searchTerm && (
                <button
                  onClick={() => setSearchTerm("")}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              )}
            </div>

            {/* Status Filter Tabs / Dropdown */}
            <div className="flex items-center bg-gray-100 p-1 rounded-xl text-xs font-semibold text-gray-600">
              <button
                type="button"
                onClick={() => setStatusFilter("ALL")}
                className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer ${
                  statusFilter === "ALL" ? "bg-white text-gray-900 shadow-sm font-bold" : "hover:text-gray-900"
                }`}
              >
                All
              </button>
              <button
                type="button"
                onClick={() => setStatusFilter("Active")}
                className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer ${
                  statusFilter === "Active" ? "bg-white text-emerald-800 shadow-sm font-bold" : "hover:text-gray-900"
                }`}
              >
                Active ({activeCount})
              </button>
              <button
                type="button"
                onClick={() => setStatusFilter("Scheduled")}
                className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer ${
                  statusFilter === "Scheduled" ? "bg-white text-blue-800 shadow-sm font-bold" : "hover:text-gray-900"
                }`}
              >
                Scheduled ({scheduledCount})
              </button>
              <button
                type="button"
                onClick={() => setStatusFilter("Deactivated")}
                className={`px-3 py-1.5 rounded-lg transition-all cursor-pointer ${
                  statusFilter === "Deactivated" ? "bg-white text-red-800 shadow-sm font-bold" : "hover:text-gray-900"
                }`}
              >
                Deactivated ({deactivatedCount})
              </button>
            </div>

            {/* Add New Plan Button */}
            <button
              type="button"
              onClick={openCreateModal}
              className="inline-flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-bold text-white bg-[#0B3D0B] hover:bg-[#082e08] transition-all shadow-md hover:shadow-lg cursor-pointer"
            >
              <Plus className="w-4 h-4 text-[#FFD700]" />
              <span>Add New Plan</span>
            </button>
          </div>
        </div>

        {/* Content State: Loading, Empty, Grid */}
        {loading ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 animate-pulse">
            {[1, 2, 3, 4, 5, 6].map((i) => (
              <div key={i} className="bg-white rounded-2xl p-6 border border-gray-200 h-72 flex flex-col justify-between">
                <div className="space-y-3">
                  <div className="h-5 bg-gray-200 rounded w-2/3" />
                  <div className="h-3 bg-gray-200 rounded w-1/3" />
                  <div className="h-2 bg-gray-200 rounded w-full mt-6" />
                </div>
                <div className="h-10 bg-gray-200 rounded-xl" />
              </div>
            ))}
          </div>
        ) : plans.length === 0 ? (
          <div className="bg-white rounded-2xl border border-gray-200 p-12 text-center space-y-4 shadow-sm">
            <div className="w-16 h-16 rounded-full bg-emerald-50 text-[#0B3D0B] flex items-center justify-center mx-auto">
              <Gem className="w-8 h-8 text-[#0B3D0B]" />
            </div>
            <h3 className="text-base font-bold text-gray-900">No Jewellery Plans Found</h3>
            <p className="text-xs text-gray-500 max-w-md mx-auto">
              {searchTerm || selectedCategoryFilter !== "ALL" || statusFilter !== "ALL"
                ? "No plans match your current search and filter criteria. Try resetting filters."
                : "No plans have been created yet. Click 'Add New Plan' to set up your first jewellery savings plan."}
            </p>
            {(searchTerm || selectedCategoryFilter !== "ALL" || statusFilter !== "ALL") && (
              <button
                type="button"
                onClick={() => {
                  setSearchTerm("");
                  setSelectedCategoryFilter("ALL");
                  setStatusFilter("ALL");
                }}
                className="text-xs font-bold text-[#0B3D0B] underline cursor-pointer"
              >
                Clear all filters
              </button>
            )}
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {plans.map((plan) => {
              const isScheduled = plan.displayStatus === "Scheduled";
              const isDeactivated = plan.displayStatus === "Deactivated";

              return (
                <div
                  key={plan.id}
                  className="bg-white rounded-2xl border border-gray-200 shadow-sm hover:shadow-md transition-all duration-200 flex flex-col justify-between p-5 sm:p-6 relative group"
                >
                  {/* Card Header: Title & Status Badge */}
                  <div>
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <h3 className="font-bold text-gray-900 text-base leading-snug tracking-tight">
                          {plan.name}
                        </h3>
                        <span className="text-[11px] font-bold text-gray-400 tracking-wider uppercase">
                          {plan.categoryName || "General"}
                        </span>
                      </div>

                      {/* Status badge */}
                      <span
                        className={`text-[11px] font-bold px-2.5 py-1 rounded-full whitespace-nowrap ${
                          isDeactivated
                            ? "bg-red-50 text-red-700 border border-red-200"
                            : isScheduled
                            ? "bg-blue-50 text-blue-700 border border-blue-200"
                            : "bg-emerald-50 text-emerald-800 border border-emerald-200"
                        }`}
                      >
                        {plan.displayStatus}
                      </span>
                    </div>

                    {/* Image Thumbnail (if available) */}
                    {plan.imageUrl ? (
                      <div className="mt-4 relative w-full h-32 rounded-xl overflow-hidden bg-gray-100 border border-gray-100">
                        <img
                          src={plan.imageUrl}
                          alt={plan.name}
                          className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                          onError={(e) => {
                            (e.target as HTMLElement).style.display = "none";
                          }}
                        />
                      </div>
                    ) : null}

                    {/* PLAN GOLD PROGRESS Bar */}
                    <div className="mt-5 space-y-2">
                      <div className="flex items-center justify-between text-[11px] font-bold">
                        <span className="text-gray-400 tracking-wider">PLAN GOLD PROGRESS</span>
                        <span className="text-gray-900">{plan.overallProgressPercentage}%</span>
                      </div>
                      <div className="w-full bg-gray-100 rounded-full h-2 overflow-hidden">
                        <div
                          className="bg-[#FFD700] h-2 rounded-full transition-all duration-500"
                          style={{ width: `${Math.min(100, Math.max(0, plan.overallProgressPercentage))}%` }}
                        />
                      </div>
                    </div>

                    {/* Details Two-Column Grid (matches Screenshot 1) */}
                    <div className="mt-5 pt-4 border-t border-gray-100 grid grid-cols-2 gap-4 text-xs">
                      <div className="space-y-1">
                        <div className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">
                          DURATION
                        </div>
                        <div className="font-bold text-gray-800">
                          {plan.allowedDurationsMonths?.length > 0
                            ? plan.allowedDurationsMonths.join(" / ") + " months"
                            : "Flexible"}
                        </div>
                      </div>

                      <div className="space-y-1">
                        <div className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">
                          PLAN GOLD
                        </div>
                        <div className="font-bold text-gray-800">
                          {plan.targetProductGoldWeightGrams} g
                        </div>
                      </div>

                      <div className="space-y-1 col-span-2">
                        <div className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">
                          START DATE
                        </div>
                        <div className="font-semibold text-gray-700 flex items-center gap-1.5">
                          <Calendar className="w-3.5 h-3.5 text-gray-400" />
                          <span>{plan.startDate}</span>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Card Actions Footer (matches Screenshot 1) */}
                  <div className="mt-6 pt-4 border-t border-gray-100 flex items-center gap-2">
                    <button
                      type="button"
                      onClick={() => {
                        setViewCustomersPlan(plan);
                        setCustomersPage(1);
                        setCustomersSearch("");
                      }}
                      className="flex-1 py-2.5 px-3 rounded-xl bg-[#0B3D0B] text-white hover:bg-[#082e08] text-xs font-bold transition-all shadow-sm flex items-center justify-center gap-1.5 cursor-pointer"
                    >
                      <Users className="w-3.5 h-3.5 text-[#FFD700]" />
                      <span>View Customers</span>
                    </button>

                    <button
                      type="button"
                      onClick={() => openEditModal(plan)}
                      className="py-2.5 px-3 rounded-xl border border-gray-200 text-gray-700 hover:bg-gray-50 text-xs font-bold transition-all cursor-pointer"
                    >
                      Edit
                    </button>

                    {/* Three-dot menu button */}
                    <div className="relative action-menu-container">
                      <button
                        type="button"
                        onClick={() =>
                          setActiveMenuId((prev) => (prev === plan.id ? null : plan.id))
                        }
                        className="p-2.5 rounded-xl border border-gray-200 text-gray-600 hover:bg-gray-50 transition-all cursor-pointer"
                        aria-label="More actions"
                      >
                        <MoreVertical className="w-4 h-4" />
                      </button>

                      {activeMenuId === plan.id && (
                        <div className="absolute right-0 bottom-full mb-2 w-44 bg-white rounded-xl shadow-xl border border-gray-200 p-1.5 z-20 animate-fadeIn">
                          {plan.isActive ? (
                            <button
                              type="button"
                              onClick={() => {
                                setStatusActionPlan({ plan, action: "Deactivate" });
                                setStatusReason("");
                                setActiveMenuId(null);
                              }}
                              className="w-full text-left px-3 py-2 text-xs font-bold text-red-600 hover:bg-red-50 rounded-lg flex items-center gap-2 transition-all cursor-pointer"
                            >
                              <Power className="w-3.5 h-3.5" />
                              <span>Deactivate Plan</span>
                            </button>
                          ) : (
                            <button
                              type="button"
                              onClick={() => {
                                setStatusActionPlan({ plan, action: "Reopen" });
                                setStatusReason("");
                                setActiveMenuId(null);
                              }}
                              className="w-full text-left px-3 py-2 text-xs font-bold text-emerald-700 hover:bg-emerald-50 rounded-lg flex items-center gap-2 transition-all cursor-pointer"
                            >
                              <RefreshCw className="w-3.5 h-3.5" />
                              <span>Reopen Plan</span>
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between bg-white px-4 py-3 rounded-2xl border border-gray-200 text-xs">
            <span className="text-gray-500">
              Showing page <strong className="text-gray-800">{currentPage}</strong> of{" "}
              <strong className="text-gray-800">{totalPages}</strong> ({totalCount} total)
            </span>
            <div className="flex items-center gap-2">
              <button
                disabled={currentPage <= 1}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 rounded-lg border border-gray-200 disabled:opacity-40 hover:bg-gray-50 transition-all font-semibold flex items-center gap-1 cursor-pointer"
              >
                <ChevronLeft className="w-3.5 h-3.5" /> Prev
              </button>
              <button
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                className="px-3 py-1.5 rounded-lg border border-gray-200 disabled:opacity-40 hover:bg-gray-50 transition-all font-semibold flex items-center gap-1 cursor-pointer"
              >
                Next <ChevronRight className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        )}

        {/* ========================================================================= */}
        {/* CREATE / EDIT PLAN MODAL (matches Screenshot 2 layout)                   */}
        {/* ========================================================================= */}
        {(showCreateModal || editingPlan) && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs animate-fadeIn overflow-y-auto">
            <div className="bg-white rounded-3xl shadow-2xl border border-gray-100 w-full max-w-xl overflow-hidden my-8">
              {/* Modal Header */}
              <div className="px-6 py-5 border-b border-gray-100 flex items-center justify-between bg-[#FAF9F6]">
                <div className="flex items-center gap-2.5">
                  <div className="w-8 h-8 rounded-lg bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center">
                    <Gem className="w-4 h-4" />
                  </div>
                  <h3 className="font-bold text-gray-900 text-lg">
                    {editingPlan ? "Edit Jewellery Plan" : "Add New Plan"}
                  </h3>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    setShowCreateModal(false);
                    setEditingPlan(null);
                  }}
                  className="w-8 h-8 rounded-full bg-gray-200/60 hover:bg-gray-200 flex items-center justify-center text-gray-600 transition-all cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              {/* Form Content */}
              <form onSubmit={editingPlan ? handleEditSubmit : handleCreateSubmit} className="p-6 space-y-5">
                {formError && (
                  <div className="p-3 bg-red-50 border border-red-200 rounded-xl text-red-900 text-xs flex items-center gap-2">
                    <AlertCircle className="w-4 h-4 text-red-600 flex-shrink-0" />
                    <span>{formError}</span>
                  </div>
                )}

                {/* Edit Enrolment Warning Banner */}
                {editingPlan && editingPlan.totalEnrolments > 0 && (
                  <div className="p-3 bg-amber-50 border border-amber-200 rounded-xl text-amber-900 text-xs flex items-start gap-2.5">
                    <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5" />
                    <div>
                      <strong className="block">Active Customer Enrolments Detected</strong>
                      Existing customers will retain their agreed target grams, selected duration, and deadlines.
                      Changes made here will apply only to new enrolments joining in the future.
                    </div>
                  </div>
                )}

                {/* Branch Selection (Super Admin only on Create) */}
                {isSuperAdmin && !editingPlan && branches.length > 0 && (
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Branch <span className="text-red-500">*</span>
                    </label>
                    <select
                      value={formBranchId}
                      onChange={(e) => handleModalBranchChange(e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
                    >
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.name} ({b.code})
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                {/* Category Dropdown */}
                <div>
                  <div className="flex items-center justify-between mb-1.5">
                    <label className="block text-xs font-bold text-gray-700">
                      Category <span className="text-red-500">*</span>
                    </label>
                    {categoriesLoading && (
                      <span className="text-[11px] text-gray-400 animate-pulse">Loading categories...</span>
                    )}
                  </div>
                  <select
                    value={formCategoryId}
                    onChange={(e) => {
                      setFormCategoryId(e.target.value);
                      if (formFieldErrors.categoryId) {
                        setFormFieldErrors((prev) => {
                          const n = { ...prev };
                          delete n.categoryId;
                          return n;
                        });
                      }
                    }}
                    className={`w-full px-3.5 py-2.5 rounded-xl border text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B] ${
                      formFieldErrors.categoryId ? "border-red-400 bg-red-50/20" : "border-gray-200"
                    }`}
                  >
                    <option value="">Select a category</option>
                    {activeCategoriesForBranch.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                  {activeCategoriesForBranch.length === 0 && !categoriesLoading && (
                    <div className="mt-2 p-2.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 text-[11px] flex items-center justify-between">
                      <span>No active categories found for this branch.</span>
                      <Link
                        href="/jewellery-plans/categories"
                        className="font-bold underline text-[#0B3D0B] hover:text-emerald-950 ml-2"
                      >
                        Create Category →
                      </Link>
                    </div>
                  )}
                  {formFieldErrors.categoryId && (
                    <p className="mt-1 text-[11px] text-red-600">{formFieldErrors.categoryId}</p>
                  )}
                </div>

                {/* Plan Name Input */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1.5">
                    Plan Name <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="text"
                    value={formName}
                    onChange={(e) => {
                      setFormName(e.target.value);
                      if (formFieldErrors.name) {
                        setFormFieldErrors((prev) => {
                          const n = { ...prev };
                          delete n.name;
                          return n;
                        });
                      }
                    }}
                    placeholder="e.g. Bridal Gold Saver"
                    className={`w-full px-3.5 py-2.5 rounded-xl border text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B] ${
                      formFieldErrors.name ? "border-red-400 bg-red-50/20" : "border-gray-200"
                    }`}
                  />
                  {formFieldErrors.name && (
                    <p className="mt-1 text-[11px] text-red-600">{formFieldErrors.name}</p>
                  )}
                </div>

                {/* Duration Options (Months) (matches Screenshot 2 layout) */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1.5">
                    Duration Options (Months) <span className="text-red-500">*</span>
                  </label>

                  {/* Standard toggle chips: 6, 12, 18 months */}
                  <div className="flex flex-wrap items-center gap-2 mb-2.5">
                    {[6, 12, 18].map((month) => {
                      const isSelected = formDurations.includes(month);
                      return (
                        <button
                          key={month}
                          type="button"
                          onClick={() => handleToggleDuration(month)}
                          className={`px-3.5 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                            isSelected
                              ? "bg-[#FFD700] text-[#0B3D0B] shadow-xs"
                              : "bg-gray-100 text-gray-600 hover:bg-gray-200 border border-gray-200"
                          }`}
                        >
                          {month} months
                        </button>
                      );
                    })}
                  </div>

                  {/* Custom months input row */}
                  <div className="flex items-center gap-2 mb-2">
                    <input
                      type="number"
                      min="1"
                      step="1"
                      value={customMonthInput}
                      onChange={(e) => setCustomMonthInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          e.preventDefault();
                          handleAddCustomMonth();
                        }
                      }}
                      placeholder="Add custom months (e.g. 9)"
                      className="flex-1 px-3.5 py-2 rounded-xl border border-gray-200 text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
                    />
                    <button
                      type="button"
                      onClick={handleAddCustomMonth}
                      className="px-3.5 py-2 rounded-xl bg-gray-100 hover:bg-gray-200 text-gray-800 text-xs font-bold transition-all border border-gray-200 cursor-pointer"
                    >
                      + Add Month
                    </button>
                  </div>

                  {/* Selected Duration Chips list */}
                  <div className="flex flex-wrap items-center gap-1.5 mt-2">
                    {formDurations.map((m) => (
                      <span
                        key={m}
                        className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-amber-50 text-amber-900 border border-amber-200 text-xs font-bold"
                      >
                        <span>{m} mo</span>
                        <button
                          type="button"
                          onClick={() => handleRemoveDuration(m)}
                          className="hover:text-red-700 transition-colors"
                          title="Remove duration"
                        >
                          <X className="w-3 h-3" />
                        </button>
                      </span>
                    ))}
                  </div>
                  <p className="mt-1 text-[11px] text-gray-400">
                    Select one or more duration options for this plan.
                  </p>
                  {formFieldErrors.durations && (
                    <p className="mt-1 text-[11px] text-red-600">{formFieldErrors.durations}</p>
                  )}
                </div>

                {/* Two-Column: Start Date & Plan Gold (grams) */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Start Date <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="date"
                      value={formStartDate}
                      onChange={(e) => setFormStartDate(e.target.value)}
                      className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
                    />
                    <p className="mt-1 text-[10px] text-gray-400">
                      Date this plan becomes available for enrolment.
                    </p>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-gray-700 mb-1.5">
                      Plan Gold (grams) <span className="text-red-500">*</span>
                    </label>
                    <input
                      type="number"
                      step="0.0001"
                      min="0.0001"
                      value={formTargetGrams}
                      onChange={(e) => {
                        setFormTargetGrams(e.target.value);
                        if (formFieldErrors.targetGrams) {
                          setFormFieldErrors((prev) => {
                            const n = { ...prev };
                            delete n.targetGrams;
                            return n;
                          });
                        }
                      }}
                      placeholder="e.g. 25"
                      className={`w-full px-3.5 py-2.5 rounded-xl border text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B] ${
                        formFieldErrors.targetGrams ? "border-red-400 bg-red-50/20" : "border-gray-200"
                      }`}
                    />
                    {formFieldErrors.targetGrams && (
                      <p className="mt-1 text-[11px] text-red-600">{formFieldErrors.targetGrams}</p>
                    )}
                  </div>
                </div>

                {/* Jewellery Image Upload Dropzone */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1.5">
                    Jewellery Product Image <span className="text-red-500">*</span>
                  </label>
                  <input
                    type="file"
                    ref={fileInputRef}
                    onChange={handleImageFileChange}
                    accept="image/png,image/jpeg,image/jpg,image/webp"
                    className="hidden"
                  />

                  {formImagePreview ? (
                    <div className="relative rounded-2xl border border-gray-200 overflow-hidden bg-gray-50 p-2 flex items-center justify-between">
                      <div className="flex items-center gap-3">
                        <img
                          src={formImagePreview}
                          alt="Jewellery Preview"
                          className="w-16 h-16 rounded-xl object-cover border border-gray-200"
                        />
                        <div>
                          <span className="text-xs font-bold text-gray-800 block">
                            {formImageFile ? formImageFile.name : "Plan Image"}
                          </span>
                          <span className="text-[11px] text-emerald-700 font-semibold flex items-center gap-1">
                            <CheckCircle2 className="w-3.5 h-3.5" /> Ready
                          </span>
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() => fileInputRef.current?.click()}
                        className="px-3 py-1.5 rounded-xl border border-gray-200 hover:bg-gray-100 text-xs font-bold text-gray-700 transition-all cursor-pointer"
                      >
                        Change
                      </button>
                    </div>
                  ) : (
                    <div
                      onClick={() => fileInputRef.current?.click()}
                      className={`border-2 border-dashed rounded-2xl p-6 text-center hover:bg-gray-50 transition-all cursor-pointer ${
                        formFieldErrors.image ? "border-red-300 bg-red-50/10" : "border-gray-200"
                      }`}
                    >
                      <UploadCloud className="w-8 h-8 text-[#0B3D0B] mx-auto mb-2" />
                      <span className="text-xs font-bold text-gray-800 block">
                        Upload Jewellery Image
                      </span>
                      <span className="text-[11px] text-gray-400 block mt-0.5">
                        PNG, JPG, or WEBP (Max 5MB)
                      </span>
                      {formUploading && (
                        <div className="mt-2 text-xs text-emerald-800 font-semibold animate-pulse">
                          Uploading image to storage...
                        </div>
                      )}
                    </div>
                  )}
                  {formFieldErrors.image && (
                    <p className="mt-1 text-[11px] text-red-600">{formFieldErrors.image}</p>
                  )}
                </div>

                {/* Description (Optional) */}
                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1.5">
                    Description (optional)
                  </label>
                  <textarea
                    rows={2}
                    value={formDescription}
                    onChange={(e) => setFormDescription(e.target.value)}
                    placeholder="Provide details about craftsmanship or product highlights..."
                    className="w-full px-3.5 py-2.5 rounded-xl border border-gray-200 text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
                  />
                </div>

                {/* Modal Actions */}
                <div className="pt-4 border-t border-gray-100 flex items-center justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => {
                      setShowCreateModal(false);
                      setEditingPlan(null);
                    }}
                    className="px-4 py-2.5 rounded-xl border border-gray-200 text-gray-700 hover:bg-gray-50 text-xs font-bold transition-all cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={formSubmitting || formUploading}
                    className="px-5 py-2.5 rounded-xl bg-[#0B3D0B] text-white hover:bg-[#082e08] disabled:opacity-50 text-xs font-bold transition-all shadow-md cursor-pointer flex items-center gap-1.5"
                  >
                    {formSubmitting && (
                      <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    )}
                    <span>{editingPlan ? "Save Changes" : "Create Plan"}</span>
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ========================================================================= */}
        {/* STATUS TRANSITION MODAL (Deactivate / Reopen)                             */}
        {/* ========================================================================= */}
        {statusActionPlan && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl shadow-2xl border border-gray-100 w-full max-w-md overflow-hidden">
              <div className="p-6 space-y-4">
                <div
                  className={`w-12 h-12 rounded-2xl flex items-center justify-center mx-auto ${
                    statusActionPlan.action === "Deactivate"
                      ? "bg-red-50 text-red-600"
                      : "bg-emerald-50 text-emerald-700"
                  }`}
                >
                  {statusActionPlan.action === "Deactivate" ? (
                    <Power className="w-6 h-6" />
                  ) : (
                    <RefreshCw className="w-6 h-6" />
                  )}
                </div>

                <div className="text-center space-y-1">
                  <h3 className="font-bold text-gray-900 text-lg">
                    {statusActionPlan.action === "Deactivate"
                      ? "Deactivate Jewellery Plan"
                      : "Reopen Jewellery Plan"}
                  </h3>
                  <p className="text-xs text-gray-500">
                    {statusActionPlan.action === "Deactivate"
                      ? `Deactivating "${statusActionPlan.plan.name}" blocks new customer enrolments. Existing customers can continue contributing until their deadline. All customer balances and records are strictly preserved.`
                      : `Reopening "${statusActionPlan.plan.name}" will make it available for new customer enrolments once its start date has arrived.`}
                  </p>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 mb-1">
                    Reason (optional)
                  </label>
                  <input
                    type="text"
                    value={statusReason}
                    onChange={(e) => setStatusReason(e.target.value)}
                    placeholder="Enter reason for this action..."
                    className="w-full px-3.5 py-2 rounded-xl border border-gray-200 text-xs font-medium focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
                  />
                </div>

                <div className="pt-2 flex items-center justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setStatusActionPlan(null)}
                    className="px-4 py-2.5 rounded-xl border border-gray-200 text-gray-700 hover:bg-gray-50 text-xs font-bold transition-all cursor-pointer"
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    disabled={statusLoading}
                    onClick={handleStatusSubmit}
                    className={`px-5 py-2.5 rounded-xl text-xs font-bold text-white transition-all shadow-md cursor-pointer flex items-center gap-1.5 ${
                      statusActionPlan.action === "Deactivate"
                        ? "bg-red-600 hover:bg-red-700"
                        : "bg-[#0B3D0B] hover:bg-[#082e08]"
                    }`}
                  >
                    {statusLoading && (
                      <div className="w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full animate-spin" />
                    )}
                    <span>
                      {statusActionPlan.action === "Deactivate"
                        ? "Confirm Deactivation"
                        : "Confirm Reopen"}
                    </span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ========================================================================= */}
        {/* VIEW CUSTOMERS PANEL / MODAL                                              */}
        {/* ========================================================================= */}
        {viewCustomersPlan && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-xs animate-fadeIn overflow-y-auto">
            <div className="bg-white rounded-3xl shadow-2xl border border-gray-100 w-full max-w-5xl overflow-hidden my-6">
              {/* Header */}
              <div className="px-6 py-5 border-b border-gray-100 flex items-center justify-between bg-[#FAF9F6]">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-[#0B3D0B] text-[#FFD700] flex items-center justify-center">
                    <Users className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="font-bold text-gray-900 text-lg">
                      Enrolled Customers — {viewCustomersPlan.name}
                    </h3>
                    <p className="text-xs text-gray-500">
                      Category: <span className="font-semibold text-gray-700">{viewCustomersPlan.categoryName}</span> | Target:{" "}
                      <span className="font-semibold text-gray-700">{viewCustomersPlan.targetProductGoldWeightGrams} g</span> | Overall Progress:{" "}
                      <span className="font-semibold text-[#0B3D0B]">{viewCustomersPlan.overallProgressPercentage}%</span>
                    </p>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={() => {
                    setViewCustomersPlan(null);
                    setPlanCustomersData(null);
                    setSelectedCustomerContributions(null);
                  }}
                  className="w-8 h-8 rounded-full bg-gray-200/60 hover:bg-gray-200 flex items-center justify-center text-gray-600 transition-all cursor-pointer"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              {/* Sub-toolbar: Search & Count */}
              <div className="p-6 border-b border-gray-100 flex flex-col sm:flex-row items-center justify-between gap-3 bg-white">
                <span className="text-xs font-bold text-gray-600">
                  Total Enrolments: {planCustomersData?.totalCount ?? 0}
                </span>

                <form onSubmit={handleCustomersSearchSubmit} className="relative w-full sm:w-72">
                  <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
                  <input
                    type="text"
                    value={customersSearch}
                    onChange={(e) => setCustomersSearch(e.target.value)}
                    placeholder="Search by customer name, phone, email..."
                    className="w-full pl-9 pr-3 py-2 text-xs rounded-xl border border-gray-200 bg-gray-50 focus:bg-white focus:outline-none focus:ring-2 focus:ring-[#0B3D0B]/20 focus:border-[#0B3D0B]"
                  />
                </form>
              </div>

              {/* Table or Empty state */}
              <div className="overflow-x-auto">
                {customersLoading ? (
                  <div className="py-20 flex flex-col items-center justify-center gap-3">
                    <div className="w-8 h-8 border-3 border-[#0B3D0B] border-t-[#FFD700] rounded-full animate-spin" />
                    <span className="text-xs font-semibold text-gray-500">Loading customer enrolments...</span>
                  </div>
                ) : !planCustomersData || planCustomersData.items.length === 0 ? (
                  <div className="py-16 text-center space-y-2">
                    <Users className="w-10 h-10 text-gray-300 mx-auto" />
                    <h4 className="text-sm font-bold text-gray-800">No Customers Enrolled Yet</h4>
                    <p className="text-xs text-gray-400 max-w-sm mx-auto">
                      There are currently no active customer enrolments for this plan. Customers joining via mobile will appear here with live contribution progress.
                    </p>
                  </div>
                ) : (
                  <table className="w-full text-left border-collapse text-xs">
                    <thead>
                      <tr className="bg-gray-50 text-[11px] font-bold text-gray-500 uppercase tracking-wider border-b border-gray-200">
                        <th className="py-3 px-4">Customer</th>
                        <th className="py-3 px-4">Joining Date</th>
                        <th className="py-3 px-4">Duration & Deadline</th>
                        <th className="py-3 px-4 text-right">Target</th>
                        <th className="py-3 px-4 text-right">Total Paid</th>
                        <th className="py-3 px-4 text-right">Saved Gold</th>
                        <th className="py-3 px-4 text-right">Remaining</th>
                        <th className="py-3 px-4 text-center">Progress</th>
                        <th className="py-3 px-4 text-center">Status</th>
                        <th className="py-3 px-4 text-center">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {planCustomersData.items.map((c) => (
                        <tr key={c.enrolmentId} className="hover:bg-gray-50/60 transition-colors">
                          <td className="py-3.5 px-4">
                            <div className="font-bold text-gray-900">{c.customerName}</div>
                            <div className="text-[11px] text-gray-400 flex items-center gap-1.5 mt-0.5">
                              {c.phoneNumber && <span>{c.phoneNumber}</span>}
                              {c.email && <span>• {c.email}</span>}
                            </div>
                          </td>
                          <td className="py-3.5 px-4 text-gray-700 whitespace-nowrap">
                            {c.joiningDate}
                          </td>
                          <td className="py-3.5 px-4 whitespace-nowrap">
                            <span className="font-semibold text-gray-800 block">
                              {c.selectedDurationMonths} months
                            </span>
                            <span className="text-[11px] text-gray-400">Due: {c.deadline}</span>
                          </td>
                          <td className="py-3.5 px-4 text-right font-bold text-gray-800 whitespace-nowrap">
                            {c.enrolmentTargetGrams} g
                          </td>
                          <td className="py-3.5 px-4 text-right font-semibold text-gray-800 whitespace-nowrap">
                            {c.currency} {c.totalConfirmedMoneyPaid.toLocaleString()}
                          </td>
                          <td className="py-3.5 px-4 text-right font-bold text-emerald-800 whitespace-nowrap">
                            {c.netAccumulatedGrams} g
                          </td>
                          <td className="py-3.5 px-4 text-right text-gray-600 whitespace-nowrap">
                            {c.remainingGrams} g
                          </td>
                          <td className="py-3.5 px-4 text-center whitespace-nowrap">
                            <div className="inline-flex items-center gap-1.5">
                              <div className="w-14 bg-gray-100 rounded-full h-1.5 overflow-hidden">
                                <div
                                  className="bg-[#FFD700] h-1.5 rounded-full"
                                  style={{ width: `${Math.min(100, c.individualProgressPercentage)}%` }}
                                />
                              </div>
                              <span className="text-[11px] font-bold text-gray-700">
                                {c.individualProgressPercentage}%
                              </span>
                            </div>
                          </td>
                          <td className="py-3.5 px-4 text-center whitespace-nowrap">
                            <span
                              className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                                c.enrolmentStatus === "Completed"
                                  ? "bg-emerald-100 text-emerald-800"
                                  : c.enrolmentStatus === "DurationEnded"
                                  ? "bg-amber-100 text-amber-800"
                                  : "bg-blue-50 text-blue-800"
                              }`}
                            >
                              {c.enrolmentStatus}
                            </span>
                          </td>
                          <td className="py-3.5 px-4 text-center whitespace-nowrap">
                            <button
                              type="button"
                              onClick={() => setSelectedCustomerContributions(c)}
                              className="px-2.5 py-1 rounded-lg border border-gray-200 hover:bg-gray-100 text-[11px] font-bold text-gray-700 transition-all flex items-center gap-1 mx-auto cursor-pointer"
                            >
                              <History className="w-3 h-3 text-emerald-800" />
                              <span>Ledger ({c.contributions.length})</span>
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>

              {/* Customers Pagination */}
              {planCustomersData && planCustomersData.totalPages > 1 && (
                <div className="p-4 border-t border-gray-100 flex items-center justify-between text-xs bg-gray-50/50">
                  <span className="text-gray-500">
                    Page {planCustomersData.page} of {planCustomersData.totalPages}
                  </span>
                  <div className="flex items-center gap-2">
                    <button
                      disabled={customersPage <= 1}
                      onClick={() => setCustomersPage((p) => Math.max(1, p - 1))}
                      className="px-2.5 py-1 rounded-lg border border-gray-200 disabled:opacity-40 hover:bg-white text-xs font-semibold"
                    >
                      Prev
                    </button>
                    <button
                      disabled={customersPage >= planCustomersData.totalPages}
                      onClick={() => setCustomersPage((p) => Math.min(planCustomersData.totalPages, p + 1))}
                      className="px-2.5 py-1 rounded-lg border border-gray-200 disabled:opacity-40 hover:bg-white text-xs font-semibold"
                    >
                      Next
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ========================================================================= */}
        {/* CUSTOMER CONTRIBUTIONS LEDGER MODAL                                       */}
        {/* ========================================================================= */}
        {selectedCustomerContributions && (
          <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-black/60 backdrop-blur-xs animate-fadeIn">
            <div className="bg-white rounded-3xl shadow-2xl border border-gray-100 w-full max-w-2xl overflow-hidden">
              <div className="px-6 py-4 border-b border-gray-100 flex items-center justify-between bg-[#FAF9F6]">
                <div>
                  <h4 className="font-bold text-gray-900 text-sm">
                    Contribution Ledger — {selectedCustomerContributions.customerName}
                  </h4>
                  <span className="text-xs text-gray-500">
                    Target: {selectedCustomerContributions.enrolmentTargetGrams} g | Saved:{" "}
                    {selectedCustomerContributions.netAccumulatedGrams} g
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => setSelectedCustomerContributions(null)}
                  className="w-7 h-7 rounded-full bg-gray-200/60 hover:bg-gray-200 flex items-center justify-center text-gray-600"
                >
                  <X className="w-3.5 h-3.5" />
                </button>
              </div>

              <div className="p-6 max-h-96 overflow-y-auto">
                {selectedCustomerContributions.contributions.length === 0 ? (
                  <p className="text-xs text-gray-500 text-center py-8">
                    No contribution transactions recorded for this enrolment yet.
                  </p>
                ) : (
                  <table className="w-full text-left text-xs border-collapse">
                    <thead>
                      <tr className="border-b border-gray-200 text-gray-400 font-bold uppercase text-[10px]">
                        <th className="py-2">Date</th>
                        <th className="py-2 text-right">Amount</th>
                        <th className="py-2 text-right">Credited Gold</th>
                        <th className="py-2 text-right">Applied Rate (24K)</th>
                        <th className="py-2 text-center">Status</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {selectedCustomerContributions.contributions.map((tx) => (
                        <tr key={tx.id}>
                          <td className="py-2.5 font-medium text-gray-700">
                            {new Date(tx.confirmedAtUtc).toLocaleDateString()}
                          </td>
                          <td className="py-2.5 text-right font-semibold text-gray-900">
                            {tx.currency} {tx.currencyAmount.toLocaleString()}
                          </td>
                          <td className="py-2.5 text-right font-bold text-emerald-800">
                            +{tx.goldGrams} g
                          </td>
                          <td className="py-2.5 text-right text-gray-600">
                            {tx.currency} {tx.appliedRatePerGram.toLocaleString()}/g
                          </td>
                          <td className="py-2.5 text-center">
                            <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800">
                              {tx.status}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </AdminLayout>
  );
}
