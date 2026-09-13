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
}

export interface Grade {
  id: number;
  name: string;
}

export interface Section {
  id: number;
  gradeId: number;
  gradeName: string;
  name: string;
}

export interface StudentInput {
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
  gradeId: number;
  sectionId: number;
}
