export type JewelleryPlanDisplayStatus = "Active" | "Scheduled" | "Deactivated";

export interface JewelleryPlan {
  id: string;
  branchId: string;
  branchName: string;
  branchCode: string;
  currency: string;
  code: string;
  name: string;
  description: string;
  imageUrl: string;
  categoryId: string | null;
  categoryName: string;
  targetProductGoldWeightGrams: number;
  karat: number;
  allowedDurationsMonths: number[];
  startDate: string;
  isActive: boolean;
  displayStatus: JewelleryPlanDisplayStatus;
  totalEnrolments: number;
  activeEnrolments: number;
  totalNetAccumulatedGrams: number;
  totalTargetGramsAcrossEnrolments: number;
  overallProgressPercentage: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  deactivatedAtUtc: string | null;
  reopenedAtUtc: string | null;
}

export interface JewelleryPlanPagedResponse {
  items: JewelleryPlan[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  activeCount: number;
  scheduledCount: number;
  deactivatedCount: number;
}

export interface CreateJewelleryPlanRequest {
  branchId?: string;
  categoryId: string;
  name: string;
  description?: string;
  imageUrl: string;
  allowedDurationsMonths: number[];
  startDate: string;
  targetProductGoldWeightGrams: number;
  karat?: number;
}

export interface UpdateJewelleryPlanRequest {
  categoryId: string;
  name: string;
  description?: string;
  imageUrl: string;
  allowedDurationsMonths: number[];
  startDate: string;
  targetProductGoldWeightGrams: number;
  karat?: number;
}

export interface UpdateJewelleryPlanStatusRequest {
  action: "Deactivate" | "Reopen";
  reason?: string;
}

export interface EnrolmentContribution {
  id: string;
  currencyAmount: number;
  currency: string;
  goldGrams: number;
  appliedRatePerGram: number;
  karat: number;
  confirmedAtUtc: string;
  status: string;
  paymentMethod: string;
}

export interface PlanCustomerEnrolment {
  enrolmentId: string;
  customerId: string;
  customerName: string;
  phoneNumber: string;
  email: string;
  joiningDate: string;
  selectedDurationMonths: number;
  deadline: string;
  enrolmentTargetGrams: number;
  totalConfirmedMoneyPaid: number;
  currency: string;
  netAccumulatedGrams: number;
  remainingGrams: number;
  individualProgressPercentage: number;
  enrolmentStatus: string;
  contributions: EnrolmentContribution[];
}

export interface PlanCustomersPagedResponse {
  planId: string;
  planName: string;
  categoryName: string;
  planTargetGrams: number;
  currency: string;
  overallProgressPercentage: number;
  items: PlanCustomerEnrolment[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FinancialQuoteRequest {
  branchId: string;
  enrolmentId?: string;
  mode: "ByGrams" | "ByMoney";
  inputGrams?: number;
  inputMoney?: number;
}

export interface FinancialQuoteResponse {
  branchId: string;
  ratePerGram: number;
  effectiveFromUtc: string;
  currency: string;
  payableMoney: number;
  creditedGrams: number;
  remainingTargetGrams: number | null;
  willExceedTarget: boolean;
  quoteExpiresAtUtc: string;
}
