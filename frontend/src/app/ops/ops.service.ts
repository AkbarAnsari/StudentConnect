import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { OpsInfo } from './ops.model';

@Injectable({ providedIn: 'root' })
export class OpsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/ops`;

  getInfo(): Observable<OpsInfo> {
    return this.http.get<OpsInfo>(`${this.baseUrl}/info`);
  }
}
