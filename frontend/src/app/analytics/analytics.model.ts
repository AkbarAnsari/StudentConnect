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
  sectionName: string;
  attendancePercentage: number | null;
  averageMarksPercentage: number | null;
  riskLevel: 'High' | 'Watch';
  reasons: string[];
}

export interface MonthlyTrend {
  month: string;
  attendancePercentage: number;
  marksPercentage: number;
}

export interface AnalyticsDashboard {
  overview: AnalyticsOverview;
  attendanceByGrade: GradeAttendance[];
  marksByGrade: GradeMarks[];
  atRiskStudents: AtRiskStudent[];
  trend: MonthlyTrend[];
  scopeLabel: string;
  dateFrom: string;
  dateTo: string;
  attendanceThreshold: number;
  marksThreshold: number;
  generatedAtUtc: string;
}

export interface AnalyticsFilters {
  assignmentId: number | null;
  dateFrom: string;
  dateTo: string;
  attendanceThreshold: number;
  marksThreshold: number;
}
