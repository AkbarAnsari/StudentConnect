export type AttendanceStatus = 'Present' | 'Absent';

export interface AttendanceRecord {
  id: number;
  studentId: number;
  studentName: string;
  subjectId: number;
  subjectName: string;
  date: string;
  status: AttendanceStatus;
  correctionReason: string | null;
}

export interface AttendanceSummary {
  subjectId: number;
  subjectName: string;
  totalClasses: number;
  present: number;
  absent: number;
  percentage: number;
}

export interface Subject {
  id: number;
  name: string;
  code: string;
}

export interface AttendanceCreateInput {
  studentId: number;
  subjectId: number;
  date: string;
  status: AttendanceStatus;
}

export interface BulkAttendanceItem {
  studentId: number;
  status: AttendanceStatus;
  reason?: string | null;
}

export interface BulkAttendanceInput {
  subjectId: number;
  date: string;
  records: BulkAttendanceItem[];
}
