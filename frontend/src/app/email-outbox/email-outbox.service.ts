import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { EmailOutboxEntry } from './email-outbox.model';

@Injectable({ providedIn: 'root' })
export class EmailOutboxService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/email-outbox`;

  getRecent(take = 50): Observable<EmailOutboxEntry[]> {
    return this.http.get<EmailOutboxEntry[]>(this.baseUrl, { params: { take: take.toString() } });
  }
}
