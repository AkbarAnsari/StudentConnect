export interface SubjectReportRow {
  subjectId: number;
  subjectName: string;
  attendancePercentage: number;
  marksPercentage: number;
  resultCategory: string;
}

export interface StudentReportCard {
  studentId: number;
  studentCode: string;
  fullName: string;
  gradeName: string;
  sectionName: string;
  overallAttendancePercentage: number;
  overallMarksPercentage: number;
  overallResultCategory: string;
  rankInGrade: number;
  totalStudentsInGrade: number;
  homeworkCompletionRate: number;
  subjects: SubjectReportRow[];
  periodId: number | null;
  periodLabel: string;
  periodStartDate: string | null;
  periodEndDate: string | null;
  reportStatus: 'Draft' | 'Issued';
  issuanceId: number | null;
  issuedAtUtc: string | null;
  comments: string | null;
  templateName: string | null;
  headerText: string | null;
  footerText: string | null;
  signatureLabels: string[];
  generatedAtUtc: string;
}

export interface ReportPeriod {
  id: number;
  academicYearId: number;
  academicYearName: string;
  name: string;
  startDate: string;
  endDate: string;
  isLocked: boolean;
  isCurrent: boolean;
  label: string;
}

export interface ReportIssuanceSummary {
  id: number;
  studentId: number;
  termId: number;
  periodLabel: string;
  periodStartDate: string;
  periodEndDate: string;
  comments: string | null;
  issuedByUserId: number;
  issuedByName: string;
  issuedAtUtc: string;
  templateName: string | null;
  signatureCount: number;
  lastDownloadedAtUtc: string | null;
}

export interface ReportTemplate { id:number;name:string;headerText:string;footerText:string;primarySignatureLabel:string;secondarySignatureLabel:string|null;isDefault:boolean; }
export interface TranscriptIssuanceSummary { id:number;studentId:number;issuedByName:string;issuedAtUtc:string; }

export interface SectionClassReport {
  assignmentId: number;
  subjectId: number;
  subjectName: string;
  sectionId: number;
  gradeName: string;
  sectionName: string;
  studentCount: number;
  averageAttendancePercentage: number;
  averageMarksPercentage: number;
  atRiskCount: number;
}

export interface TranscriptSubjectRow {
  subjectId: number;
  subjectName: string;
  marksPercentage: number;
  resultCategory: string;
}

export interface TranscriptYearRow {
  academicYearId: number;
  academicYearName: string;
  overallMarksPercentage: number;
  overallResultCategory: string;
  subjects: TranscriptSubjectRow[];
}

export interface StudentTranscript {
  studentId: number;
  studentCode: string;
  fullName: string;
  years: TranscriptYearRow[];
}
