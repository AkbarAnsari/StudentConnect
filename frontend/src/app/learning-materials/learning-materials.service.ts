import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { LearningMaterial, LearningMaterialFilter, LearningMaterialVersion } from './learning-material.model';

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

  getVersions(id: number): Observable<LearningMaterialVersion[]> {
    return this.http.get<LearningMaterialVersion[]>(`${this.baseUrl}/${id}/versions`);
  }

  replaceFile(id: number, formData: FormData): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(`${this.baseUrl}/${id}/replace-file`, formData);
  }

  downloadVersion(materialId: number, versionId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${materialId}/versions/${versionId}/download`, { responseType: 'blob' });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  publish(id: number): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(`${this.baseUrl}/${id}/publish`, {});
  }

  schedule(id: number, scheduledForUtc: string): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(`${this.baseUrl}/${id}/schedule`, { scheduledForUtc });
  }

  close(id: number): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(`${this.baseUrl}/${id}/close`, {});
  }

  archive(id: number): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(`${this.baseUrl}/${id}/archive`, {});
  }

  reopen(id: number): Observable<LearningMaterial> {
    return this.http.post<LearningMaterial>(`${this.baseUrl}/${id}/reopen`, {});
  }
}
