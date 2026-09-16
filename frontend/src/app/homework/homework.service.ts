import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  Homework,
  HomeworkBulkDueDateInput,
  HomeworkInput,
  HomeworkSubmission,
  HomeworkUpdateInput,
  StudentHomework,
  SubmissionOverview,
} from './homework.model';

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

  bulkUpdateDueDate(input: HomeworkBulkDueDateInput): Observable<void> {
    return this.http.put<void>(`${this.homeworkUrl}/bulk-due-date`, input);
  }

  updateHomework(id: number, homework: HomeworkUpdateInput): Observable<Homework> {
    return this.http.put<Homework>(`${this.homeworkUrl}/${id}`, homework);
  }

  deleteHomework(id: number): Observable<void> {
    return this.http.delete<void>(`${this.homeworkUrl}/${id}`);
  }

  publishHomework(id: number): Observable<Homework> {
    return this.http.post<Homework>(`${this.homeworkUrl}/${id}/publish`, {});
  }

  scheduleHomework(id: number, scheduledForUtc: string): Observable<Homework> {
    return this.http.post<Homework>(`${this.homeworkUrl}/${id}/schedule`, { scheduledForUtc });
  }

  closeHomework(id: number): Observable<Homework> {
    return this.http.post<Homework>(`${this.homeworkUrl}/${id}/close`, {});
  }

  archiveHomework(id: number): Observable<Homework> {
    return this.http.post<Homework>(`${this.homeworkUrl}/${id}/archive`, {});
  }

  reopenHomework(id: number): Observable<Homework> {
    return this.http.post<Homework>(`${this.homeworkUrl}/${id}/reopen`, {});
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

  // Multipart/form-data — the attached file is optional alongside the comment.
  createSubmission(homeworkId: number, comments: string, file: File | null): Observable<HomeworkSubmission> {
    const formData = new FormData();
    formData.append('HomeworkId', String(homeworkId));
    formData.append('Comments', comments);
    if (file) {
      formData.append('File', file);
    }
    return this.http.post<HomeworkSubmission>(this.baseUrl, formData);
  }

  updateSubmission(id: number, comments: string, file: File | null): Observable<HomeworkSubmission> {
    const formData = new FormData();
    formData.append('Comments', comments);
    if (file) {
      formData.append('File', file);
    }
    return this.http.put<HomeworkSubmission>(`${this.baseUrl}/${id}`, formData);
  }

  addFeedback(id: number, teacherFeedback: string): Observable<HomeworkSubmission> {
    return this.http.put<HomeworkSubmission>(`${this.baseUrl}/${id}/feedback`, { teacherFeedback });
  }

  // Fetched via HttpClient (blob) so the auth interceptor attaches the Bearer token — a plain
  // anchor href would bypass it entirely and 401.
  downloadFile(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${id}/download`, { responseType: 'blob' });
  }

  downloadHomeworkSubmissions(homeworkId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/homework/${homeworkId}/download`, { responseType: 'blob' });
  }
}
