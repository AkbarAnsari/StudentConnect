import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AnalyticsDashboard, AnalyticsFilters, AnalyticsOverview, AtRiskStudent, GradeAttendance, GradeMarks, MonthlyTrend } from './analytics.model';

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

  getDashboard(filters: AnalyticsFilters): Observable<AnalyticsDashboard> {
    const params: Record<string, string> = {
      dateFrom: filters.dateFrom,
      dateTo: filters.dateTo,
      attendanceThreshold: String(filters.attendanceThreshold),
      marksThreshold: String(filters.marksThreshold),
    };
    if (filters.assignmentId !== null) params['assignmentId'] = String(filters.assignmentId);
    return this.http.get<AnalyticsDashboard>(`${this.baseUrl}/dashboard`, { params });
  }

  exportDashboard(filters: AnalyticsFilters): Observable<Blob> {
    const params: Record<string, string> = {
      dateFrom: filters.dateFrom,
      dateTo: filters.dateTo,
      attendanceThreshold: String(filters.attendanceThreshold),
      marksThreshold: String(filters.marksThreshold),
    };
    if (filters.assignmentId !== null) params['assignmentId'] = String(filters.assignmentId);
    return this.http.get(`${this.baseUrl}/dashboard/export`, { params, responseType: 'blob' });
  }
}
