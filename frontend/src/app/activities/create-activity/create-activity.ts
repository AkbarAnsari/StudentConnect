import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { StudentService } from '../../students/student.service';
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

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    date: ['', Validators.required],
    location: ['', Validators.required],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    capacity: [30, [Validators.required, Validators.min(1)]],
  });

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const raw = this.form.getRawValue();
    this.activityService.create({ ...raw, date: new Date(raw.date).toISOString() }).subscribe({
      next: () => this.router.navigate(['/activity-management']),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.error?.message ?? 'Unable to save the activity. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
