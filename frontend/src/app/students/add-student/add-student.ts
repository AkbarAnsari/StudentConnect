import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { StudentService } from '../student.service';

@Component({
  selector: 'app-add-student',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './add-student.html',
  styleUrl: './add-student.css',
})
export class AddStudent {
  private readonly fb = inject(FormBuilder);
  private readonly studentService = inject(StudentService);
  private readonly router = inject(Router);

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    studentId: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    sectionId: [0, [Validators.required, Validators.min(1)]],
  });

  // Refetched every time the selected grade changes — a Section only makes sense
  // scoped to a Grade, so the dropdown options must follow the Grade selection.
  protected readonly sections = toSignal(
    this.form.controls.gradeId.valueChanges.pipe(
      switchMap((gradeId) => (gradeId > 0 ? this.studentService.getSectionsByGrade(gradeId) : of([]))),
    ),
    { initialValue: [] },
  );

  constructor() {
    // A section from the previous grade would no longer be valid once the grade changes.
    this.form.controls.gradeId.valueChanges.subscribe(() => this.form.controls.sectionId.setValue(0));
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.studentService.createStudent(this.form.getRawValue()).subscribe({
      next: (student) => this.router.navigate(['/students', student.id]),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.status === 409
            ? 'A student with this Student ID already exists.'
            : 'Unable to save the student. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
