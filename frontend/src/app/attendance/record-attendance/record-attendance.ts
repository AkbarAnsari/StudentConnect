import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, HostListener, computed, effect, inject, signal, viewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AttendanceService } from '../attendance.service';
import { AttendanceStatus, BulkAttendanceItem } from '../attendance.model';
import { SectionStudent } from '../../academic-structure/academic-structure.model';
import { AuthService } from '../../auth/auth.service';
import { TeacherSubjectAssignment } from '../../teachers/teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';

@Component({
  selector: 'app-record-attendance',
  imports: [FormsModule],
  templateUrl: './record-attendance.html',
  styleUrl: './record-attendance.css',
})
export class RecordAttendance {
  private readonly attendanceService = inject(AttendanceService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  private readonly authService = inject(AuthService);

  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly loadingAssignments = signal(true);
  protected readonly assignmentId = signal<number | null>(null);
  protected readonly selectedAssignment = computed(() =>
    this.assignments().find((assignment) => assignment.id === this.assignmentId()) ?? null,
  );
  protected readonly classStudents = signal<SectionStudent[]>([]);
  protected readonly date = signal(new Date().toISOString().slice(0, 10));
  protected readonly correctionReason = signal('');

  protected readonly statusByStudentId = signal<Record<number, AttendanceStatus>>({});
  protected readonly initialStatusByStudentId = signal<Record<number, AttendanceStatus>>({});
  protected readonly recordedStudentIds = signal<Set<number>>(new Set());
  protected readonly loadingExisting = signal(false);
  protected readonly saving = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');
  protected readonly focusedStudentId = signal<number | null>(null);
  private readonly statusGroups = viewChildren<ElementRef<HTMLElement>>('statusGroup');
  protected readonly presentCount = computed(() => this.classStudents().filter((student) => this.statusFor(student.id) === 'Present').length);
  protected readonly absentCount = computed(() => this.classStudents().filter((student) => this.statusFor(student.id) === 'Absent').length);
  protected readonly lateCount = computed(() => this.classStudents().filter((student) => this.statusFor(student.id) === 'Late').length);
  protected readonly excusedCount = computed(() => this.classStudents().filter((student) => this.statusFor(student.id) === 'Excused').length);
  protected readonly pendingStudentIds = computed(() => {
    const original = this.initialStatusByStudentId();
    const recorded = this.recordedStudentIds();
    return this.classStudents()
      .filter((student) => !recorded.has(student.id) || this.statusFor(student.id) !== original[student.id])
      .map((student) => student.id);
  });
  protected readonly pendingCount = computed(() => this.pendingStudentIds().length);
  protected readonly requiresCorrectionReason = computed(() => {
    const original = this.initialStatusByStudentId();
    const recorded = this.recordedStudentIds();
    return this.classStudents().some(
      (student) => recorded.has(student.id) && this.statusFor(student.id) !== original[student.id],
    );
  });
  protected readonly canSave = computed(() =>
    this.selectedAssignment() !== null &&
    this.classStudents().length > 0 &&
    this.pendingCount() > 0 &&
    !this.loadingExisting() &&
    !this.saving() &&
    (!this.requiresCorrectionReason() || this.correctionReason().trim().length > 0),
  );

  constructor() {
    const assignmentsRequest = this.authService.role() === 'Admin'
      ? this.assignmentService.getAll()
      : this.assignmentService.getMine();
    assignmentsRequest.subscribe({
      next: (assignments) => {
        this.assignments.set(assignments);
        this.loadingAssignments.set(false);
      },
      error: () => {
        this.loadingAssignments.set(false);
        this.errorMessage.set('Unable to load your class assignments.');
      },
    });

    effect((onCleanup) => {
      const assignment = this.selectedAssignment();
      const date = this.date();

      if (assignment === null) {
        this.classStudents.set([]);
        this.statusByStudentId.set({});
        this.initialStatusByStudentId.set({});
        this.recordedStudentIds.set(new Set());
        return;
      }

      this.loadingExisting.set(true);
      this.errorMessage.set('');
      const subscription = forkJoin({
        students: this.assignmentService.getStudents(assignment.id),
        records: this.attendanceService.getBySubjectAndDate(assignment.subjectId, date),
      }).subscribe({
        next: ({ students, records }) => {
          const existingStatuses = Object.fromEntries(records.map((record) => [record.studentId, record.status]));
          this.loadingExisting.set(false);
          this.classStudents.set(students);
          this.statusByStudentId.set(existingStatuses);
          this.initialStatusByStudentId.set(existingStatuses);
          this.recordedStudentIds.set(new Set(records.map((record) => record.studentId)));
        },
        error: () => {
          this.loadingExisting.set(false);
          this.classStudents.set([]);
          this.statusByStudentId.set({});
          this.initialStatusByStudentId.set({});
          this.recordedStudentIds.set(new Set());
          this.errorMessage.set('Unable to load the class roster and existing attendance. Nothing has been changed.');
        },
      });
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected onAssignmentChange(value: number | null): void {
    this.assignmentId.set(value);
    this.correctionReason.set('');
    this.successMessage.set('');
  }

  protected onDateChange(value: string): void {
    this.date.set(value);
    this.correctionReason.set('');
    this.successMessage.set('');
  }

  protected statusFor(studentId: number): AttendanceStatus {
    return this.statusByStudentId()[studentId] ?? 'Present';
  }

  protected onStatusChange(studentId: number, status: AttendanceStatus): void {
    this.statusByStudentId.update((current) => ({ ...current, [studentId]: status }));
    this.successMessage.set('');
  }

  protected markAll(status: AttendanceStatus): void {
    this.statusByStudentId.set(Object.fromEntries(this.classStudents().map((student) => [student.id, status])));
    this.successMessage.set('');
  }

  protected onStatusKeydown(event: KeyboardEvent, rowIndex: number, studentId: number): void {
    const key = event.key.toLowerCase();
    if (key === 'p' || event.key === 'ArrowLeft') {
      event.preventDefault();
      this.onStatusChange(studentId, 'Present');
      return;
    }
    if (key === 'a' || event.key === 'ArrowRight') {
      event.preventDefault();
      this.onStatusChange(studentId, 'Absent');
      return;
    }
    if (key === 'l') {
      event.preventDefault();
      this.onStatusChange(studentId, 'Late');
      return;
    }
    if (key === 'e') {
      event.preventDefault();
      this.onStatusChange(studentId, 'Excused');
      return;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      const statuses: AttendanceStatus[] = ['Present', 'Absent', 'Late', 'Excused'];
      this.onStatusChange(studentId, statuses[(statuses.indexOf(this.statusFor(studentId)) + 1) % statuses.length]);
      return;
    }

    const targetIndex = event.key === 'ArrowUp'
      ? rowIndex - 1
      : event.key === 'ArrowDown'
        ? rowIndex + 1
        : event.key === 'Home'
          ? 0
          : event.key === 'End'
            ? this.statusGroups().length - 1
            : rowIndex;
    if (targetIndex !== rowIndex && targetIndex >= 0 && targetIndex < this.statusGroups().length) {
      event.preventDefault();
      this.statusGroups()[targetIndex].nativeElement.focus();
    }
  }

  protected onSave(): void {
    const assignment = this.selectedAssignment();
    const date = this.date();
    const students = this.classStudents();

    if (assignment === null || students.length === 0) {
      this.errorMessage.set('Select an assigned class with active students before saving.');
      return;
    }

    if (this.requiresCorrectionReason() && !this.correctionReason().trim()) {
      this.errorMessage.set('Enter a correction reason before changing existing attendance.');
      return;
    }

    const pendingCount = this.pendingCount();

    const records: BulkAttendanceItem[] = students.map((s) => ({
      studentId: s.id,
      status: this.statusFor(s.id),
      reason: this.correctionReason() || undefined,
    }));

    this.saving.set(true);
    this.successMessage.set('');
    this.errorMessage.set('');

    this.attendanceService.saveBulkAttendance({
      sectionId: assignment.sectionId,
      subjectId: assignment.subjectId,
      date,
      records,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        const currentStatuses = Object.fromEntries(students.map((student) => [student.id, this.statusFor(student.id)]));
        this.initialStatusByStudentId.set(currentStatuses);
        this.recordedStudentIds.set(new Set(students.map((student) => student.id)));
        this.correctionReason.set('');
        this.successMessage.set(`Attendance saved for ${pendingCount} ${pendingCount === 1 ? 'student' : 'students'}.`);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.message ?? 'Unable to save attendance. Please try again.');
      },
    });
  }

  hasUnsavedChanges(): boolean {
    return this.pendingCount() > 0 && !this.saving();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
