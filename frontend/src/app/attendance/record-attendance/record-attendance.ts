import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { map } from 'rxjs';
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
  private readonly students = toSignal(this.studentService.getStudents().pipe(map((r) => r.items)), { initialValue: [] as Student[] });

  protected readonly gradeId = signal<number | null>(null);
  protected readonly subjectId = signal<number | null>(null);
  protected readonly date = signal(new Date().toISOString().slice(0, 10));
  protected readonly correctionReason = signal('');

  protected readonly statusByStudentId = signal<Record<number, AttendanceStatus>>({});
  protected readonly loadingExisting = signal(false);
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

  constructor() {
    // Prefill from whatever is already recorded for this subject+date, so re-saving (e.g. to
    // correct one student) doesn't silently overwrite everyone else back to the "Present" default.
    effect(() => {
      const subjectId = this.subjectId();
      const date = this.date();

      if (subjectId === null) {
        this.statusByStudentId.set({});
        return;
      }

      this.loadingExisting.set(true);
      this.attendanceService.getBySubjectAndDate(subjectId, date).subscribe({
        next: (records) => {
          this.loadingExisting.set(false);
          this.statusByStudentId.set(Object.fromEntries(records.map((r) => [r.studentId, r.status])));
        },
        error: () => {
          this.loadingExisting.set(false);
          this.statusByStudentId.set({});
        },
      });
    });
  }

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
      reason: this.correctionReason() || undefined,
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
