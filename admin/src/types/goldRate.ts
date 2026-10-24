export interface EffectiveGoldRateDetail {
  rate24k: number;
  rate22k: number;
  currency: string;
  activeSource: string; // "ManualOverride" | "LiveApi" | "BranchManual"
  isOverridden: boolean;
  effectiveFromUtc: string;
  branchId: string | null;
  branchName: string | null;
}

export interface LiveApiGoldRateDetail {
  rate24k: number | null;
  rate22k: number | null;
  currency: string;
  provider: string;
  isConfigured: boolean;
  isAvailable: boolean;
  timestampUtc: string | null;
  statusMessage: string | null;
}

export interface ManualOverrideDetail {
  id: string;
  branchId: string | null;
  branchName: string | null;
  rate24k: number;
  rate22k: number;
  isEnabled: boolean;
  reason: string | null;
  updatedByStaffId: string | null;
  updatedByStaffName: string | null;
  updatedAtUtc: string;
}

export interface BranchGoldRateSummary {
  id: string;
  name: string;
  code: string;
  city: string;
  rate24k: number;
  rate22k: number;
  hasCustomOverride: boolean;
  isOverrideEnabled: boolean;
}

export interface GoldRateOverview {
  effectiveRate: EffectiveGoldRateDetail;
  liveApiRate: LiveApiGoldRateDetail;
  manualOverride: ManualOverrideDetail | null;
  branches: BranchGoldRateSummary[];
}

export interface SetGoldRateOverrideRequest {
  branchId: string | null;
  rate24k: number;
  rate22k: number;
  isEnabled: boolean;
  reason?: string;
}

export interface ToggleGoldRateOverrideRequest {
  branchId: string | null;
  isEnabled: boolean;
}

export interface GoldRateHistoryItem {
  id: string;
  branchId: string | null;
  branchName: string | null;
  karat: number;
  ratePerGram: number;
  source: string;
  effectiveFromUtc: string;
  recordedByStaffId: string | null;
  recordedByStaffName: string | null;
  remarks: string | null;
}
