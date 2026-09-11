import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AttendanceService } from '../../attendance/attendance.service';
import { AssessmentInput } from '../marks.model';
import { MarksService } from '../marks.service';

@Component({
  selector: 'app-create-assessment',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './create-assessment.html',
  styleUrl: './create-assessment.css',
})
export class CreateAssessment {
  private readonly fb = inject(FormBuilder);
  private readonly marksService = inject(MarksService);
  private readonly attendanceService = inject(AttendanceService);
  private readonly router = inject(Router);

  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  protected readonly assessmentTypes = ['UnitTest', 'MidTerm', 'FinalExam', 'Quiz', 'Assignment', 'Practical'];
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    subjectId: [0, [Validators.required, Validators.min(1)]],
    date: [new Date().toISOString().slice(0, 10), Validators.required],
    maximumMarks: [100, [Validators.required, Validators.min(1)]],
    assessmentType: ['UnitTest', Validators.required],
    description: [''],
  });

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.marksService.createAssessment(this.form.getRawValue() as AssessmentInput).subscribe({
      next: () => this.router.navigate(['/assessments']),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.status === 400
            ? 'Some of the entered data was invalid. Please check the subject and maximum marks.'
            : 'Unable to save the assessment. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
