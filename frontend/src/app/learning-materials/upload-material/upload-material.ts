import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { validateFile } from '../../shared/file-validation.util';
import { TeacherSubjectAssignment } from '../../teachers/teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { LearningMaterialsService } from '../learning-materials.service';

const ALLOWED_EXTENSIONS = ['.pdf', '.docx', '.png', '.jpg', '.jpeg'];
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024;

@Component({
  selector: 'app-upload-material',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './upload-material.html',
  styleUrl: './upload-material.css',
})
export class UploadMaterial {
  private readonly fb = inject(FormBuilder);
  private readonly materialsService = inject(LearningMaterialsService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly loadingAssignments = signal(true);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly fileError = signal('');
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    topic: ['', Validators.required],
    assignmentId: [0, [Validators.required, Validators.min(1)]],
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

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.selectedFile.set(null);
    const error = validateFile(file, ALLOWED_EXTENSIONS, MAX_FILE_SIZE_BYTES);
    this.fileError.set(error ?? '');
    if (error) {
      return;
    }

    this.selectedFile.set(file);
  }

  protected onSubmit(): void {
    const file = this.selectedFile();
    if (this.form.invalid || !file) {
      this.form.markAllAsTouched();
      if (!file) {
        this.fileError.set('Please select a file.');
      }
      return;
    }

    const { title, description, topic, assignmentId, releaseMode, scheduledFor } = this.form.getRawValue();
    const assignment = this.assignments().find((item) => item.id === assignmentId);
    if (!assignment) {
      this.errorMessage.set('Select an assigned class.');
      return;
    }
    if (releaseMode === 'Scheduled' && !scheduledFor) {
      this.errorMessage.set('Choose a future release date and time.');
      return;
    }
    const formData = new FormData();
    formData.append('title', title);
    formData.append('description', description);
    formData.append('topic', topic);
    formData.append('subjectId', String(assignment.subjectId));
    formData.append('gradeId', String(assignment.gradeId));
    formData.append('sectionId', String(assignment.sectionId));
    formData.append('teacherId', String(assignment.teacherId));
    formData.append('status', releaseMode);
    if (releaseMode === 'Scheduled') formData.append('scheduledForUtc', new Date(scheduledFor).toISOString());
    formData.append('file', file);

    this.submitting.set(true);
    this.errorMessage.set('');

    this.materialsService.upload(formData).subscribe({
      next: () => {
        this.saved = true;
        this.router.navigate(['/teacher-materials']);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.message ?? 'Unable to upload learning material. Please try again.');
      },
    });
  }

  hasUnsavedChanges(): boolean {
    return (this.form.dirty || this.selectedFile() !== null) && !this.saved && !this.submitting();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
