import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
    section: ['', Validators.required],
  });

  constructor() {
    effect(() => {
      const s = this.student();
      if (s) {
        this.form.setValue({
          firstName: s.firstName,
          lastName: s.lastName,
          email: s.email,
          gradeId: s.gradeId,
          section: s.section,
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
