export interface AcademicYear {
  id: number;
  name: string;
  startDate: string;
  endDate: string;
  isCurrent: boolean;
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
