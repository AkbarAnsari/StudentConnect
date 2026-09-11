import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { Activity, ActivityRegistration } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ActivityRegistrationService } from '../activity-registration.service';

interface ActivityDetailState {
  loading: boolean;
  error: boolean;
  activity: Activity | null;
}

const LOADING_STATE: ActivityDetailState = { loading: true, error: false, activity: null };

@Component({
  selector: 'app-activity-detail',
  imports: [RouterLink],
  templateUrl: './activity-detail.html',
  styleUrl: './activity-detail.css',
})
export class ActivityDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly activityService = inject(ActivityService);
  private readonly registrationService = inject(ActivityRegistrationService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly activityId = Number(this.route.snapshot.paramMap.get('activityId'));

  protected readonly state = toSignal(
    this.activityService.getById(this.activityId).pipe(
      map((activity): ActivityDetailState => ({ loading: false, error: false, activity })),
      catchError(() => of<ActivityDetailState>({ loading: false, error: true, activity: null })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly myRegistration = signal<ActivityRegistration | null>(null);
  protected readonly checkedRegistration = signal(false);
  protected readonly actionError = signal('');

  constructor() {
    this.registrationService.getForStudent(this.studentId).subscribe({
      next: (registrations) => {
        this.myRegistration.set(registrations.find((r) => r.activityId === this.activityId && r.status === 'Registered') ?? null);
        this.checkedRegistration.set(true);
      },
      error: () => this.checkedRegistration.set(true),
    });
  }

  protected isPast(activity: Activity): boolean {
    return new Date(activity.date).getTime() < Date.now();
  }

  protected onRegister(): void {
    this.actionError.set('');
    this.registrationService.register({ activityId: this.activityId }).subscribe({
      next: () => window.location.reload(),
      error: (err) =>
        this.actionError.set(err?.error?.message ?? 'Unable to register. Please check whether the StudentConnect API is running.'),
    });
  }

  protected onCancel(): void {
    const registration = this.myRegistration();
    if (!registration || !confirm('Are you sure you want to cancel this registration?')) {
      return;
    }

    this.actionError.set('');
    this.registrationService.cancel(registration.id).subscribe({
      next: () => window.location.reload(),
      error: () => this.actionError.set('Unable to cancel registration. Please check whether the StudentConnect API is running.'),
    });
  }
}
