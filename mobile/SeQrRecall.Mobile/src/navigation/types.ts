import type { AuthIdentifier } from '@utils/identifier';

export type AuthStackParamList = {
  Identifier: undefined;
  Otp: { identifier: AuthIdentifier };
  ProfileSetup: undefined;
};

export type RootTabParamList = {
  NotesTab: undefined;
  LeadsTab: undefined;
  CustomersTab: undefined;
};

export type NotesStackParamList = {
  NotesList: undefined;
  Record: undefined;
  NoteDetails: { noteId: string; justFinished?: boolean };
};

export type LeadsStackParamList = {
  LeadsList: undefined;
  RecordLead: undefined;
  LeadDetails: { leadId: string; justFinished?: boolean };
};

export type CustomersStackParamList = {
  CustomersList: undefined;
  CustomerForm: { customerId?: string };
  CustomerDetails: { customerId: string };
  RecordInteraction: { customerId: string };
  InteractionDetails: { interactionId: string; justFinished?: boolean };
};

export type AppStackParamList = NotesStackParamList;
