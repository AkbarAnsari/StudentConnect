import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { AnalyticsService } from '../analytics.service';

@Component({
  selector: 'app-analytics-dashboard',
  imports: [RouterLink],
  templateUrl: './analytics-dashboard.html',
  styleUrl: './analytics-dashboard.css',
})
export class AnalyticsDashboard {
  private readonly analyticsService = inject(AnalyticsService);

  protected readonly overview = toSignal(this.analyticsService.getOverview(), { initialValue: null });
  protected readonly attendanceByGrade = toSignal(this.analyticsService.getAttendanceByGrade(), { initialValue: [] });
  protected readonly marksByGrade = toSignal(this.analyticsService.getMarksByGrade(), { initialValue: [] });
  protected readonly atRiskStudents = toSignal(this.analyticsService.getAtRiskStudents(), { initialValue: [] });
  protected readonly trend = toSignal(this.analyticsService.getTrend(), { initialValue: [] });

  protected resultBadgeClass(category: string): string {
    switch (category) {
      case 'Excellent':
      case 'Very Good':
        return 'badge-success';
      case 'Good':
        return 'badge-info';
      case 'Needs Improvement':
        return 'badge-warning';
      default:
        return 'badge-danger';
    }
  }
}
