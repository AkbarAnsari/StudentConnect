export type AnnouncementScopeType = 'School' | 'Grade' | 'Section';

export interface Announcement {
  id: number;
  title: string;
  body: string;
  scopeType: AnnouncementScopeType;
  scopeName: string | null;
  authorName: string;
  createdAtUtc: string;
}

export interface AnnouncementCreateInput {
  title: string;
  body: string;
  scopeType: AnnouncementScopeType;
  scopeGradeId?: number;
  scopeSectionId?: number;
}
