import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { Activity, ActivityRegistration } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ActivityRegistrationService } from '../activity-registration.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { validateFile } from '../../shared/file-validation.util';

interface RegistrationsState {
  loading: boolean;
  error: boolean;
  activity: Activity | null;
  registrations: ActivityRegistration[];
}

const LOADING_STATE: RegistrationsState = { loading: true, error: false, activity: null, registrations: [] };

@Component({
  selector: 'app-activity-registrations-view',
  imports: [RouterLink, FormsModule],
  templateUrl: './activity-registrations-view.html',
  styleUrl: './activity-registrations-view.css',
})
export class ActivityRegistrationsView {
  private readonly route = inject(ActivatedRoute);
  private readonly activityService = inject(ActivityService);
  private readonly registrationService = inject(ActivityRegistrationService);

  protected readonly activityId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly actionError = signal('');
  protected readonly updatingId = signal<number | null>(null);
  protected readonly paymentReferences = signal<Record<number, string>>({});
  protected readonly messageTitle = signal('');
  protected readonly messageBody = signal('');
  protected readonly messagePriority = signal('Normal');
  protected readonly messageStatus = signal('Registered');
  protected readonly successMessage = signal('');
  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  protected readonly state = toSignal(
    this.refresh$.pipe(
      switchMap(() => combineLatest([
        this.activityService.getById(this.activityId),
        this.registrationService.getForActivity(this.activityId),
      ]).pipe(
        map(([activity, registrations]): RegistrationsState => ({ loading: false, error: false, activity, registrations })),
        catchError(() => of<RegistrationsState>({ loading: false, error: true, activity: null, registrations: [] })),
        startWith(LOADING_STATE),
      )),
    ),
    { initialValue: LOADING_STATE },
  );

  protected onCheckIn(registration: ActivityRegistration, checkedIn: boolean): void {
    this.actionError.set('');
    this.updatingId.set(registration.id);
    this.registrationService.setCheckIn(registration.id, checkedIn).subscribe({
      next: () => {
        this.updatingId.set(null);
        this.refresh$.next();
      },
      error: (err) => {
        this.updatingId.set(null);
        this.actionError.set(err?.error?.message ?? 'Unable to update participant check-in.');
      },
    });
  }

  protected setPaymentReference(id: number, value: string): void { this.paymentReferences.update(items => ({ ...items, [id]: value })); }

  protected markPaid(registration: ActivityRegistration): void {
    const reference = this.paymentReferences()[registration.id]?.trim();
    if (!reference) { this.actionError.set('Enter a payment reference.'); return; }
    this.updatingId.set(registration.id);
    this.registrationService.markPaid(registration.id, reference).subscribe({ next: () => { this.updatingId.set(null); this.successMessage.set('Payment recorded.'); this.refresh$.next(); }, error: err => { this.updatingId.set(null); this.actionError.set(err.error?.message ?? 'Unable to record payment.'); } });
  }

  protected uploadConsent(registration: ActivityRegistration, event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    const error = validateFile(file, ['.pdf', '.docx', '.png', '.jpg', '.jpeg'], 10 * 1024 * 1024);
    if (error || !file) { this.actionError.set(error ?? 'Select a consent document.'); return; }
    this.updatingId.set(registration.id);
    this.registrationService.uploadConsentDocument(registration.id, file).subscribe({ next: () => { this.updatingId.set(null); this.successMessage.set('Consent document saved.'); this.refresh$.next(); }, error: err => { this.updatingId.set(null); this.actionError.set(err.error?.message ?? 'Unable to upload consent.'); } });
  }

  protected downloadConsent(registration: ActivityRegistration): void {
    this.registrationService.downloadConsentDocument(registration.id).subscribe(blob => triggerBlobDownload(blob, registration.consentDocumentFileName ?? 'consent-document'));
  }

  protected notifyParticipants(): void {
    if (!this.messageTitle().trim() || !this.messageBody().trim()) { this.actionError.set('Message title and body are required.'); return; }
    this.registrationService.notifyParticipants(this.activityId, this.messageTitle(), this.messageBody(), this.messagePriority(), this.messageStatus()).subscribe({ next: result => { this.successMessage.set(`Message queued for ${result.count} participant(s).`); this.messageTitle.set(''); this.messageBody.set(''); }, error: err => this.actionError.set(err.error?.message ?? 'Unable to message participants.') });
  }

  protected onExport(activity: Activity): void {
    this.actionError.set('');
    this.registrationService.exportForActivity(this.activityId).subscribe({
      next: (file) => {
        const url = URL.createObjectURL(file);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `${activity.title.replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '')}-participants.csv`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.actionError.set('Unable to export the participant list.'),
    });
  }
}

