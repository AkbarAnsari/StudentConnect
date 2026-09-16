export type StudentLifecycleStatus = 'Active' | 'Withdrawn' | 'Graduated' | 'Transferred';

export interface Student {
  id: number;
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
  gradeId: number;
  gradeName: string;
  sectionId: number | null;
  sectionName: string;
  status: StudentLifecycleStatus;
  statusChangedAtUtc: string | null;
  statusReason: string | null;
  dateOfBirth: string | null;
  phone: string | null;
  address: string | null;
}

export interface Grade {
  id: number;
  name: string;
}

export interface StudentSubject {
  id: number;
  name: string;
  code: string;
}

export interface Section {
  id: number;
  gradeId: number;
  gradeName: string;
  name: string;
  homeroomTeacherId: number | null;
  homeroomTeacherName: string | null;
  isArchived: boolean;
}

export interface StudentInput {
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
  gradeId: number;
  sectionId: number;
  dateOfBirth?: string | null;
  phone?: string | null;
  address?: string | null;
  subjectIds?: number[];
  createLogin?: boolean;
  username?: string | null;
  guardianParentUserId?: number | null;
  guardianRelationshipType?: string;
  guardianIsPrimaryContact?: boolean;
}

export interface StudentStatusUpdate {
  status: StudentLifecycleStatus;
  reason?: string;
}

export interface StudentBulkStatusResult {
  requested: number;
  updated: number;
}

export interface StudentHistoryEntry {
  id: number;
  timestampUtc: string;
  username: string;
  role: string;
  action: string;
  entityType: string;
  entityId: string;
  details: string | null;
}

export interface StudentImportRowResult {
  row: number;
  success: boolean;
  message: string | null;
  studentId: string | null;
}

export interface StudentImportResult {
  totalRows: number;
  successCount: number;
  errors: StudentImportRowResult[];
}
