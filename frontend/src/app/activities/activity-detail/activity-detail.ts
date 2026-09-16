import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { Activity, ActivityRegistration } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ActivityRegistrationService } from '../activity-registration.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { AuthService } from '../../auth/auth.service';

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
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly authService = inject(AuthService);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly activityId = Number(this.route.snapshot.paramMap.get('activityId'));
  protected readonly canManageRegistration = computed(() =>
    this.authService.role() === 'Student' && this.authService.currentUser()?.studentId === this.studentId,
  );

  protected readonly state = toSignal(
    this.refresh$.pipe(
      switchMap(() =>
        this.activityService.getById(this.activityId).pipe(
          map((activity): ActivityDetailState => ({ loading: false, error: false, activity })),
          catchError(() => of<ActivityDetailState>({ loading: false, error: true, activity: null })),
          startWith(LOADING_STATE),
        ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly myRegistration = signal<ActivityRegistration | null>(null);
  protected readonly checkedRegistration = signal(false);
  protected readonly actionError = signal('');
  protected readonly consentConfirmed = signal(false);

  constructor() {
    this.registrationService.getForStudent(this.studentId).subscribe({
      next: (registrations) => {
        this.myRegistration.set(
          registrations.find((r) => r.activityId === this.activityId && (r.status === 'Registered' || r.status === 'Waitlisted')) ?? null,
        );
        this.checkedRegistration.set(true);
      },
      error: () => this.checkedRegistration.set(true),
    });
  }

  protected registrationUnavailableReason(activity: Activity): string | null {
    const now = Date.now();
    if (new Date(activity.date).getTime() <= now || (activity.registrationClosesAtUtc && new Date(activity.registrationClosesAtUtc).getTime() <= now)) return 'Registration is closed.';
    if (activity.registrationOpensAtUtc && new Date(activity.registrationOpensAtUtc).getTime() > now) return `Registration opens ${new Date(activity.registrationOpensAtUtc).toLocaleString()}.`;
    return null;
  }

  protected onRegister(): void {
    const activity = this.state().activity;
    if (activity?.consentRequired && !this.consentConfirmed()) {
      this.actionError.set('Confirm consent before registering for this activity.');
      return;
    }
    this.actionError.set('');
    this.registrationService.register({ activityId: this.activityId, consentConfirmed: this.consentConfirmed() }).subscribe({
      next: () => {
        this.loadRegistration();
        this.refresh$.next();
      },
      error: (err) =>
        this.actionError.set(err?.error?.message ?? 'Unable to register. Please check whether the StudentConnect API is running.'),
    });
  }

  protected async onCancel(): Promise<void> {
    const registration = this.myRegistration();
    if (!registration || !(await this.confirmDialog.confirm('Are you sure you want to cancel this registration?'))) {
      return;
    }

    this.actionError.set('');
    this.registrationService.cancel(registration.id).subscribe({
      next: () => {
        this.myRegistration.set(null);
        this.refresh$.next();
      },
      error: () => this.actionError.set('Unable to cancel registration. Please check whether the StudentConnect API is running.'),
    });
  }

  protected onConsentChange(confirmed: boolean): void {
    this.consentConfirmed.set(confirmed);
  }

  private loadRegistration(): void {
    this.registrationService.getForStudent(this.studentId).subscribe({
      next: (registrations) => {
        this.myRegistration.set(registrations.find((r) => r.activityId === this.activityId && (r.status === 'Registered' || r.status === 'Waitlisted')) ?? null);
        this.checkedRegistration.set(true);
      },
      error: () => this.checkedRegistration.set(true),
    });
  }
}
