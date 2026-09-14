import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AttendanceService } from '../../attendance/attendance.service';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
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
  private readonly academicStructure = inject(AcademicStructureService);
  private readonly router = inject(Router);

  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  protected readonly terms = toSignal(this.academicStructure.getTerms(), { initialValue: [] });
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
    termId: [0],
  });

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const { termId, ...rest } = this.form.getRawValue();
    this.marksService.createAssessment({ ...rest, termId: termId > 0 ? termId : null } as AssessmentInput).subscribe({
      next: () => this.router.navigate(['/assessments']),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.status === 400
            ? 'Some of the entered data was invalid. Please check the subject and maximum marks.'
            : err.status === 409
              ? (err.error?.message ?? 'This term is locked.')
              : 'Unable to save the assessment. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
