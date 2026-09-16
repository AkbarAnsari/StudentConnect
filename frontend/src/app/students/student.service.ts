import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult } from '../shared/paged-result.model';
import { Grade, Section, Student, StudentBulkStatusResult, StudentHistoryEntry, StudentInput, StudentImportResult, StudentLifecycleStatus, StudentStatusUpdate, StudentSubject } from './student.model';

// Phase 4: the API now persists data via EF Core + SQL Server, but from
// Angular's perspective nothing changes — it still just talks HTTP to the API.
@Injectable({ providedIn: 'root' })
export class StudentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/students`;
  private readonly gradesUrl = `${environment.apiBaseUrl}/grades`;
  private readonly sectionsUrl = `${environment.apiBaseUrl}/sections`;
  private readonly subjectsUrl = `${environment.apiBaseUrl}/subjects`;

  // status defaults to "Active" server-side when omitted; pass 'All' to include every status.
  // pageSize defaults generously so existing "just give me the roster" callers (dropdowns,
  // record-attendance, enter-marks, manage-parents) keep working unchanged; the Students list
  // page itself passes its own smaller page/pageSize and surfaces the real paging metadata.
  getStudents(
    status?: string,
    page = 1,
    pageSize = 200,
    gradeId?: number,
    query?: string,
    sort?: string,
    sectionId?: number,
  ): Observable<PagedResult<Student>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    if (gradeId) {
      params = params.set('gradeId', gradeId);
    }
    if (query) {
      params = params.set('q', query);
    }
    if (sort) {
      params = params.set('sort', sort);
    }
    if (sectionId) {
      params = params.set('sectionId', sectionId);
    }
    return this.http.get<PagedResult<Student>>(this.baseUrl, { params });
  }

  getStudentById(id: number): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/${id}`);
  }

  getStudentSubjects(id: number): Observable<StudentSubject[]> {
    return this.http.get<StudentSubject[]>(`${this.baseUrl}/${id}/subjects`);
  }

  getStudentHistory(id: number): Observable<StudentHistoryEntry[]> {
    return this.http.get<StudentHistoryEntry[]>(`${this.baseUrl}/${id}/history`);
  }

  createStudent(student: StudentInput): Observable<Student> {
    return this.http.post<Student>(this.baseUrl, student);
  }

  updateStudent(id: number, student: Omit<StudentInput, 'studentId'>): Observable<Student> {
    return this.http.put<Student>(`${this.baseUrl}/${id}`, student);
  }

  deleteStudent(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  updateStatus(id: number, update: StudentStatusUpdate): Observable<Student> {
    return this.http.put<Student>(`${this.baseUrl}/${id}/status`, update);
  }

  updateStatuses(studentIds: number[], status: StudentLifecycleStatus, reason: string): Observable<StudentBulkStatusResult> {
    return this.http.put<StudentBulkStatusResult>(`${this.baseUrl}/status/bulk`, { studentIds, status, reason });
  }

  exportStudents(status?: string, gradeId?: number, sectionId?: number, query?: string, sort?: string): Observable<Blob> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (gradeId) params = params.set('gradeId', gradeId);
    if (sectionId) params = params.set('sectionId', sectionId);
    if (query) params = params.set('q', query);
    if (sort) params = params.set('sort', sort);
    return this.http.get(`${this.baseUrl}/export`, { params, responseType: 'blob' });
  }

  importStudents(file: File): Observable<StudentImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<StudentImportResult>(`${this.baseUrl}/import`, formData);
  }

  getGrades(): Observable<Grade[]> {
    return this.http.get<Grade[]>(this.gradesUrl);
  }

  getSubjects(): Observable<StudentSubject[]> {
    return this.http.get<StudentSubject[]>(this.subjectsUrl);
  }

  getSectionsByGrade(gradeId: number): Observable<Section[]> {
    return this.http.get<Section[]>(`${this.sectionsUrl}/grade/${gradeId}`);
  }

  getResolvedCurriculumDefaults(gradeId: number, sectionId?: number): Observable<{ gradeId: number; sectionId: number | null; subjectIds: number[] }> {
    let params = new HttpParams().set('gradeId', gradeId);
    if (sectionId) params = params.set('sectionId', sectionId);
    return this.http.get<{ gradeId: number; sectionId: number | null; subjectIds: number[] }>(`${environment.apiBaseUrl}/curriculum-defaults/resolved`, { params });
  }
}
