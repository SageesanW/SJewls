export interface JewelleryCategory {
  id: string;
  branchId: string;
  branchName: string;
  branchCode: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  isActive: boolean;
  planCount: number;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface JewelleryCategoryPagedResponse {
  items: JewelleryCategory[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface CreateJewelleryCategoryRequest {
  name: string;
  description?: string;
  imageUrl?: string;
  isActive: boolean;
  branchId?: string;
}

export interface UpdateJewelleryCategoryRequest {
  name: string;
  description?: string;
  imageUrl?: string;
  isActive: boolean;
}

export interface UpdateJewelleryCategoryStatusRequest {
  isActive: boolean;
}

export interface ImageUploadResponse {
  url: string;
  fileName: string;
  size: number;
}
