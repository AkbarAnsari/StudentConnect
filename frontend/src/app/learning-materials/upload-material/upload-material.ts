import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AttendanceService } from '../../attendance/attendance.service';
import { AuthService } from '../../auth/auth.service';
import { DEMO_TEACHER_ID } from '../../homework/teacher-homework/teacher-homework';
import { StudentService } from '../../students/student.service';
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
  private readonly attendanceService = inject(AttendanceService);
  private readonly studentService = inject(StudentService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly fileError = signal('');
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    subjectId: [0, [Validators.required, Validators.min(1)]],
    gradeId: [0, [Validators.required, Validators.min(1)]],
  });

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;

    this.fileError.set('');
    this.selectedFile.set(null);

    if (!file) {
      this.fileError.set('Please select a file.');
      return;
    }

    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(extension)) {
      this.fileError.set('This file type is not supported.');
      return;
    }

    if (file.size > MAX_FILE_SIZE_BYTES) {
      this.fileError.set('File size must be less than 10 MB.');
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

    const { title, description, subjectId, gradeId } = this.form.getRawValue();
    const formData = new FormData();
    formData.append('title', title);
    formData.append('description', description);
    formData.append('subjectId', String(subjectId));
    formData.append('gradeId', String(gradeId));
    formData.append('teacherId', String(this.authService.currentUser()?.teacherId ?? DEMO_TEACHER_ID));
    formData.append('file', file);

    this.submitting.set(true);
    this.errorMessage.set('');

    this.materialsService.upload(formData).subscribe({
      next: () => this.router.navigate(['/teacher-materials']),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.status === 400
            ? (err.error?.message ?? 'Some of the entered data was invalid.')
            : 'Unable to upload learning material. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
