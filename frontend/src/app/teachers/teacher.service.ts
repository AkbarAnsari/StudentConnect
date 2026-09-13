import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Teacher, TeacherCreateInput } from './teacher.model';

@Injectable({ providedIn: 'root' })
export class TeacherService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/teachers`;

  getTeachers(): Observable<Teacher[]> {
    return this.http.get<Teacher[]>(this.baseUrl);
  }

  createTeacher(teacher: TeacherCreateInput): Observable<Teacher> {
    return this.http.post<Teacher>(this.baseUrl, teacher);
  }

  deleteTeacher(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
