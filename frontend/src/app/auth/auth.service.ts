import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { CurrentUser, LoginRequest, LoginResponse } from './auth.model';

// Development-only local authentication (see docs/phase-10.md). Holds the access token and
// current user in memory + localStorage, and is the single place the rest of the Angular
// app asks "who is logged in?" — no other service or component talks to /api/auth directly.
const TOKEN_STORAGE_KEY = 'studentconnect.token';
const USER_STORAGE_KEY = 'studentconnect.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly loginUrl = `${environment.apiBaseUrl}/auth/login`;

  private readonly currentUserSignal = signal<CurrentUser | null>(this.readStoredUser());

  readonly currentUser = this.currentUserSignal.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUserSignal() !== null);
  readonly role = computed(() => this.currentUserSignal()?.role ?? null);

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(this.loginUrl, request).pipe(
      tap((response) => {
        const user: CurrentUser = {
          username: response.username,
          role: response.role,
          displayName: response.displayName,
          studentId: response.studentId,
          teacherId: response.teacherId,
          children: response.children,
        };
        localStorage.setItem(TOKEN_STORAGE_KEY, response.token);
        localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
        this.currentUserSignal.set(user);
      }),
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_STORAGE_KEY);
    localStorage.removeItem(USER_STORAGE_KEY);
    this.currentUserSignal.set(null);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_STORAGE_KEY);
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
