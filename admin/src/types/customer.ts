export interface CustomerListItem {
  id: string;
  fullName: string;
  phoneNumber: string | null;
  email: string | null;
  nic: string | null;
  isPhoneVerified: boolean;
  isEmailVerified: boolean;
  primaryBranchId: string;
  primaryBranchName: string;
  primaryBranchCode: string;
  isActive: boolean;
  isProfileComplete: boolean;
  activePlansCount: number;
  totalSlotsCount: number;
  createdAtUtc: string;
  deactivatedAtUtc?: string | null;
  deactivationReason?: string | null;
  closedAtUtc?: string | null;
  closureReason?: string | null;
}

export interface CustomerSlot {
  id: string;
  slotNumber: number;
  planName: string;
  planCode: string;
  monthlyInstalmentAmount: number;
  status: string;
  activatedAtUtc: string | null;
}

export interface CustomerEnrolment {
  id: string;
  planName: string;
  planCode: string;
  targetWeightGrams: number;
  totalSavedGrams: number;
  totalContributedCurrency: number;
  status: string;
  startDate: string;
  targetEndDate: string;
}

export interface CustomerDetail {
  id: string;
  fullName: string;
  dateOfBirth: string | null;
  nic: string | null;
  phoneNumber: string | null;
  email: string | null;
  isPhoneVerified: boolean;
  isEmailVerified: boolean;
  phoneVerifiedAtUtc: string | null;
  emailVerifiedAtUtc: string | null;
  primaryBranchId: string;
  primaryBranchName: string;
  primaryBranchCode: string;
  isActive: boolean;
  isProfileComplete: boolean;
  createdAtUtc: string;
  deactivatedAtUtc?: string | null;
  deactivationReason?: string | null;
  closedAtUtc?: string | null;
  closureReason?: string | null;
  chituSlots: CustomerSlot[];
  jewelleryEnrolments: CustomerEnrolment[];
}

export interface CustomerPagedResponse {
  items: CustomerListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CustomerMetrics {
  totalSlots: number;
  totalCustomers: number;
  activeInvestment: number;
  closedAccounts: number;
  inactiveCustomers: number;
  pendingAccounts: number;
}

export interface CustomerStatistics {
  totalCustomers: number;
  activeCustomers: number;
  inactiveCustomers: number;
}

export interface CreateCustomerRequest {
  fullName: string;
  phoneNumber: string;
  email: string;
  nic: string;
  branchId?: string;
}

export interface UpdateCustomerStatusRequest {
  isActive: boolean;
  reason?: string;
}
