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
}
