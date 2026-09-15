import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { validateFile } from '../../shared/file-validation.util';
import { StudentImportResult } from '../student.model';
import { StudentService } from '../student.service';

const ALLOWED_EXTENSIONS = ['.csv'];
const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024;

@Component({
  selector: 'app-import-students',
  imports: [RouterLink],
  templateUrl: './import-students.html',
  styleUrl: './import-students.css',
})
export class ImportStudents {
  private readonly studentService = inject(StudentService);

  protected readonly selectedFile = signal<File | null>(null);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly result = signal<StudentImportResult | null>(null);

  protected onFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.selectedFile.set(null);
    this.result.set(null);

    const error = validateFile(file, ALLOWED_EXTENSIONS, MAX_FILE_SIZE_BYTES);
    this.errorMessage.set(error ?? '');
    if (error) {
      return;
    }

    this.selectedFile.set(file);
  }

  protected onSubmit(): void {
    const file = this.selectedFile();
    if (!file) {
      this.errorMessage.set('Please choose a CSV file first.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');
    this.result.set(null);

    this.studentService.importStudents(file).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.result.set(result);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.message ?? 'Unable to import students. Please try again.');
      },
    });
  }
}
