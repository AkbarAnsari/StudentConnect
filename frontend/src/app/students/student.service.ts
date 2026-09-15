import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult } from '../shared/paged-result.model';
import { Grade, Section, Student, StudentInput, StudentImportResult, StudentStatusUpdate, StudentSubject } from './student.model';

// Phase 4: the API now persists data via EF Core + SQL Server, but from
// Angular's perspective nothing changes — it still just talks HTTP to the API.
@Injectable({ providedIn: 'root' })
export class StudentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/students`;
  private readonly gradesUrl = `${environment.apiBaseUrl}/grades`;
  private readonly sectionsUrl = `${environment.apiBaseUrl}/sections`;

  // status defaults to "Active" server-side when omitted; pass 'All' to include every status.
  // pageSize defaults generously so existing "just give me the roster" callers (dropdowns,
  // record-attendance, enter-marks, manage-parents) keep working unchanged; the Students list
  // page itself passes its own smaller page/pageSize and surfaces the real paging metadata.
  getStudents(status?: string, page = 1, pageSize = 200, gradeId?: number): Observable<PagedResult<Student>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    if (gradeId) {
      params = params.set('gradeId', gradeId);
    }
    return this.http.get<PagedResult<Student>>(this.baseUrl, { params });
  }

  getStudentById(id: number): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/${id}`);
  }

  getStudentSubjects(id: number): Observable<StudentSubject[]> {
    return this.http.get<StudentSubject[]>(`${this.baseUrl}/${id}/subjects`);
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

  importStudents(file: File): Observable<StudentImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<StudentImportResult>(`${this.baseUrl}/import`, formData);
  }

  getGrades(): Observable<Grade[]> {
    return this.http.get<Grade[]>(this.gradesUrl);
  }

  getSectionsByGrade(gradeId: number): Observable<Section[]> {
    return this.http.get<Section[]>(`${this.sectionsUrl}/grade/${gradeId}`);
  }
}
