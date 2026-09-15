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
  private readonly attendanceService = inject(AttendanceService);
  private readonly studentService = inject(StudentService);
  private readonly teacherService = inject(TeacherService);
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly teachers = toSignal(this.teacherService.getTeachers().pipe(map((r) => r.items)), { initialValue: [] });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    subjectId: [0, [Validators.required, Validators.min(1)]],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    dueDate: [new Date().toISOString().slice(0, 10), Validators.required],
    teacherId: [this.authService.currentUser()?.teacherId ?? 0, [Validators.required, Validators.min(1)]],
  });

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.homeworkService
      .createHomework(this.form.getRawValue())
      .subscribe({
        next: () => this.router.navigate(['/teacher-homework']),
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(
            err.status === 400
              ? 'Some of the entered data was invalid. Please check the subject and grade.'
              : 'Unable to save the homework. Please check whether the StudentConnect API is running.',
          );
        },
      });
  }
}
