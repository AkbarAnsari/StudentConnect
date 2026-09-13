import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
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

  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    teacherId: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    username: [''],
    password: [''],
  });

  constructor() {
    this.loadTeachers();
  }

  private loadTeachers(): void {
    this.teacherService.getTeachers().subscribe((teachers) => this.teachers.set(teachers));
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

  protected deleteTeacher(id: number): void {
    this.errorMessage.set('');
    this.teacherService.deleteTeacher(id).subscribe({
      next: () => this.loadTeachers(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the teacher.'),
    });
  }
}
