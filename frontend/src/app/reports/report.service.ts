import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ReportIssuanceSummary, ReportPeriod, ReportTemplate, SectionClassReport, StudentReportCard, StudentTranscript, TranscriptIssuanceSummary } from './report.model';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/reports`;

  getStudentReportCard(studentId: number, termId?: number | null, issuanceId?: number | null): Observable<StudentReportCard> {
    const params: Record<string, string> = {};
    if (termId) params['termId'] = String(termId);
    if (issuanceId) params['issuanceId'] = String(issuanceId);
    return this.http.get<StudentReportCard>(`${this.baseUrl}/student/${studentId}/report-card`, { params });
  }

  getReportPeriods(): Observable<ReportPeriod[]> {
    return this.http.get<ReportPeriod[]>(`${this.baseUrl}/periods`);
  }

  getReportCardHistory(studentId: number): Observable<ReportIssuanceSummary[]> {
    return this.http.get<ReportIssuanceSummary[]>(`${this.baseUrl}/student/${studentId}/report-card/history`);
  }

  issueStudentReportCard(studentId: number, termId: number, comments: string, templateId?: number | null): Observable<StudentReportCard> {
    return this.http.post<StudentReportCard>(`${this.baseUrl}/student/${studentId}/report-card/issue`, { termId, comments, templateId });
  }

  getTeacherClassReports(teacherId: number): Observable<SectionClassReport[]> {
    return this.http.get<SectionClassReport[]>(`${this.baseUrl}/teacher/${teacherId}/classes`);
  }

  exportStudentReportCard(studentId: number, termId?: number | null, issuanceId?: number | null): Observable<Blob> {
    const params: Record<string, string> = {};
    if (termId) params['termId'] = String(termId);
    if (issuanceId) params['issuanceId'] = String(issuanceId);
    return this.http.get(`${this.baseUrl}/student/${studentId}/report-card/export`, { params, responseType: 'blob' });
  }

  exportTeacherClassReports(teacherId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/teacher/${teacherId}/classes/export`, { responseType: 'blob' });
  }

  getStudentTranscript(studentId: number, issuanceId?: number): Observable<StudentTranscript> {
    return this.http.get<StudentTranscript>(`${this.baseUrl}/student/${studentId}/transcript`, { params: issuanceId ? { issuanceId } : {} });
  }

  exportStudentTranscript(studentId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/student/${studentId}/transcript/export`, { responseType: 'blob' });
  }

  getTemplates():Observable<ReportTemplate[]>{return this.http.get<ReportTemplate[]>(`${environment.apiBaseUrl}/report-templates`);}
  saveTemplate(template:ReportTemplate):Observable<ReportTemplate>{return this.http.post<ReportTemplate>(`${environment.apiBaseUrl}/report-templates`,template);}
  issueBatch(termId:number,gradeId:number|null,templateId:number|null,comments:string):Observable<{issued:number;skipped:number}>{return this.http.post<{issued:number;skipped:number}>(`${this.baseUrl}/report-cards/batch`,{termId,gradeId,templateId,comments});}
  signIssuance(issuanceId:number,label:string,signedName:string):Observable<void>{return this.http.put<void>(`${this.baseUrl}/issuances/${issuanceId}/sign`,{label,signedName});}
  issueTranscript(studentId:number):Observable<TranscriptIssuanceSummary>{return this.http.post<TranscriptIssuanceSummary>(`${this.baseUrl}/student/${studentId}/transcript/issue`,{});}
  getTranscriptHistory(studentId:number):Observable<TranscriptIssuanceSummary[]>{return this.http.get<TranscriptIssuanceSummary[]>(`${this.baseUrl}/student/${studentId}/transcript/history`);}
}
