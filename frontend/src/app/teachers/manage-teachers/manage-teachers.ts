import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { Teacher } from '../teacher.model';
import { TeacherService } from '../teacher.service';

@Component({
  selector: 'app-manage-teachers',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './manage-teachers.html',
  styleUrl: './manage-teachers.css',
})
export class ManageTeachers {
  private readonly fb = inject(FormBuilder);
  private readonly teacherService = inject(TeacherService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);
  protected readonly query = signal('');

  protected readonly editingId = signal<number | null>(null);
  protected readonly editSubmitting = signal(false);
  protected readonly editError = signal('');
  protected readonly filteredTeachers = computed(() => {
    const query = this.query().trim().toLowerCase();
    return query
      ? this.teachers().filter((teacher) => `${teacher.firstName} ${teacher.lastName} ${teacher.teacherId} ${teacher.email} ${teacher.username ?? ''}`.toLowerCase().includes(query))
      : this.teachers();
  });

  protected readonly form = this.fb.nonNullable.group({
    teacherId: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    username: [''],
    password: [''],
  });

  protected readonly editForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
  });

  constructor() {
    this.loadTeachers();
  }

  protected loadTeachers(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.teacherService.getTeachers().subscribe({
      next: (result) => {
        this.teachers.set(result.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }

  protected startEdit(teacher: Teacher): void {
    this.editingId.set(teacher.id);
    this.editError.set('');
    this.editForm.setValue({ firstName: teacher.firstName, lastName: teacher.lastName, email: teacher.email });
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.editError.set('');
    this.editForm.markAsPristine();
  }

  protected saveEdit(id: number): void {
    if (this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.editSubmitting.set(true);
    this.editError.set('');

    this.teacherService.updateTeacher(id, this.editForm.getRawValue()).subscribe({
      next: () => {
        this.editSubmitting.set(false);
        this.editingId.set(null);
        this.editForm.markAsPristine();
        this.loadTeachers();
      },
      error: (err: HttpErrorResponse) => {
        this.editSubmitting.set(false);
        this.editError.set(err.error?.message ?? 'Unable to update the teacher.');
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

    const { teacherId, firstName, lastName, email, username, password } = this.form.getRawValue();
    this.teacherService
      .createTeacher({
        teacherId,
        firstName,
        lastName,
        email,
        username: username || undefined,
        password: password || undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.form.reset();
          this.loadTeachers();
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(err.error?.message ?? 'Unable to create the teacher. Please check whether the StudentConnect API is running.');
        },
      });
  }

  protected async deleteTeacher(teacher: Teacher): Promise<void> {
    if (!(await this.confirmDialog.confirm(`Delete ${teacher.firstName} ${teacher.lastName}? Their teacher record and assignments may be affected.`))) return;
    this.errorMessage.set('');
    this.teacherService.deleteTeacher(teacher.id).subscribe({
      next: () => this.loadTeachers(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the teacher.'),
    });
  }

  hasUnsavedChanges(): boolean {
    return (this.form.dirty || (this.editingId() !== null && this.editForm.dirty)) && !this.submitting() && !this.editSubmitting();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
