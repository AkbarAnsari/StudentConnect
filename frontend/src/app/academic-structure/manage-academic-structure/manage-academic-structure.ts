import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject } from '../../attendance/attendance.model';
import { Grade, Section } from '../../students/student.model';
import { Teacher } from '../../teachers/teacher.model';
import { TeacherService } from '../../teachers/teacher.service';
import { TeacherSubjectAssignment } from '../../teachers/teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { AcademicStructureService } from '../academic-structure.service';
import { AcademicYear, GradingBand, RolloverStudentRow, Term } from '../academic-structure.model';

@Component({
  selector: 'app-manage-academic-structure',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './manage-academic-structure.html',
  styleUrl: './manage-academic-structure.css',
})
export class ManageAcademicStructure {
  private readonly fb = inject(FormBuilder);
  private readonly academicStructure = inject(AcademicStructureService);
  private readonly teacherService = inject(TeacherService);
  private readonly teacherSubjectAssignments = inject(TeacherSubjectAssignmentService);

  protected readonly activeTab = signal<'structure' | 'calendar' | 'assignments' | 'grading' | 'rollover'>('structure');

  protected readonly grades = signal<Grade[]>([]);
  protected readonly subjects = signal<Subject[]>([]);
  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly academicYears = signal<AcademicYear[]>([]);
  protected readonly terms = signal<Term[]>([]);
  protected readonly sections = signal<Section[]>([]);
  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly errorMessage = signal('');
  protected readonly showArchivedYears = signal(false);
  protected readonly showArchivedSections = signal(false);

  protected readonly rolloverYearId = signal(0);
  protected readonly rolloverPreview = signal<RolloverStudentRow[] | null>(null);
  protected readonly rolloverLoading = signal(false);
  protected readonly rolloverMessage = signal('');
  protected readonly rolloverError = signal('');
  protected readonly rolloverForce = signal(false);

  protected readonly gradingBands = signal<GradingBand[]>([]);
  protected readonly gradingBandsMessage = signal('');
  protected readonly gradingBandsError = signal('');

