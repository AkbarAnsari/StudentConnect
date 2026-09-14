import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Announcement, AnnouncementCreateInput } from './announcement.model';

@Injectable({ providedIn: 'root' })
export class AnnouncementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/announcements`;

  getRecent(take = 20): Observable<Announcement[]> {
    return this.http.get<Announcement[]>(this.baseUrl, { params: { take } });
  }

  create(announcement: AnnouncementCreateInput): Observable<Announcement> {
    return this.http.post<Announcement>(this.baseUrl, announcement);
  }
}
