export type UserRole = 'Student' | 'Teacher' | 'Admin';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  username: string;
  role: UserRole;
  displayName: string;
  studentId: number | null;
  teacherId: number | null;
}

export interface CurrentUser {
  username: string;
  role: UserRole;
  displayName: string;
  studentId: number | null;
  teacherId: number | null;
}
