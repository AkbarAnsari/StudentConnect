import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Activity, ActivityInput, ActivityStatus, ActivityUpdateInput } from './activity.model';

// ActivityComponent -> ActivityService -> HttpClient -> ASP.NET Core API -> EF Core -> SQL Server.
@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/activities`;

  getAll(): Observable<Activity[]> {
    return this.http.get<Activity[]>(this.baseUrl);
  }

  getById(id: number): Observable<Activity> {
    return this.http.get<Activity>(`${this.baseUrl}/${id}`);
  }

  getByGrade(gradeId: number): Observable<Activity[]> {
    return this.http.get<Activity[]>(`${this.baseUrl}/grade/${gradeId}`);
  }

  create(activity: ActivityInput): Observable<Activity> {
    return this.http.post<Activity>(this.baseUrl, activity);
  }

  update(id: number, activity: ActivityUpdateInput): Observable<Activity> {
    return this.http.put<Activity>(`${this.baseUrl}/${id}`, activity);
  }

  updateStatus(id: number, status: ActivityStatus): Observable<Activity> {
    return this.http.put<Activity>(`${this.baseUrl}/${id}/status`, { status });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  moderate(id: number, action: 'Submit' | 'Approve' | 'Reject', reason?: string): Observable<Activity> {
    return this.http.put<Activity>(`${this.baseUrl}/${id}/moderation`, { action, reason: reason || null });
  }
}
