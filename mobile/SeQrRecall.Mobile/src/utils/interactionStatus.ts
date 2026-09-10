import type { ProcessingStatus } from '@models/index';

export function interactionStatusCopy(status: ProcessingStatus): string {
  switch (status) {
    case 'Draft':
      return 'Not uploaded yet';
    case 'Uploading':
    case 'Uploaded':
    case 'Processing':
      return 'Understanding this conversation...';
    case 'Completed':
      return 'This conversation is ready';
    case 'Failed':
      return 'Couldn’t save this conversation';
  }
}

export function isInteractionBusy(status: ProcessingStatus): boolean {
  return status === 'Uploading' || status === 'Uploaded' || status === 'Processing';
}

export function interactionTitle(shortSummary: string | null | undefined): string {
  const value = shortSummary?.trim();
  return value ? value : 'Conversation';
}
