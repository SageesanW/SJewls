import { Branch, ForgotPasswordResponse, LoginResponse, ResetPasswordResponse, StaffUser } from "../types/auth";

const API_BASE_URL = (process.env.NEXT_PUBLIC_API_URL || "").replace(/\/+$/, "");

export class ApiError extends Error {
  status: number;
  data: any;

  constructor(message: string, status: number, data?: any) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.data = data;
  }
}

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem("sjewls_staff_token");
}

export function setToken(token: string): void {
  if (typeof window === "undefined") return;
  localStorage.setItem("sjewls_staff_token", token);
  // Also store in cookie for SSR route protection if needed
  document.cookie = `sjewls_staff_token=${token}; path=/; max-age=86400; SameSite=Strict`;
}

export function removeToken(): void {
  if (typeof window === "undefined") return;
  localStorage.removeItem("sjewls_staff_token");
  document.cookie = "sjewls_staff_token=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT";
}

async function fetchWithAuth<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const token = getToken();
  const isFormData = typeof FormData !== "undefined" && options.body instanceof FormData;
  const headers: Record<string, string> = {
    ...(isFormData ? {} : { "Content-Type": "application/json" }),
    ...(options.headers as Record<string, string>),
  };

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const url = `${API_BASE_URL}${endpoint}`;
  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (response.status === 401) {
    removeToken();
    if (typeof window !== "undefined" && !window.location.pathname.includes("/login")) {
      window.dispatchEvent(new CustomEvent("sjewls:unauthorized"));
    }
  }

  const responseText = await response.text();
  let data: any = null;
  if (responseText) {
    try {
      data = JSON.parse(responseText);
    } catch {
      data = { message: responseText };
    }
  }

  if (!response.ok) {
    const errorMsg = data?.message || data?.title || `Request failed with status ${response.status}`;
    throw new ApiError(errorMsg, response.status, data);
  }

  return data as T;
}

