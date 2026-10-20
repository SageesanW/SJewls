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

export interface AdminJewelleryPaymentsQuery {
  branchId?: string;
  planId?: string;
  status?: string; // "All" | "Successful" | "Pending"
  search?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminJewelleryPaymentItem {
  paymentId: string;
  contributionId?: string | null;
  instalmentNumber?: number | null;
  instalmentLabel: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  customerEmail?: string | null;
  enrolmentId: string;
  enrolmentReference: string;
  planId: string;
  planName: string;
  branchId: string;
  branchName: string;
  branchTimezone: string;
  amount: number;
  currency: string;
  goldRate: number;
  goldCredit: number;
  estimatedPendingGoldCredit?: number | null;
  status: "Successful" | "Pending" | string;
  paymentMethod: string;
  paymentReference: string;
  dueDate: string;
  paidAtUtc?: string | null;
  createdAtUtc: string;
  canSendReminder: boolean;
  reminderReason?: string | null;
  lastSuccessfulPaymentAt?: string | null;
  reminderEligibleAt?: string | null;
}

export interface AdminJewelleryPaymentsPagedResponse {
  items: AdminJewelleryPaymentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  totalConfirmedAmount: number;
  totalConfirmedGrams: number;
  totalPendingAmount: number;
  totalPendingCount: number;
  totalSuccessfulCount: number;
  currency: string;
}

export interface AdminIncompleteEnrolmentsQuery {
  branchId?: string;
  planId?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface AdminIncompleteEnrolmentItem {
  enrolmentId: string;
  enrolmentReference: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  customerEmail?: string | null;
  planId: string;
  planName: string;
  branchId: string;
  branchName: string;
  joiningDate: string;
  selectedDurationMonths: number;
  deadline: string;
  targetGrams: number;
  netSavedGrams: number;
  remainingGrams: number;
  totalConfirmedMoneyPaid: number;
  currency: string;
  progressPercentage: number;
  status: string;
  canSendReminder: boolean;
  reminderReason?: string | null;
  lastSuccessfulPaymentAt?: string | null;
}

export interface AdminIncompleteEnrolmentsPagedResponse {
  items: AdminIncompleteEnrolmentItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  totalIncompleteEnrolments: number;
  totalRemainingGrams: number;
  totalConfirmedMoneyPaid: number;
  currency: string;
}

export interface ChannelStatus {
  available: boolean;
  description: string;
}

export interface AdminReminderContext {
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  customerEmail?: string | null;
  isPhoneVerified: boolean;
  isEmailVerified: boolean;
  enrolmentId: string;
  enrolmentReference: string;
  planId: string;
  planName: string;
  targetGrams: number;
  savedGrams: number;
  remainingGrams: number;
  startDate: string;
  deadline: string;
  lastSuccessfulPaymentAt?: string | null;
  totalContributedCurrency: number;
  currency: string;
  reminderReason: string;
  canSendReminder: boolean;
  reminderEligibleAt?: string | null;
  suggestedTitle: string;
  suggestedMessage: string;
  inApp: ChannelStatus;
  email: ChannelStatus;
  sms: ChannelStatus;
  push: ChannelStatus;
}

export interface AdminSendReminderRequest {
  channels: string[];
  title: string;
  message: string;
}

export interface AdminSendReminderResult {
  success: boolean;
  message: string;
  deliveredChannels: string[];
  failedChannels: string[];
  sentAtUtc: string;
}
