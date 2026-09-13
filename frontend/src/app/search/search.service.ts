import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { SearchResult } from './search.model';

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/search`;

  search(query: string): Observable<SearchResult[]> {
    return this.http.get<SearchResult[]>(this.baseUrl, { params: { q: query } });
  }
}
