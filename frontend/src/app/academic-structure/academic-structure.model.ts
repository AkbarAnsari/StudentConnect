export interface AcademicYear {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
  isArchived: boolean;
}

export interface AcademicYearInput {
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
}

export interface Term {
  id: number;
  academicYearId: number;
  academicYearName: string;
  name: string;
  startDate: string;
  endDate: string;
  isLocked: boolean;
}

export interface TermInput {
  academicYearId: number;
  name: string;
  startDate: string;
  endDate: string;
}

export interface SectionInput {
  gradeId: number;
  name: string;
  homeroomTeacherId?: number;
}

export interface GradeInput {
  name: string;
}

export interface SubjectInput {
  name: string;
  code: string;
}

export interface SectionStudent {
  id: number;
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
}

export interface GradingBand {
  id: number;
  label: string;
  minPercentage: number;
}

export interface GradingBandInput {
  label: string;
  minPercentage: number;
}

export interface RolloverStudentRow {
  studentId: number;
  studentName: string;
  currentGradeId: number;
  currentGradeName: string;
  currentSectionId: number | null;
  currentSectionName: string | null;
  proposedGradeId: number | null;
  proposedGradeName: string | null;
  proposedSectionId: number | null;
  proposedSectionName: string | null;
  excluded: boolean;
  exclusionReason: string | null;
}

export interface RolloverCommitResult {
  promoted: number;
  skipped: number;
}
