import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SectionStudent } from '../academic-structure/academic-structure.model';
import { TeacherSubjectAssignment, TeacherSubjectAssignmentInput } from './teacher-subject-assignment.model';

@Injectable({ providedIn: 'root' })
export class TeacherSubjectAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/teacher-subject-assignments`;

  getAll(): Observable<TeacherSubjectAssignment[]> {
    return this.http.get<TeacherSubjectAssignment[]>(this.baseUrl);
  }

  getMine(): Observable<TeacherSubjectAssignment[]> {
    return this.http.get<TeacherSubjectAssignment[]>(`${this.baseUrl}/my`);
  }

  getStudents(id: number): Observable<SectionStudent[]> {
    return this.http.get<SectionStudent[]>(`${this.baseUrl}/${id}/students`);
  }

  create(assignment: TeacherSubjectAssignmentInput): Observable<TeacherSubjectAssignment> {
    return this.http.post<TeacherSubjectAssignment>(this.baseUrl, assignment);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
