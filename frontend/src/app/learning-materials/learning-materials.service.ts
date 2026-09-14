import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { LearningMaterial, LearningMaterialFilter } from './learning-material.model';

// LearningMaterialsComponent -> LearningMaterialsService -> HttpClient -> ASP.NET Core API
// -> SQL Server (metadata) + local file storage (actual file). Angular never talks to
// either store directly, and never knows whether files live on disk or, later, in Azure Blob Storage.
@Injectable({ providedIn: 'root' })
export class LearningMaterialsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/learning-materials`;

  getAll(filter?: LearningMaterialFilter): Observable<LearningMaterial[]> {
    let params = new HttpParams();
    if (filter?.subjectId) {
      params = params.set('subjectId', filter.subjectId);
    }
    if (filter?.gradeId) {
      params = params.set('gradeId', filter.gradeId);
    }
    return this.http.get<LearningMaterial[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<LearningMaterial> {
    return this.http.get<LearningMaterial>(`${this.baseUrl}/${id}`);
  }

  // Fetched via HttpClient (blob) so the auth interceptor attaches the Bearer token — a plain
  // anchor href would bypass it entirely and 401.
  downloadFile(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/download`, { responseType: 'blob' });
  }

  getForStudent(studentId: number): Observable<LearningMaterial[]> {
    return this.http.get<LearningMaterial[]>(`${this.baseUrl}/student/${studentId}`);
  }

  getForTeacher(teacherId: number): Observable<LearningMaterial[]> {
    return this.http.get<LearningMaterial[]>(`${this.baseUrl}/teacher/${teacherId}`);
  }

  upload(formData: FormData): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(this.baseUrl, formData);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
