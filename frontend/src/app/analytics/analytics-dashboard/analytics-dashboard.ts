import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { AnalyticsDashboard as AnalyticsDashboardData, AnalyticsFilters } from '../analytics.model';
import { AnalyticsService } from '../analytics.service';

interface DashboardState {
  loading: boolean;
  error: string;
  data: AnalyticsDashboardData | null;
}

const today = new Date();
const ninetyDaysAgo = new Date(today);
ninetyDaysAgo.setDate(today.getDate() - 90);
const toDateValue = (date: Date) => date.toISOString().slice(0, 10);
const INITIAL_FILTERS: AnalyticsFilters = {
  assignmentId: null,
  dateFrom: toDateValue(ninetyDaysAgo),
  dateTo: toDateValue(today),
  attendanceThreshold: 75,
  marksThreshold: 40,
};

@Component({
  selector: 'app-analytics-dashboard',
  imports: [FormsModule, RouterLink],
  templateUrl: './analytics-dashboard.html',
  styleUrl: './analytics-dashboard.css',
})
export class AnalyticsDashboard {
  private readonly analyticsService = inject(AnalyticsService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  protected readonly authService = inject(AuthService);

  protected readonly assignments = toSignal(
    this.authService.role() === 'Admin' ? this.assignmentService.getAll() : this.assignmentService.getMine(),
    { initialValue: [] },
  );
  protected readonly assignmentId = signal<number | null>(null);
  protected readonly dateFrom = signal(INITIAL_FILTERS.dateFrom);
  protected readonly dateTo = signal(INITIAL_FILTERS.dateTo);
  protected readonly attendanceThreshold = signal(INITIAL_FILTERS.attendanceThreshold);
  protected readonly marksThreshold = signal(INITIAL_FILTERS.marksThreshold);
  protected readonly exportError = signal('');
  private readonly query$ = new BehaviorSubject<AnalyticsFilters>(INITIAL_FILTERS);

  protected readonly state = toSignal(
    this.query$.pipe(
      switchMap((filters) => this.analyticsService.getDashboard(filters).pipe(
        map((data): DashboardState => ({ loading: false, error: '', data })),
        catchError((err) => of<DashboardState>({ loading: false, error: err?.error?.message ?? 'Analytics could not be loaded.', data: null })),
        startWith<DashboardState>({ loading: true, error: '', data: null }),
      )),
    ),
    { initialValue: { loading: true, error: '', data: null } as DashboardState },
  );

  protected readonly filterInvalid = computed(() =>
    !this.dateFrom() || !this.dateTo() || this.dateFrom() > this.dateTo() ||
    this.attendanceThreshold() < 0 || this.attendanceThreshold() > 100 ||
    this.marksThreshold() < 0 || this.marksThreshold() > 100,
  );
  protected readonly gradeRows = computed(() => {
    const data = this.state().data;
    if (!data) return [];
    const grades = new Map<number, { gradeId: number; gradeName: string; attendancePercentage: number | null; totalRecords: number; marksPercentage: number | null; resultCategory: string }>();
    for (const row of data.attendanceByGrade) {
      grades.set(row.gradeId, { gradeId: row.gradeId, gradeName: row.gradeName, attendancePercentage: row.attendancePercentage, totalRecords: row.totalRecords, marksPercentage: null, resultCategory: 'No published results' });
    }
    for (const row of data.marksByGrade) {
      const existing = grades.get(row.gradeId);
      grades.set(row.gradeId, { gradeId: row.gradeId, gradeName: row.gradeName, attendancePercentage: existing?.attendancePercentage ?? null, totalRecords: existing?.totalRecords ?? 0, marksPercentage: row.averageMarksPercentage, resultCategory: row.resultCategory });
    }
    return [...grades.values()].sort((left, right) => left.gradeName.localeCompare(right.gradeName));
  });

  protected applyFilters(): void {
    if (this.filterInvalid()) return;
    this.query$.next(this.currentFilters());
  }

  protected setPeriod(days: number): void {
    const end = new Date();
    const start = new Date(end);
    start.setDate(end.getDate() - days);
    this.dateFrom.set(toDateValue(start));
    this.dateTo.set(toDateValue(end));
    this.applyFilters();
  }

  protected resetFilters(): void {
    this.assignmentId.set(null);
    this.dateFrom.set(INITIAL_FILTERS.dateFrom);
    this.dateTo.set(INITIAL_FILTERS.dateTo);
    this.attendanceThreshold.set(INITIAL_FILTERS.attendanceThreshold);
    this.marksThreshold.set(INITIAL_FILTERS.marksThreshold);
    this.applyFilters();
  }

  protected retry(): void {
    this.query$.next(this.query$.value);
  }

  protected exportCsv(): void {
    this.exportError.set('');
    const filters = this.query$.value;
    this.analyticsService.exportDashboard(filters).subscribe({
      next: (blob) => triggerBlobDownload(blob, `analytics-${filters.dateFrom}-${filters.dateTo}.csv`),
      error: () => this.exportError.set('Analytics export could not be downloaded.'),
    });
  }

  protected assignmentLabel(id: number): string {
    const assignment = this.assignments().find((item) => item.id === id);
    return assignment ? `${assignment.gradeName} ${assignment.sectionName} - ${assignment.subjectName}` : '';
  }

  private currentFilters(): AnalyticsFilters {
    return {
      assignmentId: this.assignmentId(),
      dateFrom: this.dateFrom(),
      dateTo: this.dateTo(),
      attendanceThreshold: this.attendanceThreshold(),
      marksThreshold: this.marksThreshold(),
    };
  }
}
