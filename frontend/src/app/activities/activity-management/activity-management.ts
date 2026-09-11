import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { Activity } from '../activity.model';
import { ActivityService } from '../activity.service';

interface ActivityManagementState {
  loading: boolean;
  error: boolean;
  activities: Activity[];
}

const LOADING_STATE: ActivityManagementState = { loading: true, error: false, activities: [] };

@Component({
  selector: 'app-activity-management',
  imports: [RouterLink],
  templateUrl: './activity-management.html',
  styleUrl: './activity-management.css',
})
export class ActivityManagement {
  private readonly activityService = inject(ActivityService);

  protected readonly deleteError = signal('');

  protected readonly state = toSignal(
    this.activityService.getAll().pipe(
      map((activities): ActivityManagementState => ({ loading: false, error: false, activities })),
      catchError(() => of<ActivityManagementState>({ loading: false, error: true, activities: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected onDelete(id: number): void {
    if (!confirm('Are you sure you want to delete this activity?')) {
      return;
    }

    this.deleteError.set('');
    this.activityService.delete(id).subscribe({
      next: () => window.location.reload(),
      error: (err) =>
        this.deleteError.set(err?.error?.message ?? 'Unable to delete this activity. Please check whether the StudentConnect API is running.'),
    });
  }
}
