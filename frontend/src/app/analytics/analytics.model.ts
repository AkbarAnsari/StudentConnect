export interface AnalyticsOverview {
  totalStudents: number;
  totalTeachers: number;
  totalSubjects: number;
  overallAttendancePercentage: number;
  averageMarksPercentage: number;
}

export interface GradeAttendance {
  gradeId: number;
  gradeName: string;
  totalRecords: number;
  attendancePercentage: number;
}

export interface GradeMarks {
  gradeId: number;
  gradeName: string;
  averageMarksPercentage: number;
  resultCategory: string;
}

export interface AtRiskStudent {
  id: number;
  studentId: string;
  fullName: string;
  gradeName: string;
  attendancePercentage: number | null;
  averageMarksPercentage: number | null;
}
