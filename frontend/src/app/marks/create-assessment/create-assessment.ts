import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { AuthService } from '../../auth/auth.service';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
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
  private readonly academicStructure = inject(AcademicStructureService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly assignments = toSignal(
    this.authService.role() === 'Admin' ? this.assignmentService.getAll() : this.assignmentService.getMine(),
    { initialValue: [] },
  );
  protected readonly terms = toSignal(this.academicStructure.getTerms(), { initialValue: [] });
  protected readonly assessmentTypes = ['UnitTest', 'MidTerm', 'FinalExam', 'Quiz', 'Assignment', 'Practical'];
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    assignmentId: [0, [Validators.required, Validators.min(1)]],
    date: [new Date().toISOString().slice(0, 10), Validators.required],
    maximumMarks: [100, [Validators.required, Validators.min(1)]],
    assessmentType: ['UnitTest', Validators.required],
    description: [''],
    termId: [0],
    weight: [1, [Validators.required, Validators.min(0.1)]],
  });

  protected onSubmit(publish = false): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const { termId, assignmentId, ...rest } = this.form.getRawValue();
    const assignment = this.assignments().find((item) => item.id === assignmentId);
    if (!assignment) {
      this.errorMessage.set('Select an assigned class.');
      this.submitting.set(false);
      return;
    }
    this.marksService.createAssessment({
      ...rest,
      subjectId: assignment.subjectId,
      teacherSubjectAssignmentId: assignment.id,
      termId: termId > 0 ? termId : null,
      publish,
    } as AssessmentInput).subscribe({
      next: () => {
        this.saved = true;
        this.router.navigate(['/assessments']);
      },
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

  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saved && !this.submitting();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
