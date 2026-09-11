import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ActivityRegistration, ActivityRegistrationInput } from './activity.model';

@Injectable({ providedIn: 'root' })
export class ActivityRegistrationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/activity-registrations`;

  getForStudent(studentId: number): Observable<ActivityRegistration[]> {
    return this.http.get<ActivityRegistration[]>(`${this.baseUrl}/student/${studentId}`);
  }

  getForActivity(activityId: number): Observable<ActivityRegistration[]> {
    return this.http.get<ActivityRegistration[]>(`${this.baseUrl}/activity/${activityId}`);
  }

  register(registration: ActivityRegistrationInput): Observable<ActivityRegistration> {
    return this.http.post<ActivityRegistration>(this.baseUrl, registration);
  }

  cancel(id: number): Observable<ActivityRegistration> {
    return this.http.put<ActivityRegistration>(`${this.baseUrl}/${id}/cancel`, {});
  }
}
