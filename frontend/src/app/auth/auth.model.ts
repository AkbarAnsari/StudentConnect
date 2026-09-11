export type UserRole = 'Student' | 'Teacher' | 'Admin' | 'Parent';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface ParentChild {
  id: number;
  studentId: string;
  firstName: string;
  lastName: string;
  gradeName: string;
  section: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  role: UserRole;
  displayName: string;
  studentId: number | null;
  teacherId: number | null;
  children: ParentChild[] | null;
}

export interface CurrentUser {
  username: string;
  role: UserRole;
  displayName: string;
  studentId: number | null;
  teacherId: number | null;
  children: ParentChild[] | null;
}
