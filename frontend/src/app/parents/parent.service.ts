import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Parent, ParentCreateInput } from './parent.model';

@Injectable({ providedIn: 'root' })
export class ParentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/parents`;

  getParents(): Observable<Parent[]> {
    return this.http.get<Parent[]>(this.baseUrl);
  }

  createParent(parent: ParentCreateInput): Observable<Parent> {
    return this.http.post<Parent>(this.baseUrl, parent);
  }

  linkChild(parentId: number, studentId: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${parentId}/children`, { studentId });
  }

  unlinkChild(parentId: number, studentId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${parentId}/children/${studentId}`);
  }
}
