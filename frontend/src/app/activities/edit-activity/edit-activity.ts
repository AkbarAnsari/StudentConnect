import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { StudentService } from '../../students/student.service';
import { Section } from '../../students/student.model';
import { ActivityUpdateInput } from '../activity.model';
import { ActivityService } from '../activity.service';

@Component({
  selector: 'app-edit-activity',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './edit-activity.html',
  styleUrl: './edit-activity.css',
})
export class EditActivity {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly activityService = inject(ActivityService);
  private readonly studentService = inject(StudentService);

  private readonly activityId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly activity = toSignal(this.activityService.getById(this.activityId), { initialValue: null });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly sections = signal<Section[]>([]);
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    date: ['', Validators.required],
    location: ['', Validators.required],
    capacity: [1, [Validators.required, Validators.min(1)]],
    sectionId: [0],
    organizerName: [''],
    contactEmail: ['', Validators.email],
    registrationOpensAtUtc: [''],
    registrationClosesAtUtc: [''],
    consentRequired: [false],
    feeAmount: [0, Validators.min(0)],
  });

  constructor() {
    effect(() => {
      const a = this.activity();
      if (a) {
        this.form.setValue({
          title: a.title,
          description: a.description,
          date: this.toLocalValue(a.date),
          location: a.location,
          capacity: a.capacity,
          sectionId: a.sectionId ?? 0,
          organizerName: a.organizerName,
          contactEmail: a.contactEmail ?? '',
          registrationOpensAtUtc: this.toLocalValue(a.registrationOpensAtUtc),
          registrationClosesAtUtc: this.toLocalValue(a.registrationClosesAtUtc),
          consentRequired: a.consentRequired,
          feeAmount: a.feeAmount ?? 0,
        });
        this.studentService.getSectionsByGrade(a.gradeId).subscribe((sections) => this.sections.set(sections));
      }
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const raw = this.form.getRawValue();
    const input: ActivityUpdateInput = {
      ...raw,
      date: new Date(raw.date).toISOString(),
      sectionId: raw.sectionId || null,
      contactEmail: raw.contactEmail.trim() || null,
      registrationOpensAtUtc: raw.registrationOpensAtUtc ? new Date(raw.registrationOpensAtUtc).toISOString() : null,
      registrationClosesAtUtc: raw.registrationClosesAtUtc ? new Date(raw.registrationClosesAtUtc).toISOString() : null,
      feeAmount: raw.feeAmount > 0 ? raw.feeAmount : null,
    };
    this.activityService.update(this.activityId, input).subscribe({
      next: () => {
        this.saved = true;
        this.router.navigate(['/activity-management']);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.error?.message ?? 'Unable to update the activity. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved && !this.submitting();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }

  private toLocalValue(value: string | null): string {
    if (!value) return '';
    const date = new Date(value);
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
    return local.toISOString().slice(0, 16);
  }
}
