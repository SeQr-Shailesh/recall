import type { ProcessingStatus } from '@models/index';

export function noteStatusCopy(status: ProcessingStatus): string {
  switch (status) {
    case 'Draft':
      return 'Not uploaded yet';
    case 'Uploading':
    case 'Uploaded':
    case 'Processing':
      return 'Understanding your note...';
    case 'Completed':
      return 'Your note is ready';
    case 'Failed':
      return 'Couldn’t save this note';
  }
}

export function isNoteBusy(status: ProcessingStatus): boolean {
  return status === 'Uploading' || status === 'Uploaded' || status === 'Processing';
}

export function noteTitle(title: string | null | undefined): string {
  const value = title?.trim();
  return value ? value : 'Voice note';
}
