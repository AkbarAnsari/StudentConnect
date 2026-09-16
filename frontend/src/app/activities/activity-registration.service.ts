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

  setCheckIn(id: number, checkedIn: boolean): Observable<ActivityRegistration> {
    return this.http.put<ActivityRegistration>(`${this.baseUrl}/${id}/check-in`, { checkedIn });
  }

  markPaid(id: number, paymentReference: string): Observable<ActivityRegistration> {
    return this.http.put<ActivityRegistration>(`${this.baseUrl}/${id}/payment`, { paymentReference });
  }

  uploadConsentDocument(id: number, file: File): Observable<ActivityRegistration> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.put<ActivityRegistration>(`${this.baseUrl}/${id}/consent-document`, formData);
  }

  downloadConsentDocument(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/consent-document`, { responseType: 'blob' });
  }

  notifyParticipants(activityId: number, title: string, message: string, priority: string, registrationStatus: string): Observable<{ count: number }> {
    return this.http.post<{ count: number }>(`${this.baseUrl}/activity/${activityId}/notify`, { title, message, priority, registrationStatus });
  }

  exportForActivity(activityId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/activity/${activityId}/export`, { responseType: 'blob' });
  }
}
