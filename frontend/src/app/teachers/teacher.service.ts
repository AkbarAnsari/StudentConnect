import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PagedResult } from '../shared/paged-result.model';
import { Teacher, TeacherCreateInput, TeacherUpdateInput } from './teacher.model';

@Injectable({ providedIn: 'root' })
export class TeacherService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/teachers`;

  getTeachers(page = 1, pageSize = 100): Observable<PagedResult<Teacher>> {
    return this.http.get<PagedResult<Teacher>>(this.baseUrl, { params: { page, pageSize } });
  }

  createTeacher(teacher: TeacherCreateInput): Observable<Teacher> {
    return this.http.post<Teacher>(this.baseUrl, teacher);
  }

  updateTeacher(id: number, teacher: TeacherUpdateInput): Observable<Teacher> {
    return this.http.put<Teacher>(`${this.baseUrl}/${id}`, teacher);
  }

  deleteTeacher(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
