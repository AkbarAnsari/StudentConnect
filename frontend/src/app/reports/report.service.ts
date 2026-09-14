import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SectionClassReport, StudentReportCard, StudentTranscript } from './report.model';

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

  exportStudentReportCard(studentId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/student/${studentId}/report-card/export`, { responseType: 'blob' });
  }

  exportTeacherClassReports(teacherId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/teacher/${teacherId}/classes/export`, { responseType: 'blob' });
  }

  getStudentTranscript(studentId: number): Observable<StudentTranscript> {
    return this.http.get<StudentTranscript>(`${this.baseUrl}/student/${studentId}/transcript`);
  }

  exportStudentTranscript(studentId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/student/${studentId}/transcript/export`, { responseType: 'blob' });
  }
}
