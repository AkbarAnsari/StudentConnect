import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AccountProfile, UserAccess, UserInvitationInput, UserSession } from './auth.model';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly authUrl = `${environment.apiBaseUrl}/auth`;
  private readonly usersUrl = `${environment.apiBaseUrl}/users`;

  getAccount(): Observable<AccountProfile> {
    return this.http.get<AccountProfile>(`${this.authUrl}/account`);
  }

  updateAccount(email: string | null): Observable<AccountProfile> {
    return this.http.put<AccountProfile>(`${this.authUrl}/account`, { email });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.authUrl}/change-password`, { currentPassword, newPassword });
  }

  revokeSessions(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.authUrl}/revoke-sessions`, {});
  }

  getUsers(): Observable<UserAccess[]> {
    return this.http.get<UserAccess[]>(this.usersUrl);
  }

  deactivate(id: number): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/${id}/deactivate`, {});
  }

  reactivate(id: number): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/${id}/reactivate`, {});
  }

  sendPasswordInvitation(id: number): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.usersUrl}/${id}/password-invitation`, {});
  }

  createInvitation(input: UserInvitationInput): Observable<UserAccess> {
    return this.http.post<UserAccess>(`${this.usersUrl}/invitations`, input);
  }

  updateManagedContact(id: number, email: string | null): Observable<UserAccess> {
    return this.http.put<UserAccess>(`${this.usersUrl}/${id}/contact`, { email });
  }

  getSessions(id: number): Observable<UserSession[]> {
    return this.http.get<UserSession[]>(`${this.usersUrl}/${id}/sessions`);
  }

  revokeSession(id: number, sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.usersUrl}/${id}/sessions/${sessionId}`);
  }
}
