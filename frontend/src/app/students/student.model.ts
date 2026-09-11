export interface Student {
  id: number;
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
  gradeId: number;
  gradeName: string;
  section: string;
}

export interface Grade {
  id: number;
  name: string;
}

export interface StudentInput {
  studentId: string;
  firstName: string;
  lastName: string;
  email: string;
  gradeId: number;
  section: string;
}
