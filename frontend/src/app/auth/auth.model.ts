export type UserRole = 'Student' | 'Teacher' | 'Admin' | 'Parent';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface SchoolRegistrationRequest {
  schoolName: string;
  adminUsername: string;
  adminPassword: string;
  adminEmail: string;
}

export interface ForgotPasswordRequest {
  username: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface VerifyEmailRequest {
  token: string;
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
  refreshToken: string;
  username: string;
  role: UserRole;
  displayName: string;
  studentId: number | null;
  teacherId: number | null;
  emailVerified: boolean;
  children: ParentChild[] | null;
}

export interface CurrentUser {
  username: string;
  role: UserRole;
  displayName: string;
  studentId: number | null;
  teacherId: number | null;
  emailVerified: boolean;
  children: ParentChild[] | null;
}
