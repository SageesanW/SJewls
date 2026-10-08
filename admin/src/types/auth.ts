export interface StaffBranch {
  branchId: string;
  code: string;
  name: string;
  city: string;
}

export interface StaffUser {
  id: string;
  fullName: string;
  email: string;
  username: string | null;
  phoneNumber: string;
  isActive: boolean;
  roles: string[];
  assignedBranches: StaffBranch[];
  createdAtUtc: string;
  updatedAtUtc: string | null;
  deactivatedAtUtc?: string | null;
  deactivationReason?: string | null;
}

export interface UpdateStaffStatusRequest {
  isActive: boolean;
  reason?: string;
}

export interface LoginResponse {
  accessToken: string;
  tokenType: string;
  expiresAtUtc: string;
  user: StaffUser;
}

export interface ForgotPasswordResponse {
  message: string;
}

export interface ResetPasswordResponse {
  message: string;
}

export interface Branch {
  id: string;
  code: string;
  name: string;
  city: string;
  isActive: boolean;
}
