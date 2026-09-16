import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { TeacherSubjectAssignment } from '../../teachers/teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { HomeworkService } from '../homework.service';

@Component({
  selector: 'app-create-homework',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './create-homework.html',
  styleUrl: './create-homework.css',
})
export class CreateHomework {
  private readonly fb = inject(FormBuilder);
  private readonly homeworkService = inject(HomeworkService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly loadingAssignments = signal(true);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    assignmentId: [0, [Validators.required, Validators.min(1)]],
    dueDate: [new Date().toISOString().slice(0, 10), Validators.required],
    releaseMode: ['Draft' as 'Draft' | 'Published' | 'Scheduled'],
    scheduledFor: [''],
  });

  constructor() {
    const request = this.authService.role() === 'Admin' ? this.assignmentService.getAll() : this.assignmentService.getMine();
    request.subscribe({
      next: (assignments) => {
        this.assignments.set(assignments);
        this.loadingAssignments.set(false);
      },
      error: () => {
        this.loadingAssignments.set(false);
        this.errorMessage.set('Unable to load class assignments.');
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

    const { assignmentId, releaseMode, scheduledFor, ...values } = this.form.getRawValue();
    const assignment = this.assignments().find((item) => item.id === assignmentId);
    if (!assignment) {
      this.errorMessage.set('Select an assigned class.');
      this.submitting.set(false);
      return;
    }

    if (releaseMode === 'Scheduled' && !scheduledFor) {
      this.errorMessage.set('Choose a future release date and time.');
      this.submitting.set(false);
      return;
    }

    this.homeworkService
      .createHomework({
        ...values,
        subjectId: assignment.subjectId,
        gradeId: assignment.gradeId,
        sectionId: assignment.sectionId,
        teacherId: assignment.teacherId,
        status: releaseMode,
        scheduledForUtc: releaseMode === 'Scheduled' ? new Date(scheduledFor).toISOString() : null,
      })
      .subscribe({
        next: () => {
          this.saved = true;
          this.router.navigate(['/teacher-homework']);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(err.error?.message ?? 'Unable to save the homework. Please try again.');
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
