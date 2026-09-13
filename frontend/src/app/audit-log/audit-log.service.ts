import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuditLog } from './audit-log.model';

@Injectable({ providedIn: 'root' })
export class AuditLogService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/audit-logs`;

  getRecent(entityType?: string, take = 100): Observable<AuditLog[]> {
    const params: Record<string, string> = { take: take.toString() };
    if (entityType) {
      params['entityType'] = entityType;
    }
    return this.http.get<AuditLog[]>(this.baseUrl, { params });
  }
}
