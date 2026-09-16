import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { StudentService } from '../../students/student.service';
import { Activity, ActivityRegistration } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ActivityRegistrationService } from '../activity-registration.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { AuthService } from '../../auth/auth.service';
import { StudentRecordNav } from '../../students/student-record-nav/student-record-nav';

interface StudentActivitiesState {
  loading: boolean;
  error: boolean;
  activities: Activity[];
  registrations: ActivityRegistration[];
}

const LOADING_STATE: StudentActivitiesState = { loading: true, error: false, activities: [], registrations: [] };

@Component({
  selector: 'app-student-activities',
  imports: [RouterLink, StudentRecordNav],
  templateUrl: './student-activities.html',
  styleUrl: './student-activities.css',
})
export class StudentActivities {
  private readonly route = inject(ActivatedRoute);
  private readonly studentService = inject(StudentService);
  private readonly activityService = inject(ActivityService);
  private readonly registrationService = inject(ActivityRegistrationService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly authService = inject(AuthService);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly canManageRegistration = computed(() =>
    this.authService.role() === 'Student' && this.authService.currentUser()?.studentId === this.studentId,
  );

  protected readonly state = toSignal(
    this.refresh$.pipe(
      switchMap(() =>
        this.studentService.getStudentById(this.studentId).pipe(
          switchMap((student) =>
            combineLatest([
              this.activityService.getByGrade(student.gradeId),
              this.registrationService.getForStudent(this.studentId),
            ]).pipe(
              map(([activities, registrations]) => [
                activities.filter((activity) => activity.sectionId === null || activity.sectionId === student.sectionId),
                registrations,
              ] as const),
            ),
          ),
          map(([activities, registrations]): StudentActivitiesState => ({ loading: false, error: false, activities, registrations })),
          catchError(() => of<StudentActivitiesState>({ loading: false, error: true, activities: [], registrations: [] })),
          startWith(LOADING_STATE),
        ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly myRegistrations = computed(() =>
    this.state().registrations.filter((r) => r.status === 'Registered' || r.status === 'Waitlisted'),
  );

  protected registrationFor(activityId: number): ActivityRegistration | null {
    return (
      this.state().registrations.find(
        (r) => r.activityId === activityId && (r.status === 'Registered' || r.status === 'Waitlisted'),
      ) ?? null
    );
  }

  protected registrationUnavailableReason(activity: Activity): string | null {
    const now = Date.now();
    if (new Date(activity.date).getTime() <= now || (activity.registrationClosesAtUtc && new Date(activity.registrationClosesAtUtc).getTime() <= now)) {
      return 'Registration closed';
    }
    if (activity.registrationOpensAtUtc && new Date(activity.registrationOpensAtUtc).getTime() > now) {
      return `Opens ${new Date(activity.registrationOpensAtUtc).toLocaleString()}`;
    }
    return null;
  }

  protected readonly actionError = signal('');
  protected readonly consentActivityIds = signal<ReadonlySet<number>>(new Set());

  protected hasConsent(activityId: number): boolean {
    return this.consentActivityIds().has(activityId);
  }

  protected onConsentChange(activityId: number, confirmed: boolean): void {
    const updated = new Set(this.consentActivityIds());
    confirmed ? updated.add(activityId) : updated.delete(activityId);
    this.consentActivityIds.set(updated);
  }

  protected onRegister(activity: Activity): void {
    const consentConfirmed = this.hasConsent(activity.id);
    if (activity.consentRequired && !consentConfirmed) {
      this.actionError.set('Confirm consent before registering for this activity.');
      return;
    }
    this.actionError.set('');
    this.registrationService.register({ activityId: activity.id, consentConfirmed }).subscribe({
      next: () => this.refresh$.next(),
      error: (err) => this.actionError.set(err?.error?.message ?? 'Unable to register. Please try again.'),
    });
  }

  protected async onCancel(registrationId: number): Promise<void> {
    if (!(await this.confirmDialog.confirm('Are you sure you want to cancel this registration?'))) {
      return;
    }

    this.actionError.set('');
    this.registrationService.cancel(registrationId).subscribe({
      next: () => this.refresh$.next(),
      error: () => this.actionError.set('Unable to cancel registration. Please check whether the StudentConnect API is running.'),
    });
  }
}
