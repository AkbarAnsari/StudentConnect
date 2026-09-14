import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { map } from 'rxjs';
import { AttendanceService } from '../../attendance/attendance.service';
import { StudentService } from '../../students/student.service';
import { Assessment, BulkMarkItem } from '../marks.model';
import { MarksService } from '../marks.service';

@Component({
  selector: 'app-enter-marks',
  imports: [FormsModule],
  templateUrl: './enter-marks.html',
  styleUrl: './enter-marks.css',
})
export class EnterMarks {
  private readonly marksService = inject(MarksService);
  private readonly attendanceService = inject(AttendanceService);
  private readonly studentService = inject(StudentService);

  protected readonly subjects = toSignal(this.attendanceService.getSubjects(), { initialValue: [] });
  protected readonly students = toSignal(this.studentService.getStudents().pipe(map((r) => r.items)), { initialValue: [] });

  protected readonly subjectId = signal<number | null>(null);
  protected readonly assessmentId = signal<number | null>(null);
  protected readonly assessments = signal<Assessment[]>([]);
  protected readonly marksByStudentId = signal<Record<number, number | null>>({});
  protected readonly loadingExisting = signal(false);

  protected readonly saving = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');

  protected readonly selectedAssessment = computed(() =>
    this.assessments().find((a) => a.id === this.assessmentId()) ?? null,
  );

  constructor() {
    // Prefill from whatever marks are already recorded for this assessment, so the teacher can
    // see (and selectively correct) existing values instead of facing a misleadingly blank grid.
    effect(() => {
      const assessmentId = this.assessmentId();

      if (assessmentId === null) {
        this.marksByStudentId.set({});
        return;
      }

      this.loadingExisting.set(true);
      this.marksService.getAssessmentResults(assessmentId).subscribe({
        next: (results) => {
          this.loadingExisting.set(false);
          this.marksByStudentId.set(Object.fromEntries(results.map((r) => [r.studentId, r.marksObtained])));
        },
        error: () => {
          this.loadingExisting.set(false);
          this.marksByStudentId.set({});
        },
      });
    });
  }

  protected onSubjectChange(subjectId: number | null): void {
    this.subjectId.set(subjectId);
    this.assessmentId.set(null);
    this.assessments.set([]);

    if (subjectId === null) {
      return;
    }

    this.marksService.getAssessmentsBySubject(subjectId).subscribe({
      next: (assessments) => this.assessments.set(assessments),
      error: () => this.errorMessage.set('Unable to load assessments for this subject.'),
    });
  }

  protected markFor(studentId: number): number | null {
    return this.marksByStudentId()[studentId] ?? null;
  }

  protected onMarkChange(studentId: number, marks: number | null): void {
    this.marksByStudentId.update((current) => ({ ...current, [studentId]: marks }));
  }

  protected onSave(): void {
    const assessment = this.selectedAssessment();
    if (!assessment) {
      this.errorMessage.set('Select a subject and assessment before saving.');
      return;
    }

    const records: BulkMarkItem[] = this.students()
      .map((s) => ({ studentId: s.id, marksObtained: this.markFor(s.id) }))
      .filter((r): r is BulkMarkItem => r.marksObtained !== null);

    if (records.length === 0) {
      this.errorMessage.set('Enter at least one mark before saving.');
      return;
    }

    this.saving.set(true);
    this.successMessage.set('');
    this.errorMessage.set('');

    this.marksService.saveBulkMarks({ assessmentId: assessment.id, marks: records }).subscribe({
      next: () => {
        this.saving.set(false);
        this.successMessage.set('Marks saved successfully.');
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(
          err.status === 400
            ? 'Some marks were invalid — check they are within the allowed range.'
            : 'Unable to save marks. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
