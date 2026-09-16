import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { OperationalSettings, OpsInfo } from './ops.model';

@Injectable({ providedIn: 'root' })
export class OpsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/ops`;

  getInfo(): Observable<OpsInfo> {
    return this.http.get<OpsInfo>(`${this.baseUrl}/info`);
  }
  getSettings():Observable<OperationalSettings>{return this.http.get<OperationalSettings>(`${this.baseUrl}/settings`);}
  saveSettings(settings:OperationalSettings):Observable<void>{return this.http.put<void>(`${this.baseUrl}/settings`,settings);}
  applyRetention():Observable<{auditRows:number;notificationRows:number}>{return this.http.post<{auditRows:number;notificationRows:number}>(`${this.baseUrl}/apply-retention`,{});}
  testAlert():Observable<{message:string}>{return this.http.post<{message:string}>(`${this.baseUrl}/test-alert`,{});}
}
