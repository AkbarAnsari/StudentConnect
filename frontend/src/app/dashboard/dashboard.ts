import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of, startWith } from 'rxjs';
import { AnalyticsOverview } from '../analytics/analytics.model';
import { AnalyticsService } from '../analytics/analytics.service';
import { AttendanceService } from '../attendance/attendance.service';
import { AuthService } from '../auth/auth.service';
import { HomeworkService } from '../homework/homework.service';
import { MarksService } from '../marks/marks.service';
import { NotificationService } from '../notifications/notification.service';

interface OverviewState {
  loading: boolean;
  error: boolean;
  overview: AnalyticsOverview | null;
}

const OVERVIEW_LOADING: OverviewState = { loading: true, error: false, overview: null };

interface StudentSummaryState {
  loading: boolean;
  error: boolean;
  attendancePercentage: number | null;
  marksPercentage: number | null;
  pendingHomeworkCount: number;
}

const STUDENT_SUMMARY_LOADING: StudentSummaryState = {
  loading: true,
  error: false,
  attendancePercentage: null,
  marksPercentage: null,
  pendingHomeworkCount: 0,
};

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  protected readonly authService = inject(AuthService);
  private readonly analyticsService = inject(AnalyticsService);
  private readonly attendanceService = inject(AttendanceService);
  private readonly marksService = inject(MarksService);
  private readonly homeworkService = inject(HomeworkService);
  protected readonly notificationService = inject(NotificationService);

  // Surfaces recent announcements/notifications directly on the dashboard instead of only
  // in the header bell — the bell's own polling (started in app.html) already keeps this fed.
  protected readonly recentNotifications = computed(() => this.notificationService.notifications().slice(0, 5));

  protected readonly resendSent = signal(false);
  protected readonly resendError = signal('');

  // Admin/Teacher: reuse the already-working Analytics endpoints for a real summary
  // instead of the previous plain link list.
  protected readonly overviewState = toSignal(
    this.analyticsService.getOverview().pipe(
      map((overview): OverviewState => ({ loading: false, error: false, overview })),
      catchError(() => of<OverviewState>({ loading: false, error: true, overview: null })),
      startWith(OVERVIEW_LOADING),
    ),
    { initialValue: OVERVIEW_LOADING },
  );

  // Student: reuse the same per-student summary endpoints already used on Student Detail.
  private readonly studentId = computed(() => this.authService.currentUser()?.studentId ?? null);

  protected readonly studentSummaryState = toSignal(
    (this.studentId() === null
      ? of<StudentSummaryState>({ ...STUDENT_SUMMARY_LOADING, loading: false })
      : forkJoin({
          attendance: this.attendanceService.getStudentAttendanceSummary(this.studentId()!),
          marks: this.marksService.getStudentMarksSummary(this.studentId()!),
          homework: this.homeworkService.getStudentHomework(this.studentId()!),
        }).pipe(
          map((result): StudentSummaryState => {
            const totalClasses = result.attendance.reduce((sum, s) => sum + s.totalClasses, 0);
            const totalPresent = result.attendance.reduce((sum, s) => sum + s.present, 0);
            const totalMax = result.marks.reduce((sum, s) => sum + s.totalMaximumMarks, 0);
            const totalObtained = result.marks.reduce((sum, s) => sum + s.totalMarksObtained, 0);
            return {
              loading: false,
              error: false,
              attendancePercentage: totalClasses === 0 ? null : Math.round((totalPresent * 100) / totalClasses),
              marksPercentage: totalMax === 0 ? null : Math.round((totalObtained * 100) / totalMax),
              pendingHomeworkCount: result.homework.filter((h) => h.status === 'Not Submitted').length,
            };
          }),
          catchError(() => of<StudentSummaryState>({ ...STUDENT_SUMMARY_LOADING, loading: false, error: true })),
          startWith(STUDENT_SUMMARY_LOADING),
        )
    ),
    { initialValue: STUDENT_SUMMARY_LOADING },
  );

  protected resendVerification(): void {
    this.resendError.set('');
    this.authService.resendVerification().subscribe({
      next: () => this.resendSent.set(true),
      error: (err: HttpErrorResponse) =>
        this.resendError.set(err.error?.message ?? 'Unable to resend the verification email. Please try again later.'),
    });
  }
}
