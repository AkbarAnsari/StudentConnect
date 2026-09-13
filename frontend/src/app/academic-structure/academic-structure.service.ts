import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Grade, Section } from '../students/student.model';
import { Subject } from '../attendance/attendance.model';
import { AcademicYear, AcademicYearInput, GradeInput, SectionInput, SectionStudent, SubjectInput, Term, TermInput } from './academic-structure.model';

@Injectable({ providedIn: 'root' })
export class AcademicStructureService {
  private readonly http = inject(HttpClient);
  private readonly academicYearsUrl = `${environment.apiBaseUrl}/academic-years`;
  private readonly termsUrl = `${environment.apiBaseUrl}/terms`;
  private readonly sectionsUrl = `${environment.apiBaseUrl}/sections`;
  private readonly gradesUrl = `${environment.apiBaseUrl}/grades`;
  private readonly subjectsUrl = `${environment.apiBaseUrl}/subjects`;

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

  // A Teacher's homeroom sections, for the "My Classes" roster view.
  getMySections(): Observable<Section[]> {
    return this.http.get<Section[]>(`${this.sectionsUrl}/my`);
  }

  getStudentsInSection(sectionId: number): Observable<SectionStudent[]> {
    return this.http.get<SectionStudent[]>(`${this.sectionsUrl}/${sectionId}/students`);
  }

  createSection(section: SectionInput): Observable<Section> {
    return this.http.post<Section>(this.sectionsUrl, section);
  }

  deleteSection(id: number): Observable<void> {
    return this.http.delete<void>(`${this.sectionsUrl}/${id}`);
  }

  getGrades(): Observable<Grade[]> {
    return this.http.get<Grade[]>(this.gradesUrl);
  }

  createGrade(grade: GradeInput): Observable<Grade> {
    return this.http.post<Grade>(this.gradesUrl, grade);
  }

  deleteGrade(id: number): Observable<void> {
    return this.http.delete<void>(`${this.gradesUrl}/${id}`);
  }

  getSubjects(): Observable<Subject[]> {
    return this.http.get<Subject[]>(this.subjectsUrl);
  }

  createSubject(subject: SubjectInput): Observable<Subject> {
    return this.http.post<Subject>(this.subjectsUrl, subject);
  }

  deleteSubject(id: number): Observable<void> {
    return this.http.delete<void>(`${this.subjectsUrl}/${id}`);
  }
}
