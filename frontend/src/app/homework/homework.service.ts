import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Homework,
  HomeworkInput,
  HomeworkSubmission,
  HomeworkSubmissionInput,
  HomeworkUpdateInput,
  StudentHomework,
  SubmissionOverview,
} from './homework.model';

// HomeworkComponent -> HomeworkService -> HttpClient -> ASP.NET Core API -> EF Core -> SQL Server.
@Injectable({ providedIn: 'root' })
export class HomeworkService {
  private readonly http = inject(HttpClient);
  private readonly homeworkUrl = `${environment.apiBaseUrl}/homework`;

  getAllHomework(): Observable<Homework[]> {
    return this.http.get<Homework[]>(this.homeworkUrl);
  }

  getHomeworkById(id: number): Observable<Homework> {
    return this.http.get<Homework>(`${this.homeworkUrl}/${id}`);
  }

  getStudentHomework(studentId: number): Observable<StudentHomework[]> {
    return this.http.get<StudentHomework[]>(`${this.homeworkUrl}/student/${studentId}`);
  }

  getTeacherHomework(teacherId: number): Observable<Homework[]> {
    return this.http.get<Homework[]>(`${this.homeworkUrl}/teacher/${teacherId}`);
  }

  createHomework(homework: HomeworkInput): Observable<Homework> {
    return this.http.post<Homework>(this.homeworkUrl, homework);
  }

  updateHomework(id: number, homework: HomeworkUpdateInput): Observable<Homework> {
    return this.http.put<Homework>(`${this.homeworkUrl}/${id}`, homework);
  }

  deleteHomework(id: number): Observable<void> {
    return this.http.delete<void>(`${this.homeworkUrl}/${id}`);
  }
}

@Injectable({ providedIn: 'root' })
export class HomeworkSubmissionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/homework-submissions`;

  getSubmissionsForHomework(homeworkId: number): Observable<SubmissionOverview[]> {
    return this.http.get<SubmissionOverview[]>(`${this.baseUrl}/homework/${homeworkId}`);
  }

  getStudentSubmissions(studentId: number): Observable<HomeworkSubmission[]> {
    return this.http.get<HomeworkSubmission[]>(`${this.baseUrl}/student/${studentId}`);
  }

  createSubmission(submission: HomeworkSubmissionInput): Observable<HomeworkSubmission> {
    return this.http.post<HomeworkSubmission>(this.baseUrl, submission);
  }
}
