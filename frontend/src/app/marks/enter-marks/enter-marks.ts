import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, ElementRef, HostListener, computed, effect, inject, signal, viewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { SectionStudent } from '../../academic-structure/academic-structure.model';
import { AuthService } from '../../auth/auth.service';
import { TeacherSubjectAssignment } from '../../teachers/teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { Assessment, BulkMarkItem, MarkHistory } from '../marks.model';
import { MarksService } from '../marks.service';

@Component({
  selector: 'app-enter-marks',
  imports: [FormsModule, RouterLink, DecimalPipe, DatePipe],
  templateUrl: './enter-marks.html',
  styleUrl: './enter-marks.css',
})
export class EnterMarks {
  private readonly marksService = inject(MarksService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  private readonly authService = inject(AuthService);

  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly loadingAssignments = signal(true);
  protected readonly assignmentId = signal<number | null>(null);
  protected readonly selectedAssignment = computed(() =>
    this.assignments().find((assignment) => assignment.id === this.assignmentId()) ?? null,
  );
  protected readonly students = signal<SectionStudent[]>([]);
  protected readonly loadingClass = signal(false);
  protected readonly assessmentId = signal<number | null>(null);
  protected readonly assessments = signal<Assessment[]>([]);
  protected readonly marksByStudentId = signal<Record<number, number | null>>({});
  protected readonly initialMarksByStudentId = signal<Record<number, number | null>>({});
  protected readonly commentsByStudentId = signal<Record<number, string>>({});
  protected readonly initialCommentsByStudentId = signal<Record<number, string>>({});
  protected readonly versionsByStudentId = signal<Record<number, number>>({});
  protected readonly markIdsByStudentId = signal<Record<number, number>>({});
  private readonly resultsRefresh = signal(0);
  protected readonly loadingExisting = signal(false);

  protected readonly saving = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');
  protected readonly exporting = signal(false);
  protected readonly importing = signal(false);
  protected readonly importFile = signal<File | null>(null);
  protected readonly historyLoading = signal(false);
  protected readonly selectedHistory = signal<{ studentName: string; entries: MarkHistory[] } | null>(null);
  private readonly markInputs = viewChildren<ElementRef<HTMLInputElement>>('markInput');

  protected readonly selectedAssessment = computed(() =>
    this.assessments().find((a) => a.id === this.assessmentId()) ?? null,
  );
  protected readonly draftAssessments = computed(() => {
    const assignmentId = this.assignmentId();
    return this.assessments().filter((assessment) =>
      assessment.status === 'Draft' && assessment.teacherSubjectAssignmentId === assignmentId,
    );
  });
  protected readonly pendingStudentIds = computed(() => {
    const initial = this.initialMarksByStudentId();
    return this.students()
      .filter((student) => this.markFor(student.id) !== (initial[student.id] ?? null) || this.commentFor(student.id) !== (this.initialCommentsByStudentId()[student.id] ?? ''))
      .map((student) => student.id);
  });
  protected readonly pendingCount = computed(() => this.pendingStudentIds().length);
  protected readonly enteredCount = computed(() => this.students().filter((student) => this.markFor(student.id) !== null).length);
  protected readonly invalidCount = computed(() => {
    const assessment = this.selectedAssessment();
    if (!assessment) return 0;
    return this.students().filter((student) => {
      const mark = this.markFor(student.id);
      const clearedExisting = mark === null && this.initialMarksByStudentId()[student.id] != null;
      return clearedExisting || (mark !== null && (mark < 0 || mark > assessment.maximumMarks));
    }).length;
  });
  protected readonly classAverage = computed(() => {
    const assessment = this.selectedAssessment();
    const values = this.students()
      .map((student) => this.markFor(student.id))
      .filter((mark): mark is number => mark !== null && assessment !== null && mark >= 0 && mark <= assessment.maximumMarks);
    if (!assessment || values.length === 0) return 0;
    return Math.round(values.reduce((sum, mark) => sum + mark, 0) * 100 / (values.length * assessment.maximumMarks));
  });
  protected readonly canSave = computed(() =>
    this.selectedAssessment() !== null && this.pendingCount() > 0 && this.invalidCount() === 0 && !this.loadingExisting() && !this.saving(),
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

    // Prefill from whatever marks are already recorded for this assessment, so the teacher can
    // see (and selectively correct) existing values instead of facing a misleadingly blank grid.
    effect(() => {
      const assessmentId = this.assessmentId();
      this.resultsRefresh();

      if (assessmentId === null) {
        this.marksByStudentId.set({});
        this.initialMarksByStudentId.set({});
        this.commentsByStudentId.set({});
        this.initialCommentsByStudentId.set({});
        this.versionsByStudentId.set({});
        this.markIdsByStudentId.set({});
        return;
      }

      this.loadingExisting.set(true);
      this.marksService.getAssessmentResults(assessmentId).subscribe({
        next: (results) => {
          const existingMarks = Object.fromEntries(results.map((result) => [result.studentId, result.marksObtained]));
          const comments = Object.fromEntries(results.map((result) => [result.studentId, result.comment ?? '']));
          this.loadingExisting.set(false);
          this.marksByStudentId.set(existingMarks);
          this.initialMarksByStudentId.set(existingMarks);
          this.commentsByStudentId.set(comments);
          this.initialCommentsByStudentId.set(comments);
          this.versionsByStudentId.set(Object.fromEntries(results.map((result) => [result.studentId, result.version])));
          this.markIdsByStudentId.set(Object.fromEntries(results.map((result) => [result.studentId, result.markId])));
        },
        error: () => {
          this.loadingExisting.set(false);
          this.marksByStudentId.set({});
          this.initialMarksByStudentId.set({});
          this.commentsByStudentId.set({});
          this.initialCommentsByStudentId.set({});
          this.versionsByStudentId.set({});
          this.markIdsByStudentId.set({});
          this.errorMessage.set('Unable to load existing marks. Nothing has been changed.');
        },
      });
    });
  }

  protected onAssignmentChange(assignmentId: number | null): void {
    this.assignmentId.set(assignmentId);
    this.assessmentId.set(null);
    this.assessments.set([]);
    this.students.set([]);
    this.marksByStudentId.set({});
    this.initialMarksByStudentId.set({});
    this.errorMessage.set('');

    const assignment = this.selectedAssignment();
    if (assignment === null) {
      return;
    }

    this.loadingClass.set(true);
    forkJoin({
      students: this.assignmentService.getStudents(assignment.id),
      assessments: this.marksService.getAssessmentsBySubject(assignment.subjectId),
    }).subscribe({
      next: ({ students, assessments }) => {
        this.students.set(students);
        this.assessments.set(assessments);
        this.loadingClass.set(false);
      },
      error: () => {
        this.loadingClass.set(false);
        this.errorMessage.set('Unable to load the class roster and assessments.');
      },
    });
  }

  protected markFor(studentId: number): number | null {
    return this.marksByStudentId()[studentId] ?? null;
  }

  protected isMarkInvalid(studentId: number): boolean {
    const assessment = this.selectedAssessment();
    const mark = this.markFor(studentId);
    return assessment !== null && mark !== null && (mark < 0 || mark > assessment.maximumMarks);
  }

  protected commentFor(studentId: number): string { return this.commentsByStudentId()[studentId] ?? ''; }

  protected onCommentChange(studentId: number, comment: string): void {
    this.commentsByStudentId.update((current) => ({ ...current, [studentId]: comment }));
    this.successMessage.set('');
  }

  protected onAssessmentChange(assessmentId: number | null): void {
    this.assessmentId.set(assessmentId);
    this.successMessage.set('');
    this.errorMessage.set('');
  }

  protected onMarkChange(studentId: number, marks: number | null): void {
    this.marksByStudentId.update((current) => ({ ...current, [studentId]: marks }));
    this.successMessage.set('');
  }

  protected onMarkKeydown(event: KeyboardEvent, rowIndex: number): void {
    const direction = event.key === 'ArrowUp' ? -1 : event.key === 'ArrowDown' || event.key === 'Enter' ? 1 : 0;
    if (direction === 0) return;
    const target = this.markInputs()[rowIndex + direction];
    if (!target) return;
    event.preventDefault();
    target.nativeElement.focus();
    target.nativeElement.select();
  }

  protected onSave(): void {
    const assessment = this.selectedAssessment();
    const assignment = this.selectedAssignment();
    if (!assessment || !assignment) {
      this.errorMessage.set('Select an assigned class and assessment before saving.');
      return;
    }

    if (this.invalidCount() > 0) {
      this.errorMessage.set('Fix invalid or cleared marks before saving. Existing marks cannot be cleared from this grid.');
      return;
    }

    const pendingStudentIds = new Set(this.pendingStudentIds());
    const records: BulkMarkItem[] = this.students()
      .filter((student) => pendingStudentIds.has(student.id) && this.markFor(student.id) !== null)
      .map((student) => ({
        studentId: student.id,
        marksObtained: this.markFor(student.id)!,
        comment: this.commentFor(student.id).trim() || null,
        expectedVersion: this.versionsByStudentId()[student.id],
      }));

    if (records.length === 0) {
      this.errorMessage.set('Enter at least one mark before saving.');
      return;
    }

    this.saving.set(true);
    this.successMessage.set('');
    this.errorMessage.set('');

    this.marksService.saveBulkMarks({ sectionId: assignment.sectionId, assessmentId: assessment.id, marks: records }).subscribe({
      next: () => {
        this.saving.set(false);
        this.successMessage.set(`Saved ${records.length} ${records.length === 1 ? 'mark' : 'marks'}.`);
        this.resultsRefresh.update((value) => value + 1);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.message ?? 'Unable to save marks. Please try again.');
      },
    });
  }

