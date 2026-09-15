import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  CurrentUser,
  ForgotPasswordRequest,
  LoginRequest,
  LoginResponse,
  ResetPasswordRequest,
  SchoolRegistrationRequest,
  VerifyEmailRequest,
} from './auth.model';

// Development-only local authentication. Holds the access token and
// current user in memory + localStorage, and is the single place the rest of the Angular
// app asks "who is logged in?" — no other service or component talks to /api/auth directly.
const TOKEN_STORAGE_KEY = 'studentconnect.token';
const REFRESH_TOKEN_STORAGE_KEY = 'studentconnect.refreshToken';
const USER_STORAGE_KEY = 'studentconnect.user';

// Renew this many milliseconds before the access token actually expires, so a request made
// right at the boundary never races an already-expired token.
const REFRESH_SAFETY_MARGIN_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly loginUrl = `${environment.apiBaseUrl}/auth/login`;
  private readonly registerSchoolUrl = `${environment.apiBaseUrl}/schools/register`;
  private readonly forgotPasswordUrl = `${environment.apiBaseUrl}/auth/forgot-password`;
  private readonly resetPasswordUrl = `${environment.apiBaseUrl}/auth/reset-password`;
  private readonly refreshUrl = `${environment.apiBaseUrl}/auth/refresh`;
  private readonly logoutUrl = `${environment.apiBaseUrl}/auth/logout`;
  private readonly verifyEmailUrl = `${environment.apiBaseUrl}/auth/verify-email`;
  private readonly resendVerificationUrl = `${environment.apiBaseUrl}/auth/resend-verification`;

  private readonly currentUserSignal = signal<CurrentUser | null>(this.readStoredUser());
  private refreshTimer: ReturnType<typeof setTimeout> | null = null;

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);
  readonly role = computed(() => this.currentUserSignal()?.role ?? null);

  constructor() {
    // Resume proactive renewal for an already-logged-in session restored from localStorage.
    const token = this.getToken();
    if (token) {
      this.scheduleRefresh(token);
    }
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(this.loginUrl, request).pipe(tap((response) => this.storeSession(response)));
  }

  // Self-service tenant onboarding: creates a brand-new school + its first Admin account,
  // and logs that admin in immediately using the returned token (same session-storage flow
  // as login()) — no separate login step needed after registering.
  registerSchool(request: SchoolRegistrationRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(this.registerSchoolUrl, request).pipe(tap((response) => this.storeSession(response)));
  }

  forgotPassword(request: ForgotPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(this.forgotPasswordUrl, request);
  }

  resetPassword(request: ResetPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(this.resetPasswordUrl, request);
  }

  // Exchanges the stored refresh token for a new access+refresh token pair.
  refresh(): Observable<LoginResponse> {
    const refreshToken = this.getRefreshToken() ?? '';
    return this.http.post<LoginResponse>(this.refreshUrl, { refreshToken }).pipe(tap((response) => this.storeSession(response)));
  }

  verifyEmail(request: VerifyEmailRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(this.verifyEmailUrl, request);
  }

  resendVerification(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(this.resendVerificationUrl, {});
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      // Best-effort server-side revocation — don't block clearing the local session on it.
      this.http.post(this.logoutUrl, { refreshToken }).subscribe({ error: () => undefined });
    }

    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }

    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
    localStorage.removeItem(USER_STORAGE_KEY);
    this.currentUserSignal.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
  }

  private storeSession(response: LoginResponse): void {
    const user: CurrentUser = {
      username: response.username,
      role: response.role,
      displayName: response.displayName,
      studentId: response.studentId,
      teacherId: response.teacherId,
      emailVerified: response.emailVerified,
      children: response.children,
    };
    localStorage.setItem(TOKEN_STORAGE_KEY, response.token);
    localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, response.refreshToken);
    localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
    this.currentUserSignal.set(user);
    this.scheduleRefresh(response.token);
  }

  // Proactively renews the access token shortly before it expires, based on the JWT's own
  // "exp" claim — avoids a reactive 401-retry-queue while still keeping sessions alive.
  private scheduleRefresh(token: string): void {
    if (this.refreshTimer) {
      clearTimeout(this.refreshTimer);
      this.refreshTimer = null;
    }

    const expiryMs = this.getTokenExpiryMs(token);
    if (expiryMs === null) {
      return;
    }

    const delay = Math.max(expiryMs - Date.now() - REFRESH_SAFETY_MARGIN_MS, 5_000);
    this.refreshTimer = setTimeout(() => {
      this.refresh().subscribe({ error: () => this.logout() });
    }, delay);
  }

  private getTokenExpiryMs(token: string): number | null {
    try {
      const payload = token.split('.')[1];
      const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
      const decoded = JSON.parse(atob(normalized)) as { exp?: number };
      return typeof decoded.exp === 'number' ? decoded.exp * 1000 : null;
    } catch {
      return null;
    }
  }

  private readStoredUser(): CurrentUser | null {
    const raw = localStorage.getItem(USER_STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as CurrentUser;
    } catch {
      return null;
    }
  }
}
