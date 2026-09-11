import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { AttendanceService } from '../attendance.service';
import { AttendanceStatus, BulkAttendanceItem } from '../attendance.model';
import { Student } from '../../students/student.model';
import { StudentService } from '../../students/student.service';

@Component({
  selector: 'app-record-attendance',
  imports: [FormsModule],
  templateUrl: './record-attendance.html',
  styleUrl: './record-attendance.css',
})
export class RecordAttendance {
  private readonly attendanceService = inject(AttendanceService);
  private readonly studentService = inject(StudentService);

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  private readonly students = toSignal(this.studentService.getStudents(), { initialValue: [] as Student[] });

  protected readonly gradeId = signal<number | null>(null);
  protected readonly subjectId = signal<number | null>(null);
  protected readonly date = signal(new Date().toISOString().slice(0, 10));

  protected readonly statusByStudentId = signal<Record<number, AttendanceStatus>>({});
  protected readonly saving = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');

  protected readonly classStudents = computed(() => {
    const grade = this.gradeId();
    if (grade === null) {
      return [];
    }
    return this.students().filter((s) => s.gradeId === grade);
  });

  protected statusFor(studentId: number): AttendanceStatus {
    return this.statusByStudentId()[studentId] ?? 'Present';
  }

  protected onStatusChange(studentId: number, status: AttendanceStatus): void {
    this.statusByStudentId.update((current) => ({ ...current, [studentId]: status }));
  }

  protected onSave(): void {
    const subjectId = this.subjectId();
    const date = this.date();
    const students = this.classStudents();

    if (subjectId === null || students.length === 0) {
      this.errorMessage.set('Select a grade and subject before saving.');
      return;
    }

    const records: BulkAttendanceItem[] = students.map((s) => ({
      studentId: s.id,
      status: this.statusFor(s.id),
    }));

    this.saving.set(true);
    this.successMessage.set('');
    this.errorMessage.set('');

    this.attendanceService.saveBulkAttendance({ subjectId, date, records }).subscribe({
      next: () => {
        this.saving.set(false);
        this.successMessage.set('Attendance saved successfully.');
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(
          err.status === 400
            ? 'Some of the entered data was invalid. Please check the grade, subject, and date.'
            : 'Unable to save attendance. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
