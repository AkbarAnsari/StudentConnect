import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Announcement, AnnouncementAudiencePreview, AnnouncementCreateInput, AnnouncementScopeType } from './announcement.model';

@Injectable({ providedIn: 'root' })
export class AnnouncementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/announcements`;

  getRecent(take = 20): Observable<Announcement[]> {
    return this.http.get<Announcement[]>(this.baseUrl, { params: { take } });
  }

  create(announcement: AnnouncementCreateInput, attachment?: File | null): Observable<Announcement> {
    const formData = new FormData();
    for (const [key, value] of Object.entries(announcement)) if (value !== undefined && value !== null) formData.append(key, String(value));
    if (attachment) formData.append('attachment', attachment);
    return this.http.post<Announcement>(this.baseUrl, formData);
  }

  previewAudience(scopeType: AnnouncementScopeType, scopeGradeId?: number, scopeSectionId?: number): Observable<AnnouncementAudiencePreview> {
    return this.http.post<AnnouncementAudiencePreview>(`${this.baseUrl}/preview-audience`, { scopeType, scopeGradeId, scopeSectionId });
  }

  publish(id: number): Observable<Announcement> {
    return this.http.post<Announcement>(`${this.baseUrl}/${id}/publish`, {});
  }

  schedule(id: number, scheduledForUtc: string): Observable<Announcement> {
    return this.http.post<Announcement>(`${this.baseUrl}/${id}/schedule`, { scheduledForUtc });
  }

  withdraw(id: number): Observable<Announcement> {
    return this.http.post<Announcement>(`${this.baseUrl}/${id}/withdraw`, {});
  }

  updateDraft(id: number, title: string, body: string, priority: string, expiresAtUtc: string | null): Observable<Announcement> {
    return this.http.put<Announcement>(`${this.baseUrl}/${id}`, { title, body, priority, expiresAtUtc });
  }

  retry(id: number): Observable<Announcement> { return this.http.post<Announcement>(`${this.baseUrl}/${id}/retry`, {}); }
  downloadAttachment(id: number): Observable<Blob> { return this.http.get(`${this.baseUrl}/${id}/attachment`, { responseType: 'blob' }); }
}
