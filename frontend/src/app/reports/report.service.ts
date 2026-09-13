import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SectionClassReport, StudentReportCard } from './report.model';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/reports`;

  getStudentReportCard(studentId: number): Observable<StudentReportCard> {
    return this.http.get<StudentReportCard>(`${this.baseUrl}/student/${studentId}/report-card`);
  }

  getTeacherClassReports(teacherId: number): Observable<SectionClassReport[]> {
    return this.http.get<SectionClassReport[]>(`${this.baseUrl}/teacher/${teacherId}/classes`);
  }
}
