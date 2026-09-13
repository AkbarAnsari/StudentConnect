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
}

export interface SectionClassReport {
  sectionId: number;
  gradeName: string;
  sectionName: string;
  studentCount: number;
  averageAttendancePercentage: number;
  averageMarksPercentage: number;
  atRiskCount: number;
}
