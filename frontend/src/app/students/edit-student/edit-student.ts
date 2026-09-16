import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { of, switchMap, tap } from 'rxjs';
import { StudentService } from '../student.service';

@Component({
  selector: 'app-edit-student',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './edit-student.html',
  styleUrl: './edit-student.css',
})
export class EditStudent {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly studentService = inject(StudentService);

  private readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  // The section to apply once the sections-for-this-grade list has loaded — set from the
  // student's existing value on first load, then cleared (further grade changes reset to
  // "no section selected" instead, since a different grade's sections don't apply).
  private pendingSectionId: number | null = null;

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly student = toSignal(this.studentService.getStudentById(this.studentId), { initialValue: null });
  protected readonly loadError = signal(false);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    sectionId: [0, [Validators.required, Validators.min(1)]],
    dateOfBirth: [''],
    phone: [''],
    address: [''],
  });

  protected readonly statusForm = this.fb.nonNullable.group({
    status: ['Active', Validators.required],
    reason: [''],
  });
  protected readonly statusSubmitting = signal(false);
  protected readonly statusMessage = signal('');
  protected readonly statusError = signal('');
  private saved = false;

  protected readonly sections = toSignal(
    this.form.controls.gradeId.valueChanges.pipe(
      switchMap((gradeId) => (gradeId > 0 ? this.studentService.getSectionsByGrade(gradeId) : of([]))),
      tap(() => {
        this.form.controls.sectionId.setValue(this.pendingSectionId ?? 0);
        this.pendingSectionId = null;
      }),
    ),
    { initialValue: [] },
  );

  constructor() {
    effect(() => {
      const s = this.student();
      if (s) {
        this.pendingSectionId = s.sectionId;
        this.form.patchValue({
          firstName: s.firstName,
          lastName: s.lastName,
          email: s.email,
          gradeId: s.gradeId,
          dateOfBirth: s.dateOfBirth ?? '',
          phone: s.phone ?? '',
          address: s.address ?? '',
        });
        this.statusForm.patchValue({ status: s.status });
      }
    });
  }

  protected onStatusSubmit(): void {
    if (this.statusForm.invalid) {
      this.statusForm.markAllAsTouched();
      return;
    }

    const { status, reason } = this.statusForm.getRawValue();

    this.statusSubmitting.set(true);
    this.statusMessage.set('');
    this.statusError.set('');

    this.studentService.updateStatus(this.studentId, { status: status as never, reason: reason || undefined }).subscribe({
      next: () => {
        this.statusSubmitting.set(false);
        this.statusForm.markAsPristine();
        this.statusMessage.set('Status updated.');
      },
      error: (err: HttpErrorResponse) => {
        this.statusSubmitting.set(false);
        this.statusError.set(err.error?.message ?? 'Unable to update status.');
      },
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const { dateOfBirth, phone, address, ...rest } = this.form.getRawValue();
    this.studentService
      .updateStudent(this.studentId, { ...rest, dateOfBirth: dateOfBirth || null, phone: phone || null, address: address || null })
      .subscribe({
        next: (student) => {
          this.saved = true;
          this.router.navigate(['/students', student.id]);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(
            err.status === 404
              ? 'Student not found.'
              : 'Unable to update the student. Please check whether the StudentConnect API is running.',
          );
        },
      });
  }

  hasUnsavedChanges(): boolean {
    return (this.form.dirty || this.statusForm.dirty) && !this.saved && !this.submitting() && !this.statusSubmitting();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
