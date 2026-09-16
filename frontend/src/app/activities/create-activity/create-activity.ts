import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of, startWith, switchMap } from 'rxjs';
import { StudentService } from '../../students/student.service';
import { ActivityInput } from '../activity.model';
import { ActivityService } from '../activity.service';

@Component({
  selector: 'app-create-activity',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './create-activity.html',
  styleUrl: './create-activity.css',
})
export class CreateActivity {
  private readonly fb = inject(FormBuilder);
  private readonly activityService = inject(ActivityService);
  private readonly studentService = inject(StudentService);
  private readonly router = inject(Router);

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    date: ['', Validators.required],
    location: ['', Validators.required],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    sectionId: [0],
    capacity: [30, [Validators.required, Validators.min(1)]],
    organizerName: [''],
    contactEmail: ['', Validators.email],
    registrationOpensAtUtc: [''],
    registrationClosesAtUtc: [''],
    consentRequired: [false],
    feeAmount: [0, Validators.min(0)],
    status: ['Draft' as 'Draft' | 'Open'],
  });

  protected readonly sections = toSignal(
    this.form.controls.gradeId.valueChanges.pipe(
      startWith(this.form.controls.gradeId.value),
      switchMap((gradeId) => (gradeId > 0 ? this.studentService.getSectionsByGrade(gradeId) : of([]))),
    ),
    { initialValue: [] },
  );

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const raw = this.form.getRawValue();
    const input: ActivityInput = {
      ...raw,
      date: new Date(raw.date).toISOString(),
      sectionId: raw.sectionId || null,
      contactEmail: raw.contactEmail.trim() || null,
      registrationOpensAtUtc: raw.registrationOpensAtUtc ? new Date(raw.registrationOpensAtUtc).toISOString() : null,
      registrationClosesAtUtc: raw.registrationClosesAtUtc ? new Date(raw.registrationClosesAtUtc).toISOString() : null,
      feeAmount: raw.feeAmount > 0 ? raw.feeAmount : null,
    };
    this.activityService.create(input).subscribe({
      next: () => {
        this.saved = true;
        this.router.navigate(['/activity-management']);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.error?.message ?? 'Unable to save the activity. Please check whether the StudentConnect API is running.',
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
}
