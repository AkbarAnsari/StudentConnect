import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Grade, Student, StudentInput } from './student.model';

// Phase 4: the API now persists data via EF Core + SQL Server, but from
// Angular's perspective nothing changes — it still just talks HTTP to the API.
@Injectable({ providedIn: 'root' })
export class StudentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/students`;
  private readonly gradesUrl = `${environment.apiBaseUrl}/grades`;

  getStudents(): Observable<Student[]> {
    return this.http.get<Student[]>(this.baseUrl);
  }

  getStudentById(id: number): Observable<Student> {
    return this.http.get<Student>(`${this.baseUrl}/${id}`);
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

  getGrades(): Observable<Grade[]> {
    return this.http.get<Grade[]>(this.gradesUrl);
  }
}
