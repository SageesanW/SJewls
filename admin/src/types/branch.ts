export interface Branch {
  id: string;
  code: string;
  name: string;
  address: string;
  city: string;
  country: string;
  currency: string;
  timezone: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
  assignedStaffCount?: number;
  customerCount?: number;
}

export interface CreateBranchRequest {
  code: string;
  name: string;
  address: string;
  city?: string;
  country?: string;
  currency?: string;
  timezone?: string;
  isActive?: boolean;
}

export interface UpdateBranchRequest {
  name: string;
  address: string;
  city?: string;
  country?: string;
  currency?: string;
  timezone?: string;
  isActive?: boolean;
}

export interface UpdateBranchStatusRequest {
  isActive: boolean;
}