  protected readonly gradeForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
  });

  protected readonly subjectForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    code: ['', Validators.required],
  });

  protected readonly yearForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
    isCurrent: [false],
  });

  protected readonly termForm = this.fb.nonNullable.group({
    academicYearId: [0, [Validators.required, Validators.min(1)]],
    name: ['', Validators.required],
    startDate: ['', Validators.required],
    endDate: ['', Validators.required],
  });

  protected readonly sectionForm = this.fb.nonNullable.group({
    gradeId: [0, [Validators.required, Validators.min(1)]],
    name: ['', Validators.required],
    homeroomTeacherId: [0],
  });

  protected readonly assignmentForm = this.fb.nonNullable.group({
    teacherId: [0, [Validators.required, Validators.min(1)]],
    subjectId: [0, [Validators.required, Validators.min(1)]],
    sectionId: [0, [Validators.required, Validators.min(1)]],
  });

  constructor() {
    this.reloadAll();
  }

  private reloadAll(): void {
    this.academicStructure.getGrades().subscribe((grades) => this.grades.set(grades));
    this.academicStructure.getSubjects().subscribe((subjects) => this.subjects.set(subjects));
    this.teacherService.getTeachers().subscribe((result) => this.teachers.set(result.items));
    this.academicStructure.getAcademicYears(this.showArchivedYears()).subscribe((years) => this.academicYears.set(years));
    this.academicStructure.getTerms().subscribe((terms) => this.terms.set(terms));
    this.academicStructure.getSections(this.showArchivedSections()).subscribe((sections) => this.sections.set(sections));
    this.academicStructure.getGradingBands().subscribe((bands) => this.gradingBands.set(bands));
    this.teacherSubjectAssignments.getAll().subscribe((assignments) => this.assignments.set(assignments));
  }

  protected onShowArchivedYearsChange(checked: boolean): void {
    this.showArchivedYears.set(checked);
    this.reloadAll();
  }

  protected onShowArchivedSectionsChange(checked: boolean): void {
    this.showArchivedSections.set(checked);
    this.reloadAll();
  }

  protected restoreAcademicYear(id: number): void {
    this.academicStructure.restoreAcademicYear(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to restore the academic year.'),
    });
  }

  protected restoreSection(id: number): void {
    this.academicStructure.restoreSection(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to restore the section.'),
    });
  }

  protected createGrade(): void {
    if (this.gradeForm.invalid) {
      this.gradeForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.academicStructure.createGrade(this.gradeForm.getRawValue()).subscribe({
      next: () => {
        this.gradeForm.reset({ name: '' });
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save the grade.'),
    });
  }

  protected deleteGrade(id: number): void {
    this.academicStructure.deleteGrade(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the grade.'),
    });
  }

  protected createSubject(): void {
    if (this.subjectForm.invalid) {
      this.subjectForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.academicStructure.createSubject(this.subjectForm.getRawValue()).subscribe({
      next: () => {
        this.subjectForm.reset({ name: '', code: '' });
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save the subject.'),
    });
  }

  protected deleteSubject(id: number): void {
    this.academicStructure.deleteSubject(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the subject.'),
    });
  }

  protected createAcademicYear(): void {
    if (this.yearForm.invalid) {
      this.yearForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.academicStructure.createAcademicYear(this.yearForm.getRawValue()).subscribe({
      next: () => {
        this.yearForm.reset({ name: '', startDate: '', endDate: '', isCurrent: false });
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save the academic year.'),
    });
  }

  protected deleteAcademicYear(id: number): void {
    this.academicStructure.deleteAcademicYear(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the academic year.'),
    });
  }

  protected createTerm(): void {
    if (this.termForm.invalid) {
      this.termForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.academicStructure.createTerm(this.termForm.getRawValue()).subscribe({
      next: () => {
        this.termForm.reset({ academicYearId: 0, name: '', startDate: '', endDate: '' });
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save the term.'),
    });
  }

  protected deleteTerm(id: number): void {
    this.academicStructure.deleteTerm(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the term.'),
    });
  }

  protected lockTerm(id: number): void {
    this.academicStructure.lockTerm(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to lock the term.'),
    });
  }

  protected unlockTerm(id: number): void {
    this.academicStructure.unlockTerm(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to unlock the term.'),
    });
  }

  protected createSection(): void {
    if (this.sectionForm.invalid) {
      this.sectionForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    const { gradeId, name, homeroomTeacherId } = this.sectionForm.getRawValue();
    this.academicStructure
      .createSection({ gradeId, name, homeroomTeacherId: homeroomTeacherId > 0 ? homeroomTeacherId : undefined })
      .subscribe({
        next: () => {
          this.sectionForm.reset({ gradeId: 0, name: '', homeroomTeacherId: 0 });
          this.reloadAll();
        },
        error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save the section.'),
      });
  }

  protected deleteSection(id: number): void {
    this.academicStructure.deleteSection(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the section.'),
    });
  }

  protected createAssignment(): void {
    if (this.assignmentForm.invalid) {
      this.assignmentForm.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.teacherSubjectAssignments.create(this.assignmentForm.getRawValue()).subscribe({
      next: () => {
        this.assignmentForm.reset({ teacherId: 0, subjectId: 0, sectionId: 0 });
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save this assignment.'),
    });
  }

  protected deleteAssignment(id: number): void {
    this.teacherSubjectAssignments.delete(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to remove this assignment.'),
    });
  }

  protected onRolloverYearChange(value: string): void {
    this.rolloverYearId.set(Number(value));
    this.rolloverPreview.set(null);
    this.rolloverMessage.set('');
    this.rolloverError.set('');
  }

  protected onRolloverForceChange(checked: boolean): void {
    this.rolloverForce.set(checked);
  }

  protected previewRollover(): void {
    if (!this.rolloverYearId()) {
      return;
    }

    this.rolloverLoading.set(true);
    this.rolloverError.set('');
    this.rolloverMessage.set('');

    this.academicStructure.previewRollover(this.rolloverYearId()).subscribe({
      next: (preview) => {
        this.rolloverLoading.set(false);
        this.rolloverPreview.set(preview);
      },
      error: (err: HttpErrorResponse) => {
        this.rolloverLoading.set(false);
        this.rolloverError.set(err.error?.message ?? 'Unable to load the rollover preview.');
      },
    });
  }

  protected commitRollover(): void {
    if (!this.rolloverYearId()) {
      return;
    }

    this.rolloverLoading.set(true);
    this.rolloverError.set('');
    this.rolloverMessage.set('');

    this.academicStructure.commitRollover(this.rolloverYearId(), this.rolloverForce()).subscribe({
      next: (result) => {
        this.rolloverLoading.set(false);
        this.rolloverPreview.set(null);
        this.rolloverMessage.set(`Promoted ${result.promoted} student(s); skipped ${result.skipped}.`);
      },
      error: (err: HttpErrorResponse) => {
        this.rolloverLoading.set(false);
        this.rolloverError.set(err.error?.message ?? 'Unable to commit the rollover.');
      },
    });
  }

  protected addBand(): void {
    this.gradingBands.update((bands) => [...bands, { id: 0, label: '', minPercentage: 0 }]);
  }

  protected removeBand(index: number): void {
    this.gradingBands.update((bands) => bands.filter((_, i) => i !== index));
  }

  protected updateBandLabel(index: number, value: string): void {
    this.gradingBands.update((bands) => bands.map((b, i) => (i === index ? { ...b, label: value } : b)));
  }

  protected updateBandMin(index: number, value: string): void {
    this.gradingBands.update((bands) => bands.map((b, i) => (i === index ? { ...b, minPercentage: Number(value) } : b)));
  }

  protected saveGradingBands(): void {
    this.gradingBandsError.set('');
    this.gradingBandsMessage.set('');

    this.academicStructure
      .replaceGradingBands(this.gradingBands().map((b) => ({ label: b.label, minPercentage: b.minPercentage })))
      .subscribe({
        next: (bands) => {
          this.gradingBands.set(bands);
          this.gradingBandsMessage.set('Grading bands saved.');
        },
        error: (err: HttpErrorResponse) => this.gradingBandsError.set(err.error?.message ?? 'Unable to save grading bands.'),
      });
  }
}