  protected exportCsv(): void {
    const assessmentId = this.assessmentId();
    if (!assessmentId) return;
    this.exporting.set(true);
    this.marksService.exportAssessmentResults(assessmentId).subscribe({
      next: (blob) => { this.exporting.set(false); triggerBlobDownload(blob, `assessment-${assessmentId}-marks.csv`); },
      error: () => { this.exporting.set(false); this.errorMessage.set('The gradebook export could not be downloaded.'); },
    });
  }

  protected onImportFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.importFile.set(file && file.name.toLowerCase().endsWith('.csv') ? file : null);
    this.errorMessage.set(file && !file.name.toLowerCase().endsWith('.csv') ? 'Select a CSV file exported from this gradebook.' : '');
  }

  protected importCsv(): void {
    const file = this.importFile();
    const assessmentId = this.assessmentId();
    const assignment = this.selectedAssignment();
    if (!file || !assessmentId || !assignment) return;
    this.importing.set(true);
    this.errorMessage.set('');
    this.marksService.importAssessmentResults(assessmentId, assignment.sectionId, file).subscribe({
      next: (result) => {
        this.importing.set(false);
        this.importFile.set(null);
        this.successMessage.set(`Imported ${result.count} ${result.count === 1 ? 'mark' : 'marks'}.`);
        this.resultsRefresh.update((value) => value + 1);
      },
      error: (err: HttpErrorResponse) => {
        this.importing.set(false);
        this.errorMessage.set(err.error?.message ?? 'The gradebook CSV could not be imported.');
      },
    });
  }

  protected openHistory(student: SectionStudent): void {
    const markId = this.markIdsByStudentId()[student.id];
    if (!markId) return;
    this.historyLoading.set(true);
    this.marksService.getMarkHistory(markId).subscribe({
      next: (entries) => { this.historyLoading.set(false); this.selectedHistory.set({ studentName: `${student.firstName} ${student.lastName}`, entries }); },
      error: () => { this.historyLoading.set(false); this.errorMessage.set('Mark history could not be loaded.'); },
    });
  }

  protected closeHistory(): void { this.selectedHistory.set(null); }

  @HostListener('document:keydown.escape')
  protected onEscape(): void { this.closeHistory(); }

  hasUnsavedChanges(): boolean {
    return this.pendingCount() > 0 && !this.saving();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
