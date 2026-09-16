import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { AssessmentType, AssessmentUpdateInput } from '../marks.model';
import { MarksService } from '../marks.service';

@Component({
  selector: 'app-edit-assessment',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './edit-assessment.html',
  styleUrl: '../create-assessment/create-assessment.css',
})
export class EditAssessment {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly marksService = inject(MarksService);
  private readonly academicStructure = inject(AcademicStructureService);
  private readonly assessmentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly assessment = toSignal(this.marksService.getAssessment(this.assessmentId), { initialValue: null });
  protected readonly terms = toSignal(this.academicStructure.getTerms(), { initialValue: [] });
  protected readonly assessmentTypes: AssessmentType[] = ['UnitTest', 'MidTerm', 'FinalExam', 'Quiz', 'Assignment', 'Practical'];
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    date: ['', Validators.required],
    maximumMarks: [100, [Validators.required, Validators.min(1)]],
    assessmentType: ['UnitTest' as AssessmentType, Validators.required],
    description: [''],
    termId: [0],
    weight: [1, [Validators.required, Validators.min(0.1)]],
  });

  constructor() {
    effect(() => {
      const assessment = this.assessment();
      if (!assessment) return;
      this.form.setValue({
        title: assessment.title,
        date: assessment.date,
        maximumMarks: assessment.maximumMarks,
        assessmentType: assessment.assessmentType,
        description: assessment.description,
        termId: assessment.termId ?? 0,
        weight: assessment.weight,
      });
      this.form.markAsPristine();
    });
  }

  protected onSubmit(): void {
    const assessment = this.assessment();
    if (!assessment || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set('');
    const { termId, ...values } = this.form.getRawValue();
    const input: AssessmentUpdateInput = { ...values, subjectId: assessment.subjectId, termId: termId || null };
    this.marksService.updateAssessment(this.assessmentId, input).subscribe({
      next: () => { this.saved = true; this.router.navigate(['/assessments']); },
      error: (err: HttpErrorResponse) => { this.submitting.set(false); this.errorMessage.set(err.error?.message ?? 'Unable to update the assessment.'); },
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saved && !this.submitting(); }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void { if (this.hasUnsavedChanges()) event.preventDefault(); }
}
