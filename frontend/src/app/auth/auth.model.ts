export type UserRole = 'Student' | 'Teacher' | 'Admin' | 'Parent' | 'Coordinator';

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

export interface AccountProfile {
  id: number;
  username: string;
  displayName: string;
  role: UserRole;
  email: string | null;
  emailVerified: boolean;
  status: 'Active' | 'Deactivated';
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface UserAccess extends AccountProfile {
  studentId: number | null;
  teacherId: number | null;
  deactivatedAtUtc: string | null;
}

export interface UserInvitationInput {
  username: string;
  email: string;
  role: UserRole;
  studentId?: number;
  teacherId?: number;
}

export interface UserSession {
  sessionId: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  userAgent: string;
  ipAddress: string;
}
