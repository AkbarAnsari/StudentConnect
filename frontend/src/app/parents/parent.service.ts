import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Parent, ParentCreateInput, ParentStudentHistory } from './parent.model';

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

  linkChild(parentId: number, studentId: number, relationshipType: string, isPrimaryContact: boolean): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${parentId}/children`, { studentId, relationshipType, isPrimaryContact, reason: 'Linked by administrator' });
  }

  unlinkChild(parentId: number, studentId: number): Observable<void> {
    const params = new HttpParams().set('reason', 'Removed by administrator');
    return this.http.delete<void>(`${this.baseUrl}/${parentId}/children/${studentId}`, { params });
  }

  getHistory(parentId: number): Observable<ParentStudentHistory[]> {
    return this.http.get<ParentStudentHistory[]>(`${this.baseUrl}/${parentId}/history`);
  }
}
