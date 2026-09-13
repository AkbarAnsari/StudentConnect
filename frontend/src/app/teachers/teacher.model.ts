export interface Teacher {
  id: number;
  teacherId: string;
  firstName: string;
  lastName: string;
  email: string;
  username: string | null;
}

export interface TeacherCreateInput {
  teacherId: string;
  firstName: string;
  lastName: string;
  email: string;
  username?: string;
  password?: string;
}
