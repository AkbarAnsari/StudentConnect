import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult } from '../shared/paged-result.model';
import { AuditLog, AuditLogFilters } from './audit-log.model';

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

  getPaged(filters: AuditLogFilters): Observable<PagedResult<AuditLog>> {
    return this.http.get<PagedResult<AuditLog>>(`${this.baseUrl}/paged`, { params: this.params(filters) });
  }

  getById(id: number): Observable<AuditLog> {
    return this.http.get<AuditLog>(`${this.baseUrl}/${id}`);
  }

  export(filters: AuditLogFilters): Observable<Blob> {
    const { page: _page, pageSize: _pageSize, ...exportFilters } = filters;
    return this.http.get(`${this.baseUrl}/export`, { params: this.params(exportFilters), responseType: 'blob' });
  }

  private params(filters: Partial<AuditLogFilters>): Record<string, string> {
    return Object.fromEntries(Object.entries(filters).filter(([, value]) => value !== '').map(([key, value]) => [key, String(value)]));
  }
}
