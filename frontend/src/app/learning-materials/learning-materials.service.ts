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

  // A plain anchor navigation to this URL triggers a normal browser download —
  // no need to fetch the file into Angular just to hand it back to the browser.
  downloadUrl(id: number): string {
    return `${this.baseUrl}/${id}/download`;
  }
}
