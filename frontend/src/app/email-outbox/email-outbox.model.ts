export interface EmailOutboxEntry {
  id: number;
  toEmail: string;
  subject: string;
  body: string;
  createdAtUtc: string;
  sentAtUtc: string | null;
  attempts: number;
  lastError: string | null;
  deadLetterOwnerUserId: number | null;
  deadLetterAssignedAtUtc: string | null;
  resolutionNotes: string | null;
}

export type EmailOutboxStatus = '' | 'sent' | 'failed' | 'pending';

export interface EmailOutboxFilters {
  page: number;
  pageSize: number;
  status: EmailOutboxStatus;
  query: string;
  fromUtc: string;
  toUtc: string;
}
