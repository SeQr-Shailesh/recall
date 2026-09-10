import type { ActionItemKind, ProcessingStatus } from './api';

export interface User {
  id: string;
  fullName: string;
  mobile: string | null;
  email: string | null;
  profilePhotoUrl: string | null;
  isProfileComplete: boolean;
  createdOn: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresOn: string;
}

export interface VerifyOtpResult {
  tokens: AuthTokens;
  user: User;
  isNewUser: boolean;
}

export interface NoteListItem {
  id: string;
  title: string | null;
  shortSummary: string | null;
  processingStatus: ProcessingStatus;
  createdOn: string;
  durationSeconds: number | null;
}

export interface NoteDetails {
  id: string;
  title: string | null;
  shortSummary: string | null;
  fullSummary: string | null;
  transcript: string | null;
  processingStatus: ProcessingStatus;
  processingError: string | null;
  createdOn: string;
  completedOn: string | null;
  durationSeconds: number | null;
  detectedLanguages: string[];
  actionItems: ActionItem[];
  hasAudio: boolean;
}

export interface CreateNoteResponse {
  id: string;
  processingStatus: ProcessingStatus;
}

export interface NoteStatus {
  id: string;
  processingStatus: ProcessingStatus;
  processingError: string | null;
  completedOn: string | null;
}

export interface LeadListItem {
  id: string;
  title: string | null;
  shortSummary: string | null;
  processingStatus: ProcessingStatus;
  createdOn: string;
  durationSeconds: number | null;
  hasPhoto: boolean;
}

export interface LeadDetails {
  id: string;
  title: string | null;
  shortSummary: string | null;
  fullSummary: string | null;
  transcript: string | null;
  processingStatus: ProcessingStatus;
  processingError: string | null;
  createdOn: string;
  completedOn: string | null;
  durationSeconds: number | null;
  detectedLanguages: string[];
  actionItems: ActionItem[];
  hasAudio: boolean;
  hasPhoto: boolean;
}

export interface CreateLeadResponse {
  id: string;
  processingStatus: ProcessingStatus;
}

export interface ActionItem {
  id: string;
  description: string;
  dueDate: string | null;
  isCompleted: boolean;
  kind: ActionItemKind;
}

export interface Customer {
  id: string;
  name: string;
  companyName: string | null;
  mobile: string | null;
  email: string | null;
  hasPhoto: boolean;
  createdOn: string;
  isActive: boolean;
}

export interface CustomerInteractionListItem {
  id: string;
  customerId: string;
  shortSummary: string | null;
  processingStatus: ProcessingStatus;
  interactionDate: string;
  hasPhoto: boolean;
}

export interface CustomerInteraction {
  id: string;
  customerId: string;
  customerName: string;
  companyName: string | null;
  shortSummary: string | null;
  fullSummary: string | null;
  transcript: string | null;
  processingStatus: ProcessingStatus;
  processingError: string | null;
  interactionDate: string;
  createdOn: string;
  completedOn: string | null;
  durationSeconds: number | null;
  detectedLanguages: string[];
  actionItems: ActionItem[];
  hasAudio: boolean;
  hasPhoto: boolean;
}

export interface CreateCustomerInteractionResponse {
  id: string;
  customerId: string;
  processingStatus: ProcessingStatus;
}
