import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AttendanceCreateInput,
  AttendanceCorrection,
  AttendanceRecord,
  AttendanceSummary,
  BulkAttendanceInput,
  Subject,
} from './attendance.model';

// Components use this service for all attendance API requests.
@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/attendance`;
  private readonly subjectsUrl = `${environment.apiBaseUrl}/subjects`;

  getStudentAttendance(studentId: number): Observable<AttendanceRecord[]> {
    return this.http.get<AttendanceRecord[]>(`${this.baseUrl}/student/${studentId}`);
  }

  getStudentSubjectAttendance(studentId: number, subjectId: number, dateFrom?: string, dateTo?: string): Observable<AttendanceRecord[]> {
    return this.http.get<AttendanceRecord[]>(`${this.baseUrl}/student/${studentId}/subject/${subjectId}`, { params: this.dateParams(dateFrom, dateTo) });
  }

  getStudentSubjectAttendanceCorrections(studentId: number, subjectId: number, dateFrom?: string, dateTo?: string): Observable<AttendanceCorrection[]> {
    return this.http.get<AttendanceCorrection[]>(`${this.baseUrl}/student/${studentId}/subject/${subjectId}/corrections`, { params: this.dateParams(dateFrom, dateTo) });
  }

  getStudentAttendanceSummary(studentId: number, dateFrom?: string, dateTo?: string): Observable<AttendanceSummary[]> {
    return this.http.get<AttendanceSummary[]>(`${this.baseUrl}/student/${studentId}/summary`, { params: this.dateParams(dateFrom, dateTo) });
  }

  // Every already-recorded status for a subject+date, used to prefill the Record Attendance
  // grid so re-saving a past date doesn't silently overwrite real data with defaults.
  getBySubjectAndDate(subjectId: number, date: string): Observable<AttendanceRecord[]> {
    return this.http.get<AttendanceRecord[]>(`${this.baseUrl}/subject/${subjectId}/date/${date}`);
  }

  createAttendance(record: AttendanceCreateInput): Observable<AttendanceRecord> {
    return this.http.post<AttendanceRecord>(this.baseUrl, record);
  }

  updateAttendance(id: number, status: AttendanceStatusInput): Observable<AttendanceRecord> {
    return this.http.put<AttendanceRecord>(`${this.baseUrl}/${id}`, status);
  }

  deleteAttendance(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  saveBulkAttendance(request: BulkAttendanceInput): Observable<{ message: string; count: number }> {
    return this.http.post<{ message: string; count: number }>(`${this.baseUrl}/bulk`, request);
  }

  getSubjects(): Observable<Subject[]> {
    return this.http.get<Subject[]>(this.subjectsUrl);
  }

  private dateParams(dateFrom?: string, dateTo?: string): HttpParams {
    let params = new HttpParams();
    if (dateFrom) params = params.set('dateFrom', dateFrom);
    if (dateTo) params = params.set('dateTo', dateTo);
    return params;
  }
}

interface AttendanceStatusInput {
  status: 'Present' | 'Absent' | 'Late' | 'Excused';
  reason?: string;
}
