import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
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
  });

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
        });
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

    this.studentService.updateStudent(this.studentId, this.form.getRawValue()).subscribe({
      next: (student) => this.router.navigate(['/students', student.id]),
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
}