export const adminApi = {
  // Authentication
  async login(usernameOrEmail: string, password: string): Promise<LoginResponse> {
    const res = await fetchWithAuth<LoginResponse>("/api/v1/admin/auth/login", {
      method: "POST",
      body: JSON.stringify({ usernameOrEmail, password }),
    });
    if (res.accessToken) {
      setToken(res.accessToken);
    }
    return res;
  },

  async logout(): Promise<void> {
    try {
      await fetchWithAuth("/api/v1/admin/auth/logout", {
        method: "POST",
      });
    } finally {
      removeToken();
    }
  },

  async getMe(): Promise<StaffUser> {
    return fetchWithAuth<StaffUser>("/api/v1/admin/auth/me");
  },

  async forgotPassword(email: string): Promise<ForgotPasswordResponse> {
    return fetchWithAuth<ForgotPasswordResponse>("/api/v1/admin/auth/forgot-password", {
      method: "POST",
      body: JSON.stringify({ email }),
    });
  },

  async resetPassword(data: { email: string; token: string; newPassword: string; confirmPassword: string }): Promise<ResetPasswordResponse> {
    return fetchWithAuth<ResetPasswordResponse>("/api/v1/admin/auth/reset-password", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },

  // User Management
  async getUsers(): Promise<StaffUser[]> {
    return fetchWithAuth<StaffUser[]>("/api/v1/admin/users");
  },

  async createUser(data: {
    fullName: string;
    email: string;
    phoneNumber: string;
    password: string;
    username?: string;
    role: string;
    branchId?: string;
  }): Promise<StaffUser> {
    return fetchWithAuth<StaffUser>("/api/v1/admin/users", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },

  async updateUserStatus(id: string, data: { isActive: boolean; reason?: string }): Promise<StaffUser> {
    return fetchWithAuth<StaffUser>(`/api/v1/admin/users/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify(data),
    });
  },

  // Branch Management
  async getBranches(): Promise<Branch[]> {
    return fetchWithAuth<Branch[]>("/api/v1/branches");
  },

  async getAdminBranches(params?: { search?: string; isActive?: boolean }): Promise<import("../types/branch").Branch[]> {
    const query = new URLSearchParams();
    if (params?.search) query.set("search", params.search);
    if (params?.isActive !== undefined) query.set("isActive", params.isActive.toString());

    const queryString = query.toString();
    const endpoint = queryString ? `/api/v1/admin/branches?${queryString}` : "/api/v1/admin/branches";
    return fetchWithAuth<import("../types/branch").Branch[]>(endpoint);
  },

  async getBranchById(id: string): Promise<import("../types/branch").Branch> {
    return fetchWithAuth<import("../types/branch").Branch>(`/api/v1/admin/branches/${id}`);
  },

  async createBranch(data: import("../types/branch").CreateBranchRequest): Promise<import("../types/branch").Branch> {
    return fetchWithAuth<import("../types/branch").Branch>("/api/v1/admin/branches", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },

  async updateBranch(id: string, data: import("../types/branch").UpdateBranchRequest): Promise<import("../types/branch").Branch> {
    return fetchWithAuth<import("../types/branch").Branch>(`/api/v1/admin/branches/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  },

  async updateBranchStatus(id: string, isActive: boolean): Promise<import("../types/branch").Branch> {
    return fetchWithAuth<import("../types/branch").Branch>(`/api/v1/admin/branches/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive }),
    });
  },

  async deleteBranch(id: string): Promise<void> {
    return fetchWithAuth<void>(`/api/v1/admin/branches/${id}`, {
      method: "DELETE",
    });
  },

  // Customer Management
  async getCustomers(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    branchId?: string;
    status?: string;
  }): Promise<import("../types/customer").CustomerPagedResponse> {
    const query = new URLSearchParams();
    if (params?.page) query.set("page", params.page.toString());
    if (params?.pageSize) query.set("pageSize", params.pageSize.toString());
    if (params?.search) query.set("search", params.search);
    if (params?.branchId) query.set("branchId", params.branchId);
    if (params?.status) query.set("status", params.status);

    const queryString = query.toString();
    const endpoint = queryString ? `/api/v1/admin/customers?${queryString}` : "/api/v1/admin/customers";
    return fetchWithAuth<import("../types/customer").CustomerPagedResponse>(endpoint);
  },

  async getCustomerStatistics(params?: { search?: string; branchId?: string }): Promise<import("../types/customer").CustomerStatistics> {
    const query = new URLSearchParams();
    if (params?.search) query.set("search", params.search);
    if (params?.branchId) query.set("branchId", params.branchId);

    const queryString = query.toString();
    const endpoint = queryString ? `/api/v1/admin/customers/statistics?${queryString}` : "/api/v1/admin/customers/statistics";
    return fetchWithAuth<import("../types/customer").CustomerStatistics>(endpoint);
  },

  async createCustomer(data: import("../types/customer").CreateCustomerRequest): Promise<import("../types/customer").CustomerDetail> {
    return fetchWithAuth<import("../types/customer").CustomerDetail>("/api/v1/admin/customers", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },

  async updateCustomerStatus(id: string, data: import("../types/customer").UpdateCustomerStatusRequest): Promise<import("../types/customer").CustomerDetail> {
    return fetchWithAuth<import("../types/customer").CustomerDetail>(`/api/v1/admin/customers/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify(data),
    });
  },

  async getCustomerMetrics(branchId?: string): Promise<import("../types/customer").CustomerMetrics> {
    const endpoint = branchId ? `/api/v1/admin/customers/metrics?branchId=${encodeURIComponent(branchId)}` : "/api/v1/admin/customers/metrics";
    return fetchWithAuth<import("../types/customer").CustomerMetrics>(endpoint);
  },

  async getCustomerDetail(id: string): Promise<import("../types/customer").CustomerDetail> {
    return fetchWithAuth<import("../types/customer").CustomerDetail>(`/api/v1/admin/customers/${id}`);
  },

  // Jewellery Plan Categories
  async getJewelleryCategories(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    branchId?: string;
    isActive?: boolean;
  }): Promise<import("../types/jewelleryCategory").JewelleryCategoryPagedResponse> {
    const query = new URLSearchParams();
    if (params?.page) query.set("page", params.page.toString());
    if (params?.pageSize) query.set("pageSize", params.pageSize.toString());
    if (params?.search) query.set("search", params.search);
    if (params?.branchId) query.set("branchId", params.branchId);
    if (params?.isActive !== undefined) query.set("isActive", params.isActive.toString());

    const queryString = query.toString();
    const endpoint = queryString ? `/api/v1/admin/jewellery-categories?${queryString}` : "/api/v1/admin/jewellery-categories";
    return fetchWithAuth<import("../types/jewelleryCategory").JewelleryCategoryPagedResponse>(endpoint);
  },

  async getJewelleryCategoryById(id: string): Promise<import("../types/jewelleryCategory").JewelleryCategory> {
    return fetchWithAuth<import("../types/jewelleryCategory").JewelleryCategory>(`/api/v1/admin/jewellery-categories/${id}`);
  },

  async createJewelleryCategory(data: import("../types/jewelleryCategory").CreateJewelleryCategoryRequest): Promise<import("../types/jewelleryCategory").JewelleryCategory> {
    return fetchWithAuth<import("../types/jewelleryCategory").JewelleryCategory>("/api/v1/admin/jewellery-categories", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },

  async updateJewelleryCategory(id: string, data: import("../types/jewelleryCategory").UpdateJewelleryCategoryRequest): Promise<import("../types/jewelleryCategory").JewelleryCategory> {
    return fetchWithAuth<import("../types/jewelleryCategory").JewelleryCategory>(`/api/v1/admin/jewellery-categories/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  },

  async updateJewelleryCategoryStatus(id: string, isActive: boolean): Promise<import("../types/jewelleryCategory").JewelleryCategory> {
    return fetchWithAuth<import("../types/jewelleryCategory").JewelleryCategory>(`/api/v1/admin/jewellery-categories/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify({ isActive }),
    });
  },

  async deleteJewelleryCategory(id: string): Promise<void> {
    return fetchWithAuth<void>(`/api/v1/admin/jewellery-categories/${id}`, {
      method: "DELETE",
    });
  },

  async uploadJewelleryCategoryImage(file: File): Promise<import("../types/jewelleryCategory").ImageUploadResponse> {
    const formData = new FormData();
    formData.append("file", file);
    return fetchWithAuth<import("../types/jewelleryCategory").ImageUploadResponse>("/api/v1/admin/jewellery-categories/upload-image", {
      method: "POST",
      body: formData,
    });
  },

  // Jewellery Plans
  async getJewelleryPlans(params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    categoryId?: string;
    status?: string;
    branchId?: string;
  }): Promise<import("../types/jewelleryPlan").JewelleryPlanPagedResponse> {
    const query = new URLSearchParams();
    if (params?.page) query.set("page", params.page.toString());
    if (params?.pageSize) query.set("pageSize", params.pageSize.toString());
    if (params?.search) query.set("search", params.search);
    if (params?.categoryId) query.set("categoryId", params.categoryId);
    if (params?.status) query.set("status", params.status);
    if (params?.branchId) query.set("branchId", params.branchId);

    const queryString = query.toString();
    const endpoint = queryString ? `/api/v1/admin/jewellery-plans?${queryString}` : "/api/v1/admin/jewellery-plans";
    return fetchWithAuth<import("../types/jewelleryPlan").JewelleryPlanPagedResponse>(endpoint);
  },

  async getJewelleryPlanById(id: string): Promise<import("../types/jewelleryPlan").JewelleryPlan> {
    return fetchWithAuth<import("../types/jewelleryPlan").JewelleryPlan>(`/api/v1/admin/jewellery-plans/${id}`);
  },

  async createJewelleryPlan(data: import("../types/jewelleryPlan").CreateJewelleryPlanRequest): Promise<import("../types/jewelleryPlan").JewelleryPlan> {
    return fetchWithAuth<import("../types/jewelleryPlan").JewelleryPlan>("/api/v1/admin/jewellery-plans", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },

  async updateJewelleryPlan(id: string, data: import("../types/jewelleryPlan").UpdateJewelleryPlanRequest): Promise<import("../types/jewelleryPlan").JewelleryPlan> {
    return fetchWithAuth<import("../types/jewelleryPlan").JewelleryPlan>(`/api/v1/admin/jewellery-plans/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  },

  async updateJewelleryPlanStatus(id: string, data: import("../types/jewelleryPlan").UpdateJewelleryPlanStatusRequest): Promise<import("../types/jewelleryPlan").JewelleryPlan> {
    return fetchWithAuth<import("../types/jewelleryPlan").JewelleryPlan>(`/api/v1/admin/jewellery-plans/${id}/status`, {
      method: "PATCH",
      body: JSON.stringify(data),
    });
  },

  async getJewelleryPlanCustomers(id: string, params?: {
    page?: number;
    pageSize?: number;
    search?: string;
    status?: string;
  }): Promise<import("../types/jewelleryPlan").PlanCustomersPagedResponse> {
    const query = new URLSearchParams();
    if (params?.page) query.set("page", params.page.toString());
    if (params?.pageSize) query.set("pageSize", params.pageSize.toString());
    if (params?.search) query.set("search", params.search);
    if (params?.status) query.set("status", params.status);

    const queryString = query.toString();
    const endpoint = queryString ? `/api/v1/admin/jewellery-plans/${id}/customers?${queryString}` : `/api/v1/admin/jewellery-plans/${id}/customers`;
    return fetchWithAuth<import("../types/jewelleryPlan").PlanCustomersPagedResponse>(endpoint);
  },

  async uploadJewelleryPlanImage(file: File): Promise<import("../types/jewelleryCategory").ImageUploadResponse> {
    const formData = new FormData();
    formData.append("file", file);
    return fetchWithAuth<import("../types/jewelleryCategory").ImageUploadResponse>("/api/v1/admin/jewellery-plans/upload-image", {
      method: "POST",
      body: formData,
    });
  },

  async calculateFinancialQuote(data: import("../types/jewelleryPlan").FinancialQuoteRequest): Promise<import("../types/jewelleryPlan").FinancialQuoteResponse> {
    return fetchWithAuth<import("../types/jewelleryPlan").FinancialQuoteResponse>("/api/v1/admin/jewellery-plans/financial-quote", {
      method: "POST",
      body: JSON.stringify(data),
    });
  },
};

