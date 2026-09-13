import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Section } from '../students/student.model';
import { AcademicYear, AcademicYearInput, SectionInput, Term, TermInput } from './academic-structure.model';

@Injectable({ providedIn: 'root' })
export class AcademicStructureService {
  private readonly http = inject(HttpClient);
  private readonly academicYearsUrl = `${environment.apiBaseUrl}/academic-years`;
  private readonly termsUrl = `${environment.apiBaseUrl}/terms`;
  private readonly sectionsUrl = `${environment.apiBaseUrl}/sections`;

  getAcademicYears(): Observable<AcademicYear[]> {
    return this.http.get<AcademicYear[]>(this.academicYearsUrl);
  }

  createAcademicYear(year: AcademicYearInput): Observable<AcademicYear> {
    return this.http.post<AcademicYear>(this.academicYearsUrl, year);
  }

  deleteAcademicYear(id: number): Observable<void> {
    return this.http.delete<void>(`${this.academicYearsUrl}/${id}`);
  }

  getTerms(): Observable<Term[]> {
    return this.http.get<Term[]>(this.termsUrl);
  }

  createTerm(term: TermInput): Observable<Term> {
    return this.http.post<Term>(this.termsUrl, term);
  }

  deleteTerm(id: number): Observable<void> {
    return this.http.delete<void>(`${this.termsUrl}/${id}`);
  }

  getSections(): Observable<Section[]> {
    return this.http.get<Section[]>(this.sectionsUrl);
  }

  createSection(section: SectionInput): Observable<Section> {
    return this.http.post<Section>(this.sectionsUrl, section);
  }

  deleteSection(id: number): Observable<void> {
    return this.http.delete<void>(`${this.sectionsUrl}/${id}`);
  }
}
