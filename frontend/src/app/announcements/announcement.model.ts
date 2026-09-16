export type AnnouncementScopeType = 'School' | 'Grade' | 'Section';
export type AnnouncementStatus = 'Draft' | 'Scheduled' | 'Sent' | 'Expired' | 'Withdrawn';
export type AnnouncementPriority = 'Normal' | 'Important' | 'Urgent';

export interface Announcement {
  id: number;
  title: string;
  body: string;
  scopeType: AnnouncementScopeType;
  scopeName: string | null;
  authorName: string;
  createdAtUtc: string;
  status: AnnouncementStatus;
  priority: AnnouncementPriority;
  scheduledForUtc: string | null;
  expiresAtUtc: string | null;
  sentAtUtc: string | null;
  withdrawnAtUtc: string | null;
  recipientCount: number;
  readCount: number;
  attachmentFileName: string | null;
  attachmentFileSize: number | null;
  dispatchAttempts: number;
  lastDispatchError: string | null;
  updatedAtUtc: string | null;
}

export interface AnnouncementCreateInput {
  title: string;
  body: string;
  scopeType: AnnouncementScopeType;
  scopeGradeId?: number;
  scopeSectionId?: number;
  status: 'Draft' | 'Scheduled' | 'Sent';
  priority: AnnouncementPriority;
  scheduledForUtc?: string | null;
  expiresAtUtc?: string | null;
}

export interface AnnouncementAudiencePreview {
  studentCount: number;
  recipientCount: number;
}
