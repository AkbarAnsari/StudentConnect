import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Assessment,
  AssessmentInput,
  AssessmentUpdateInput,
  AssessmentResult,
  BulkMarksInput,
  MarkRecord,
  MarkHistory,
  MarksSummary,
} from './marks.model';

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

  getMarkHistory(markId: number): Observable<MarkHistory[]> {
    return this.http.get<MarkHistory[]>(`${this.marksUrl}/${markId}/history`);
  }

  exportAssessmentResults(assessmentId: number): Observable<Blob> {
    return this.http.get(`${this.marksUrl}/assessment/${assessmentId}/export`, { responseType: 'blob' });
  }

  importAssessmentResults(assessmentId: number, sectionId: number, file: File): Observable<{ message: string; count: number }> {
    const formData = new FormData();
    formData.append('sectionId', String(sectionId));
    formData.append('file', file);
    return this.http.post<{ message: string; count: number }>(`${this.marksUrl}/assessment/${assessmentId}/import`, formData);
  }

  saveBulkMarks(request: BulkMarksInput): Observable<{ message: string; count: number }> {
    return this.http.post<{ message: string; count: number }>(`${this.marksUrl}/bulk`, request);
  }

  getAssessments(): Observable<Assessment[]> {
    return this.http.get<Assessment[]>(this.assessmentsUrl);
  }

  getAssessment(id: number): Observable<Assessment> {
    return this.http.get<Assessment>(`${this.assessmentsUrl}/${id}`);
  }

  updateAssessment(id: number, assessment: AssessmentUpdateInput): Observable<Assessment> {
    return this.http.put<Assessment>(`${this.assessmentsUrl}/${id}`, assessment);
  }

  getAssessmentsBySubject(subjectId: number): Observable<Assessment[]> {
    return this.http.get<Assessment[]>(`${this.assessmentsUrl}/subject/${subjectId}`);
  }

  createAssessment(assessment: AssessmentInput): Observable<Assessment> {
    return this.http.post<Assessment>(this.assessmentsUrl, assessment);
  }

  publishAssessment(id: number): Observable<Assessment> {
    return this.http.post<Assessment>(`${this.assessmentsUrl}/${id}/publish`, {});
  }

  reopenAssessment(id: number): Observable<Assessment> {
    return this.http.post<Assessment>(`${this.assessmentsUrl}/${id}/reopen`, {});
  }
}
