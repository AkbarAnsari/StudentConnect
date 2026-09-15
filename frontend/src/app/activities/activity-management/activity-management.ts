import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { Activity } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';

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
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly deleteError = signal('');

  // A manual trigger the component re-subscribes on after a successful delete, instead of
  // reloading the whole browser page to refresh the list.
  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  protected readonly state = toSignal(
    this.refresh$.pipe(
      switchMap(() =>
        this.activityService.getAll().pipe(
          map((activities): ActivityManagementState => ({ loading: false, error: false, activities })),
          catchError(() => of<ActivityManagementState>({ loading: false, error: true, activities: [] })),
          startWith(LOADING_STATE),
        ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected async onDelete(id: number): Promise<void> {
    if (!(await this.confirmDialog.confirm('Are you sure you want to delete this activity?'))) {
      return;
    }

    this.deleteError.set('');
    this.activityService.delete(id).subscribe({
      next: () => this.refresh$.next(),
      error: (err) =>
        this.deleteError.set(err?.error?.message ?? 'Unable to delete this activity. Please check whether the StudentConnect API is running.'),
    });
  }
}
