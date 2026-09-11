import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { Homework, HomeworkSubmission } from '../homework.model';
import { HomeworkService, HomeworkSubmissionService } from '../homework.service';

interface HomeworkDetailState {
  loading: boolean;
  error: boolean;
  homework: Homework | null;
}

const LOADING_STATE: HomeworkDetailState = { loading: true, error: false, homework: null };

@Component({
  selector: 'app-homework-detail',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './homework-detail.html',
  styleUrl: './homework-detail.css',
})
export class HomeworkDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly homeworkService = inject(HomeworkService);
  private readonly submissionService = inject(HomeworkSubmissionService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly homeworkId = Number(this.route.snapshot.paramMap.get('homeworkId'));

  protected readonly state = toSignal(
    this.homeworkService.getHomeworkById(this.homeworkId).pipe(
      map((homework): HomeworkDetailState => ({ loading: false, error: false, homework })),
      catchError(() => of<HomeworkDetailState>({ loading: false, error: true, homework: null })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly existingSubmission = signal<HomeworkSubmission | null>(null);
  protected readonly checkedForSubmission = signal(false);
  protected readonly submitting = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    comments: ['', Validators.required],
  });

  constructor() {
    this.submissionService.getStudentSubmissions(this.studentId).subscribe({
      next: (submissions) => {
        this.existingSubmission.set(submissions.find((s) => s.homeworkId === this.homeworkId) ?? null);
        this.checkedForSubmission.set(true);
      },
      error: () => this.checkedForSubmission.set(true),
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.submissionService
      .createSubmission({ homeworkId: this.homeworkId, comments: this.form.getRawValue().comments })
      .subscribe({
        next: (submission) => {
          this.submitting.set(false);
          this.existingSubmission.set(submission);
          this.successMessage.set('Homework submitted successfully.');
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(
            err.status === 409
              ? 'You have already submitted this homework.'
              : 'Unable to submit homework. Please check whether the StudentConnect API is running.',
          );
        },
      });
  }
}
