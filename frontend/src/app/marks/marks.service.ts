import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Assessment,
  AssessmentInput,
  AssessmentResult,
  BulkMarksInput,
  MarkRecord,
  MarksSummary,
} from './marks.model';

// MarksComponent -> MarksService -> HttpClient -> ASP.NET Core API -> EF Core -> SQL Server.
@Injectable({ providedIn: 'root' })
export class MarksService {
  private readonly http = inject(HttpClient);
  private readonly marksUrl = `${environment.apiBaseUrl}/marks`;
  private readonly assessmentsUrl = `${environment.apiBaseUrl}/assessments`;

  getStudentMarks(studentId: number): Observable<MarkRecord[]> {
    return this.http.get<MarkRecord[]>(`${this.marksUrl}/student/${studentId}`);
  }

  getStudentSubjectMarks(studentId: number, subjectId: number): Observable<MarkRecord[]> {
    return this.http.get<MarkRecord[]>(`${this.marksUrl}/student/${studentId}/subject/${subjectId}`);
  }

  getStudentMarksSummary(studentId: number): Observable<MarksSummary[]> {
    return this.http.get<MarksSummary[]>(`${this.marksUrl}/student/${studentId}/summary`);
  }

  getAssessmentResults(assessmentId: number): Observable<AssessmentResult[]> {
    return this.http.get<AssessmentResult[]>(`${this.marksUrl}/assessment/${assessmentId}`);
  }

  saveBulkMarks(request: BulkMarksInput): Observable<{ message: string; count: number }> {
    return this.http.post<{ message: string; count: number }>(`${this.marksUrl}/bulk`, request);
  }

  getAssessments(): Observable<Assessment[]> {
    return this.http.get<Assessment[]>(this.assessmentsUrl);
  }

  getAssessmentsBySubject(subjectId: number): Observable<Assessment[]> {
    return this.http.get<Assessment[]>(`${this.assessmentsUrl}/subject/${subjectId}`);
  }

  createAssessment(assessment: AssessmentInput): Observable<Assessment> {
    return this.http.post<Assessment>(this.assessmentsUrl, assessment);
  }
}
