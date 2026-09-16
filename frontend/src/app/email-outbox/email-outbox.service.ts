import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult } from '../shared/paged-result.model';
import { EmailOutboxEntry, EmailOutboxFilters } from './email-outbox.model';

@Injectable({ providedIn: 'root' })
export class EmailOutboxService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/email-outbox`;

  getRecent(take = 50): Observable<EmailOutboxEntry[]> {
    return this.http.get<EmailOutboxEntry[]>(this.baseUrl, { params: { take: take.toString() } });
  }

  retry(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/retry`, {});
  }

  getPaged(filters: EmailOutboxFilters): Observable<PagedResult<EmailOutboxEntry>> {
    return this.http.get<PagedResult<EmailOutboxEntry>>(`${this.baseUrl}/paged`, { params: this.params(filters) });
  }

  getById(id: number): Observable<EmailOutboxEntry> {
    return this.http.get<EmailOutboxEntry>(`${this.baseUrl}/${id}`);
  }

  retryFailed(ids: number[]): Observable<{ requested: number; retried: number }> {
    return this.http.post<{ requested: number; retried: number }>(`${this.baseUrl}/retry-failed`, { ids });
  }
  assignToMe(id:number,resolutionNotes:string):Observable<void>{return this.http.post<void>(`${this.baseUrl}/${id}/assign-to-me`,{resolutionNotes});}

  private params(filters: EmailOutboxFilters): Record<string, string> {
    return Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== '').map(([key, value]) => [key, String(value)]));
  }
}
