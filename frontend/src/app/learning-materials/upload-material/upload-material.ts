import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AttendanceService } from '../../attendance/attendance.service';
import { AuthService } from '../../auth/auth.service';
import { StudentService } from '../../students/student.service';
import { TeacherService } from '../../teachers/teacher.service';
import { validateFile } from '../../shared/file-validation.util';
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
  private readonly teacherService = inject(TeacherService);
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly teachers = toSignal(this.teacherService.getTeachers().pipe(map((r) => r.items)), { initialValue: [] });

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly fileError = signal('');
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    subjectId: [0, [Validators.required, Validators.min(1)]],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    teacherId: [this.authService.currentUser()?.teacherId ?? 0, [Validators.required, Validators.min(1)]],
  });

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

    const { title, description, subjectId, gradeId, teacherId } = this.form.getRawValue();
    const formData = new FormData();
    formData.append('title', title);
    formData.append('description', description);
    formData.append('subjectId', String(subjectId));
    formData.append('gradeId', String(gradeId));
    formData.append('teacherId', String(teacherId));
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
