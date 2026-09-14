import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AnalyticsOverview, AtRiskStudent, GradeAttendance, GradeMarks, MonthlyTrend } from './analytics.model';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/analytics`;

  getOverview(): Observable<AnalyticsOverview> {
    return this.http.get<AnalyticsOverview>(`${this.baseUrl}/overview`);
  }

  getAttendanceByGrade(): Observable<GradeAttendance[]> {
    return this.http.get<GradeAttendance[]>(`${this.baseUrl}/attendance-by-grade`);
  }

  getMarksByGrade(): Observable<GradeMarks[]> {
    return this.http.get<GradeMarks[]>(`${this.baseUrl}/marks-by-grade`);
  }

  getAtRiskStudents(): Observable<AtRiskStudent[]> {
    return this.http.get<AtRiskStudent[]>(`${this.baseUrl}/at-risk-students`);
  }

  getTrend(months = 12): Observable<MonthlyTrend[]> {
    return this.http.get<MonthlyTrend[]>(`${this.baseUrl}/trend`, { params: { months: months.toString() } });
  }
}
