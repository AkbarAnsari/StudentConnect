import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { Activity, ActivityStatus } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { AuthService } from '../../auth/auth.service';

interface ActivityManagementState {
  loading: boolean;
  error: boolean;
  activities: Activity[];
}

const LOADING_STATE: ActivityManagementState = { loading: true, error: false, activities: [] };

@Component({
  selector: 'app-activity-management',
  imports: [RouterLink, FormsModule],
  templateUrl: './activity-management.html',
  styleUrl: './activity-management.css',
})
export class ActivityManagement {
  private readonly activityService = inject(ActivityService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly auth = inject(AuthService);

  protected readonly actionError = signal('');
  protected readonly updatingId = signal<number | null>(null);
  protected readonly viewMode = signal<'list' | 'calendar'>('list');
  protected readonly statusFilter = signal('All');
  protected readonly gradeFilter = signal(0);
  protected readonly monthFilter = signal(new Date().toISOString().slice(0, 7));
  protected readonly moderationReason = signal<Record<number, string>>({});

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
  protected readonly grades = computed(() => [...new Map(this.state().activities.map(item => [item.gradeId, item.gradeName])).entries()]);
  protected readonly filteredActivities = computed(() => this.state().activities.filter(item =>
    (this.statusFilter() === 'All' || item.status === this.statusFilter()) &&
    (!this.gradeFilter() || item.gradeId === this.gradeFilter()) &&
    (this.viewMode() !== 'calendar' || item.date.startsWith(this.monthFilter())),
  ));
  protected readonly calendarGroups = computed(() => Object.entries(this.filteredActivities().reduce<Record<string, Activity[]>>((groups, item) => {
    const date = item.date.slice(0, 10);
    (groups[date] ??= []).push(item);
    return groups;
  }, {})));

  protected async onDelete(id: number): Promise<void> {
    if (!(await this.confirmDialog.confirm('Are you sure you want to delete this activity?'))) {
      return;
    }

    this.actionError.set('');
    this.activityService.delete(id).subscribe({
      next: () => this.refresh$.next(),
      error: (err) =>
        this.actionError.set(err?.error?.message ?? 'Unable to delete this activity. Please check whether the StudentConnect API is running.'),
    });
  }

  protected nextStatuses(activity: Activity): ActivityStatus[] {
    switch (activity.status) {
      case 'Draft': return ['Open', 'Cancelled'];
      case 'Open': return new Date(activity.date) <= new Date() ? ['Closed', 'Completed', 'Cancelled'] : ['Closed', 'Cancelled'];
      case 'Closed': return new Date(activity.date) <= new Date() ? ['Open', 'Completed', 'Cancelled'] : ['Open', 'Cancelled'];
      case 'Cancelled':
      case 'Completed': return ['Archived'];
      default: return [];
    }
  }

  protected actionLabel(status: ActivityStatus): string {
    return ({ Open: 'Open registration', Closed: 'Close registration', Cancelled: 'Cancel', Completed: 'Mark complete', Archived: 'Archive' } as Partial<Record<ActivityStatus, string>>)[status] ?? status;
  }

  protected async onStatus(activity: Activity, status: ActivityStatus): Promise<void> {
    if (status === 'Cancelled' && !(await this.confirmDialog.confirm(`Cancel ${activity.title}? Registered learners will be notified.`))) return;

    this.actionError.set('');
    this.updatingId.set(activity.id);
    this.activityService.updateStatus(activity.id, status).subscribe({
      next: () => {
        this.updatingId.set(null);
        this.refresh$.next();
      },
      error: (err) => {
        this.updatingId.set(null);
        this.actionError.set(err?.error?.message ?? 'Unable to update the activity status.');
      },
    });
  }

  protected setModerationReason(id: number, value: string): void { this.moderationReason.update(items => ({ ...items, [id]: value })); }
  protected moderate(activity: Activity, action: 'Submit' | 'Approve' | 'Reject'): void {
    this.updatingId.set(activity.id); this.actionError.set('');
    this.activityService.moderate(activity.id, action, this.moderationReason()[activity.id]).subscribe({
      next: () => { this.updatingId.set(null); this.refresh$.next(); },
      error: err => { this.updatingId.set(null); this.actionError.set(err.error?.message ?? 'Unable to update moderation.'); },
    });
  }
}
