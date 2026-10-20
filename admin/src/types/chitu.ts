export enum ChituRedemptionType {
  Cash = 1,
  Gold = 2,
}

export type ChituSlotStatus =
  | "PendingReservation"
  | "Active"
  | "Won"
  | "Closed"
  | "Defaulted";

export interface ChituPlanScheduleRoundDto {
  roundIndex: number;
  scheduledRaffleDate: string;
  isOverdue: boolean;
  isCompleted: boolean;
  recordedAtUtc?: string | null;
  winningSlotId?: string | null;
  winningSlotDisplayCode?: string | null;
  winnerCustomerName?: string | null;
}

export interface ChituPlanCardDto {
  id: string;
  branchId: string;
  branchName: string;
  code: string;
  name: string;
  description: string;
  redemptionType: ChituRedemptionType;
  redemptionTypeLabel: "Cash" | "Gold";
  durationMonths: number;
  monthlyInstalmentAmount: number;
  chituBenefitValue: number;
  totalSlotCapacity: number;
  assignedSlotsCount: number;
  availableSlotsCount: number;
  startDate: string;
  endDate: string;
  status: string;
  statusLabel: string;
  isUpcoming: boolean;
  currentRoundIndex: number;
  currentRoundDueDate: string;
  currentRoundPaidSlotsCount: number;
  currentRoundUnpaidSlotsCount: number;
  winningSlotsCount: number;
  closedSlotsCount: number;
  hasOverdueRaffle: boolean;
  overdueRaffleRoundIndex?: number | null;
  createdAtUtc: string;
}

export interface ChituPlanDetailDto extends ChituPlanCardDto {
  schedule: ChituPlanScheduleRoundDto[];
}

export interface ChituPlanListResponse {
  items: ChituPlanCardDto[];
  totalCount: number;
  activeCount: number;
  inactiveCount: number;
}

export interface CreateChituPlanRequest {
  name: string;
  code: string;
  redemptionType: ChituRedemptionType;
  durationMonths: number;
  startDate: string;
  monthlyInstalmentAmount: number;
  totalSlotCapacity: number;
  branchId: string;
  description?: string;
}

export interface UpdateChituPlanRequest {
  name?: string;
  code?: string;
  redemptionType?: ChituRedemptionType;
  durationMonths?: number;
  monthlyInstalmentAmount?: number;
  totalSlotCapacity?: number;
  startDate?: string;
  branchId?: string;
  description?: string;
  status?: string;
}

// Slot Assignment
export interface ChituSlotAssignmentPreviewRequest {
  planId: string;
  customerId: string;
  slotsCount: number;
  additionalPrepaidMonths?: number;
}

export interface ChituInstalmentAllocationItemDto {
  monthIndex: number;
  dueDate: string;
  amount: number;
}

export interface ChituSlotAllocationBreakdownDto {
  slotNumber: number;
  slotDisplayCode: string;
  instalmentsToPay: ChituInstalmentAllocationItemDto[];
  totalForSlot: number;
}

export interface ChituSlotAssignmentPreviewResponse {
  planId: string;
  planName: string;
  planCode: string;
  customerId: string;
  customerName: string;
  slotsCount: number;
  assignedSlotNumbers: number[];
  assignedSlotDisplayCodes: string[];
  currentRoundIndex: number;
  requiredMonthsPerSlot: number;
  additionalPrepaidMonths: number;
  totalMonthsPaidPerSlot: number;
  requiredAmountPerSlot: number;
  totalPayableAmount: number;
  allocationBreakdown: ChituSlotAllocationBreakdownDto[];
}

export interface AssignChituSlotsRequest {
  planId: string;
  customerId: string;
  slotsCount: number;
  additionalPrepaidMonths?: number;
  paymentMethod: "Cash" | "Card" | "BankTransfer" | 1 | 2 | 3 | 4;
  referenceNumber?: string;
  notes?: string;
  idempotencyKey?: string;
}

export interface ChituSlotDto {
  id: string;
  planId: string;
  planCode: string;
  planName: string;
  customerId: string;
  customerName: string;
  slotNumber: number;
  slotDisplayCode: string;
  status: ChituSlotStatus;
  statusLabel: string;
  monthsPaidCount: number;
  totalPaidAmount: number;
  activatedAtUtc?: string | null;
}

export interface AssignChituSlotsResponse {
  paymentId: string;
  totalAmountPaid: number;
  createdSlots: ChituSlotDto[];
  message: string;
}

// Payment
export interface ChituInstalmentDto {
  id: string;
  monthIndex: number;
  dueDate: string;
  amountDue: number;
  status: string;
  paidAtUtc?: string | null;
}

