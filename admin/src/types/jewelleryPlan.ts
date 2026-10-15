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
  premiumPercentage: number;
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
  premiumPercentage?: number;
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
  premiumPercentage?: number;
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
  paymentReference?: string;
}

export interface PlanCustomerEnrolment {
  enrolmentId: string;
  enrolmentReference?: string;
  customerId: string;
  customerName: string;
  phoneNumber: string;
  email: string;
  joiningDate: string;
  selectedDurationMonths: number;
  deadline: string;
  premiumPercentage: number;
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
  totalCollectedCurrency?: number;
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

export interface AdminAssignPlanRequest {
  customerId: string;
  planId: string;
  selectedDurationMonths: number;
  confirmRepeatEnrolment?: boolean;
  idempotencyKey?: string;
}

export interface AdminEnrolmentPreviewRequest {
  customerId: string;
  planId: string;
}

export interface AdminEnrolmentPreviewResponse {
  customerId: string;
  customerName: string;
  phoneNumber: string;
  email: string;
  customerBranchId: string;
  customerBranchName: string;
  planId: string;
  planName: string;
  categoryName: string;
  targetProductGoldWeightGrams: number;
  premiumPercentage: number;
  allowedDurationsMonths: number[];
  planStartDate: string;
  isPlanActive: boolean;
  totalExistingEnrolments: number;
  activeExistingEnrolments: number;
  requiresRepeatConfirmation: boolean;
  isEligible: boolean;
  ineligibilityReason?: string | null;
}

export interface AdminRecordPaymentPreviewRequest {
  mode: "BuyAmount" | "BuyGram";
  enteredAmount?: number;
  requestedGrams?: number;
}

export interface AdminRecordPaymentPreviewResponse {
  enrolmentId: string;
  enrolmentReference: string;
  customerId: string;
  customerName: string;
  planName: string;
  mode: "BuyAmount" | "BuyGram";
  ratePerGram: number;
  currency: string;
  rateAsOf: string;
  rateSource: string;
  premiumPercentage: number;
  baseGoldGrams: number;
  premiumGrams: number;
  chargeableGoldGrams?: number | null;
  netCreditedGrams: number;
  amountPayable: number;
  currentSavedGrams: number;
  targetGrams: number;
  remainingGramsBeforePayment: number;
  remainingGramsAfterPayment: number;
  willReachCompletion: boolean;
  willExceedTarget: boolean;
  paymentDate: string;
}

export interface AdminRecordPaymentRequest {
  mode: "BuyAmount" | "BuyGram";
  enteredAmount?: number;
  requestedGrams?: number;
  notes?: string;
  confirmedReceived: boolean;
  idempotencyKey?: string;
}

export interface AdminRecordPaymentResult {
  success: boolean;
  message: string;
  paymentId: string;
  paymentReference: string;
  amountPaid: number;
  currency: string;
  netCreditedGrams: number;
  appliedRatePerGram: number;
  appliedPremiumPercentage: number;
  newTotalSavedGrams: number;
  remainingGrams: number;
  progressPercentage: number;
  planCompleted: boolean;
  recordedAtUtc: string;
}
