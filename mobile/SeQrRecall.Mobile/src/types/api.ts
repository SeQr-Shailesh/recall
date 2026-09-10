export type ProcessingStatus =
  | 'Draft'
  | 'Uploading'
  | 'Uploaded'
  | 'Processing'
  | 'Completed'
  | 'Failed';

export type ActionItemKind = 'Action' | 'FollowUp';

export interface ApiResponse<T> {
  success: boolean;
  data: T | null;
  message: string | null;
  errors?: string[];
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedRequest {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
}