export interface ChituPayableSlotDto {
  slotId: string;
  slotNumber: number;
  slotDisplayCode: string;
  status: ChituSlotStatus;
  nextUnpaidMonthIndex: number;
  totalUnpaidMonthsRemaining: number;
  instalmentsPaidCount: number;
  totalConfirmedPaid: number;
  isPaidForCurrentRound: boolean;
  unpaidInstalments: ChituInstalmentDto[];
}

export interface CustomerPayableSlotsResponse {
  customerId: string;
  customerName: string;
  planId: string;
  planName: string;
  planCode: string;
  monthlyInstalmentAmount: number;
  currentRoundIndex: number;
  slots: ChituPayableSlotDto[];
}

export interface ChituSlotAllocationInputDto {
  slotId: string;
  amount: number;
}

export interface ChituPaymentPreviewRequest {
  planId: string;
  customerId: string;
  slotAllocations: ChituSlotAllocationInputDto[];
}

export interface ChituSlotPaymentPreviewBreakdownDto {
  slotId: string;
  slotDisplayCode: string;
  amount: number;
  monthsCovered: number;
  clearedInstalments: ChituInstalmentDto[];
}

export interface ChituPaymentPreviewResponse {
  totalAmount: number;
  slots: ChituSlotPaymentPreviewBreakdownDto[];
}

export interface RecordChituPaymentRequest {
  planId: string;
  customerId: string;
  slotAllocations: ChituSlotAllocationInputDto[];
  paymentMethod: "Cash" | "Card" | "BankTransfer" | 1 | 2 | 3 | 4;
  referenceNumber?: string;
  notes?: string;
  idempotencyKey?: string;
}

export interface ChituSlotPaymentReceiptItemDto {
  slotId: string;
  slotDisplayCode: string;
  amountAllocated: number;
  monthsCovered: number;
  clearedMonthIndices: number[];
}

export interface RecordChituPaymentResponse {
  paymentId: string;
  totalAmount: number;
  paidAtUtc: string;
  paymentMethod: string;
  slotReceipts: ChituSlotPaymentReceiptItemDto[];
  message: string;
}

// Raffle
export interface ChituEligibleSlotDto {
  slotId: string;
  slotNumber: number;
  slotDisplayCode: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  customerNic?: string | null;
  totalConfirmedPaid: number;
  monthsPaidCount: number;
}

export interface ChituEligibleSlotsResponse {
  planId: string;
  planName: string;
  roundIndex: number;
  scheduledRaffleDate: string;
  eligibleCount: number;
  eligibleSlots: ChituEligibleSlotDto[];
}

export interface RecordChituRaffleRequest {
  winningSlotId: string;
  referenceNumber?: string;
  remarks?: string;
}

export interface RecordChituRaffleResponse {
  drawId: string;
  planId: string;
  roundIndex: number;
  scheduledDate: string;
  recordedAtUtc: string;
  winningSlotId: string;
  winningSlotDisplayCode: string;
  customerId: string;
  customerName: string;
  winningEntitlementValue: number;
  advanceExcessPrepaidAmount: number;
  advanceExcessSettlementDecisionRequired: boolean;
  benefitStatus: string;
  message: string;
}

// Roster
export interface ChituRosterQueryParameters {
  roundIndex?: number;
  search?: string;
  statusFilter?: string;
  paymentFilter?: string;
  page?: number;
  pageSize?: number;
}

export interface ChituRosterRowDto {
  slotId: string;
  slotNumber: number;
  slotDisplayCode: string;
  customerId: string;
  customerName: string;
  customerPhone?: string | null;
  slotStatus: ChituSlotStatus;
  slotStatusLabel: string;
  isPaidForSelectedRound: boolean;
  isFullyPaidThroughSelectedRound: boolean;
  instalmentsPaidCount: number;
  paidThroughMonthIndex?: number | null;
  outstandingDueMonthsCount: number;
  totalConfirmedPaid: number;
  isWinner: boolean;
  winningRoundIndex?: number | null;
  entitlementAmount?: number | null;
  advanceExcessAmount?: number | null;
}

export interface ChituRosterResponse {
  planId: string;
  planName: string;
  planCode: string;
  durationMonths: number;
  selectedRoundIndex: number;
  maxSlots: number;
  assignedSlots: number;
  availableSlots: number;
  activeSlotsCount: number;
  winningSlotsCount: number;
  closedSlotsCount: number;
  roundPaidSlotsCount: number;
  roundUnpaidSlotsCount: number;
  rows: ChituRosterRowDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}
