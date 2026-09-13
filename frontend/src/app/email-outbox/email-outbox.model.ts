export interface EmailOutboxEntry {
  id: number;
  toEmail: string;
  subject: string;
  createdAtUtc: string;
  sentAtUtc: string | null;
  attempts: number;
  lastError: string | null;
}
