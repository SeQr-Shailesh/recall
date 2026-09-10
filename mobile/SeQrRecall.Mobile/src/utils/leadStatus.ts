import type { ProcessingStatus } from '@models/index';

export function leadStatusCopy(status: ProcessingStatus): string {
  switch (status) {
    case 'Draft':
      return 'Not uploaded yet';
    case 'Uploading':
    case 'Uploaded':
    case 'Processing':
      return 'Understanding this lead...';
    case 'Completed':
      return 'This lead is ready';
    case 'Failed':
      return 'Couldn’t save this lead';
  }
}

export function isLeadBusy(status: ProcessingStatus): boolean {
  return status === 'Uploading' || status === 'Uploaded' || status === 'Processing';
}

export function leadTitle(title: string | null | undefined): string {
  const value = title?.trim();
  return value ? value : 'New lead';
}
