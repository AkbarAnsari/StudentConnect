import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, Observable, of, OperatorFunction } from 'rxjs';
import { Subject } from '../../attendance/attendance.model';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { Grade, Section } from '../../students/student.model';
import { Teacher } from '../../teachers/teacher.model';
import { TeacherService } from '../../teachers/teacher.service';
import { TeacherSubjectAssignment } from '../../teachers/teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { AcademicStructureService } from '../academic-structure.service';
import { AcademicYear, GradingBand, RolloverOverrideInput, RolloverRun, RolloverStudentRow, SetupDependency, SetupResourceType, Term } from '../academic-structure.model';

type ReferenceKind = 'grade' | 'subject' | 'term' | 'section';

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
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly route = inject(ActivatedRoute);

  protected readonly activeTab = signal<'overview' | 'structure' | 'calendar' | 'assignments' | 'grading' | 'rollover'>('overview');

  protected readonly grades = signal<Grade[]>([]);
  protected readonly subjects = signal<Subject[]>([]);
  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly academicYears = signal<AcademicYear[]>([]);
  protected readonly terms = signal<Term[]>([]);
  protected readonly sections = signal<Section[]>([]);
  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly dependencies = signal<SetupDependency[]>([]);
  protected readonly errorMessage = signal('');
  protected readonly loading = signal(true);
  protected readonly loadErrors = signal<string[]>([]);
  protected readonly showArchivedYears = signal(false);
  protected readonly showArchivedSections = signal(false);
  protected readonly referenceEdit = signal<{ id: number; kind: ReferenceKind; label: string } | null>(null);
  protected readonly savingReference = signal(false);
  protected readonly curriculumGradeId = signal(0);
  protected readonly curriculumSectionId = signal(0);
  protected readonly curriculumSubjectIds = signal<number[]>([]);
  protected readonly curriculumLoading = signal(false);
  protected readonly curriculumMessage = signal('');
  protected readonly curriculumError = signal('');
  protected readonly curriculumSections = computed(() => this.sections().filter(section => section.gradeId === this.curriculumGradeId() && !section.isArchived));

  protected readonly rolloverYearId = signal(0);
  protected readonly rolloverPreview = signal<RolloverStudentRow[] | null>(null);
  protected readonly rolloverLoading = signal(false);
  protected readonly rolloverMessage = signal('');
  protected readonly rolloverError = signal('');
  protected readonly rolloverForce = signal(false);
  protected readonly rolloverOverrides = signal<Record<number, RolloverOverrideInput>>({});
  protected readonly rolloverRuns = signal<RolloverRun[]>([]);

  protected readonly gradingBands = signal<GradingBand[]>([]);
  protected readonly gradingBandsMessage = signal('');
  protected readonly gradingBandsError = signal('');
  protected readonly currentYear = computed(() => this.academicYears().find((year) => year.isCurrent) ?? null);
  protected readonly setupProgress = computed(() => [
    this.academicYears().some((year) => year.isCurrent),
    this.grades().length > 0,
    this.sections().length > 0,
    this.subjects().length > 0,
    this.assignments().length > 0,
    this.gradingBands().length > 0,
  ].filter(Boolean).length);

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

  protected readonly referenceEditForm = this.fb.nonNullable.group({
    name: ['', Validators.required],
    code: [''],
    startDate: [''],
    endDate: [''],
    homeroomTeacherId: [0],
  });

  protected readonly assignmentForm = this.fb.nonNullable.group({
    teacherId: [0, [Validators.required, Validators.min(1)]],
    subjectId: [0, [Validators.required, Validators.min(1)]],
    sectionId: [0, [Validators.required, Validators.min(1)]],
  });

  constructor() {
    this.route.paramMap.subscribe((params) => {
      this.cancelReferenceEdit();
      const area = params.get('area');
      const validAreas = ['structure', 'calendar', 'assignments', 'grading', 'rollover'] as const;
      this.activeTab.set(validAreas.includes(area as typeof validAreas[number]) ? area as typeof validAreas[number] : 'overview');
      this.errorMessage.set('');
    });
    this.reloadAll();
  }

  private reloadAll(): void {
    this.loading.set(true);
    this.loadErrors.set([]);
    const recover = <T>(label: string, fallback: T): OperatorFunction<T, T> => catchError(() => {
      this.loadErrors.update((errors) => [...errors, label]);
      return of(fallback);
    });

    forkJoin({
      grades: this.academicStructure.getGrades().pipe(recover('grades', [] as Grade[])),
      subjects: this.academicStructure.getSubjects().pipe(recover('subjects', [] as Subject[])),
      teachers: this.teacherService.getTeachers().pipe(recover('teachers', { items: [] as Teacher[], totalCount: 0, page: 1, pageSize: 200 })),
      years: this.academicStructure.getAcademicYears(this.showArchivedYears()).pipe(recover('academic years', [] as AcademicYear[])),
      terms: this.academicStructure.getTerms().pipe(recover('terms', [] as Term[])),
      sections: this.academicStructure.getSections(this.showArchivedSections()).pipe(recover('sections', [] as Section[])),
      bands: this.academicStructure.getGradingBands().pipe(recover('grading bands', [] as GradingBand[])),
      assignments: this.teacherSubjectAssignments.getAll().pipe(recover('teacher assignments', [] as TeacherSubjectAssignment[])),
      dependencies: this.academicStructure.getDependencies().pipe(recover('dependency counts', [] as SetupDependency[])),
      rolloverRuns: this.academicStructure.getRolloverRuns().pipe(recover('rollover history', [] as RolloverRun[])),
    }).pipe(finalize(() => this.loading.set(false))).subscribe((result) => {
      this.grades.set(result.grades);
      this.subjects.set(result.subjects);
      this.teachers.set(result.teachers.items);
      this.academicYears.set(result.years);
      this.terms.set(result.terms);
      this.sections.set(result.sections);
      this.gradingBands.set(result.bands);
      this.assignments.set(result.assignments);
      this.dependencies.set(result.dependencies);
      this.rolloverRuns.set(result.rolloverRuns);
    });
  }

  protected retryLoad(): void {
    this.reloadAll();
  }

  protected onCurriculumGradeChange(value: string): void {
    this.curriculumGradeId.set(Number(value));
    this.curriculumSectionId.set(0);
    this.loadCurriculumDefaults();
  }

  protected onCurriculumSectionChange(value: string): void {
    this.curriculumSectionId.set(Number(value));
    this.loadCurriculumDefaults();
  }

  private loadCurriculumDefaults(): void {
    const gradeId = this.curriculumGradeId();
    if (!gradeId) { this.curriculumSubjectIds.set([]); return; }
    this.curriculumLoading.set(true);
    this.curriculumMessage.set('');
    this.curriculumError.set('');
    this.academicStructure.getCurriculumDefaults(gradeId, this.curriculumSectionId() || undefined).subscribe({
      next: defaults => { this.curriculumSubjectIds.set(defaults.subjectIds); this.curriculumLoading.set(false); },
      error: () => { this.curriculumLoading.set(false); this.curriculumError.set('Unable to load curriculum defaults.'); },
    });
  }

  protected toggleCurriculumSubject(subjectId: number, selected: boolean): void {
    this.curriculumSubjectIds.update(ids => selected ? [...new Set([...ids, subjectId])] : ids.filter(id => id !== subjectId));
  }

  protected saveCurriculumDefaults(): void {
    const gradeId = this.curriculumGradeId();
    if (!gradeId) return;
    this.curriculumLoading.set(true);
    this.curriculumMessage.set('');
    this.curriculumError.set('');
    this.academicStructure.replaceCurriculumDefaults(gradeId, this.curriculumSectionId() || null, this.curriculumSubjectIds()).subscribe({
      next: defaults => { this.curriculumSubjectIds.set(defaults.subjectIds); this.curriculumLoading.set(false); this.curriculumMessage.set('Curriculum defaults saved.'); },
      error: (err: HttpErrorResponse) => { this.curriculumLoading.set(false); this.curriculumError.set(err.error?.message ?? 'Unable to save curriculum defaults.'); },
    });
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

  protected startGradeEdit(grade: Grade): void {
    this.referenceEdit.set({ id: grade.id, kind: 'grade', label: grade.name });
    this.referenceEditForm.reset({ name: grade.name, code: '', startDate: '', endDate: '', homeroomTeacherId: 0 });
  }

  protected async deleteGrade(id: number): Promise<void> {
    const grade = this.grades().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Delete ${grade?.name ?? 'this grade'}? ${this.dependencySummary('Grade', id)} Deletion succeeds only when no records depend on it.`))) return;
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

  protected startSubjectEdit(subject: Subject): void {
    this.referenceEdit.set({ id: subject.id, kind: 'subject', label: subject.name });
    this.referenceEditForm.reset({ name: subject.name, code: subject.code, startDate: '', endDate: '', homeroomTeacherId: 0 });
  }

  protected async deleteSubject(id: number): Promise<void> {
    const subject = this.subjects().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Delete ${subject?.name ?? 'this subject'}? ${this.dependencySummary('Subject', id)} Deletion succeeds only when no records depend on it.`))) return;
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

  protected async deleteAcademicYear(id: number): Promise<void> {
    const year = this.academicYears().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Archive academic year ${year?.name ?? ''}? It will be removed from active setup lists.`))) return;
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

  protected startTermEdit(term: Term): void {
    this.referenceEdit.set({ id: term.id, kind: 'term', label: term.name });
    this.referenceEditForm.reset({ name: term.name, code: '', startDate: term.startDate, endDate: term.endDate, homeroomTeacherId: 0 });
  }

  protected async deleteTerm(id: number): Promise<void> {
    const term = this.terms().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Delete ${term?.name ?? 'this term'}? ${this.dependencySummary('Term', id)} Deletion succeeds only when no records depend on it.`))) return;
    this.academicStructure.deleteTerm(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to delete the term.'),
    });
  }

  protected async lockTerm(id: number): Promise<void> {
    const term = this.terms().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Lock ${term?.name ?? 'this term'}? Assessment and mark changes will be blocked.`))) return;
    this.academicStructure.lockTerm(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to lock the term.'),
    });
  }

  protected async unlockTerm(id: number): Promise<void> {
    const term = this.terms().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Unlock ${term?.name ?? 'this term'} and allow assessment changes again?`))) return;
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

  protected startSectionEdit(section: Section): void {
    this.referenceEdit.set({ id: section.id, kind: 'section', label: `${section.gradeName} - ${section.name}` });
    this.referenceEditForm.reset({ name: section.name, code: '', startDate: '', endDate: '', homeroomTeacherId: section.homeroomTeacherId ?? 0 });
  }

  protected cancelReferenceEdit(): void {
    this.referenceEdit.set(null);
  }

  protected saveReferenceEdit(): void {
    const edit = this.referenceEdit();
    const value = this.referenceEditForm.getRawValue();
    if (!edit || !value.name.trim() || edit.kind === 'subject' && !value.code.trim() || edit.kind === 'term' && (!value.startDate || !value.endDate)) {
      this.referenceEditForm.markAllAsTouched();
      return;
    }

    let request: Observable<unknown>;
    switch (edit.kind) {
      case 'grade': request = this.academicStructure.updateGrade(edit.id, { name: value.name }); break;
      case 'subject': request = this.academicStructure.updateSubject(edit.id, { name: value.name, code: value.code }); break;
      case 'term': request = this.academicStructure.updateTerm(edit.id, { name: value.name, startDate: value.startDate, endDate: value.endDate }); break;
      case 'section': request = this.academicStructure.updateSection(edit.id, { name: value.name, homeroomTeacherId: value.homeroomTeacherId || undefined }); break;
    }

    this.savingReference.set(true);
    this.errorMessage.set('');
    request.pipe(finalize(() => this.savingReference.set(false))).subscribe({
      next: () => {
        this.cancelReferenceEdit();
        this.reloadAll();
      },
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to save this record.'),
    });
  }

  protected async deleteSection(id: number): Promise<void> {
    const section = this.sections().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Archive ${section?.gradeName ?? ''} Section ${section?.name ?? ''}? ${this.dependencySummary('Section', id)} Existing history remains available.`))) return;
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

  protected async deleteAssignment(id: number): Promise<void> {
    const assignment = this.assignments().find((item) => item.id === id);
    if (!(await this.confirmDialog.confirm(`Remove ${assignment?.teacherName ?? 'this teacher'} from ${assignment?.subjectName ?? 'this class'}? Assigned workflow access will be removed.`))) return;
    this.teacherSubjectAssignments.delete(id).subscribe({
      next: () => this.reloadAll(),
      error: (err: HttpErrorResponse) => this.errorMessage.set(err.error?.message ?? 'Unable to remove this assignment.'),
    });
  }

  protected onRolloverYearChange(value: string): void {
    this.rolloverYearId.set(Number(value));
    this.rolloverPreview.set(null);
    this.rolloverOverrides.set({});
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
        this.rolloverOverrides.set(Object.fromEntries(preview.filter((row) => !row.excluded).map((row) => [row.studentId, {
          studentId: row.studentId,
          skip: false,
          gradeId: row.proposedGradeId,
          sectionId: row.proposedSectionId,
        }])));
      },
      error: (err: HttpErrorResponse) => {
        this.rolloverLoading.set(false);
        this.rolloverError.set(err.error?.message ?? 'Unable to load the rollover preview.');
      },
    });
  }

  protected sectionsForRollover(row: RolloverStudentRow): Section[] {
    const gradeId = this.rolloverOverrides()[row.studentId]?.gradeId;
    return this.sections().filter((section) => section.gradeId === gradeId && !section.isArchived);
  }

  protected onRolloverSkipChange(studentId: number, skip: boolean): void {
    this.updateRolloverOverride(studentId, { skip });
  }

  protected onRolloverGradeChange(row: RolloverStudentRow, value: string): void {
    const gradeId = Number(value) || null;
    const matchingSection = this.sections().find((section) => section.gradeId === gradeId && section.name === row.currentSectionName && !section.isArchived);
    this.updateRolloverOverride(row.studentId, { gradeId, sectionId: matchingSection?.id ?? null });
  }

  protected onRolloverSectionChange(studentId: number, value: string): void {
    this.updateRolloverOverride(studentId, { sectionId: Number(value) || null });
  }

  private updateRolloverOverride(studentId: number, changes: Partial<RolloverOverrideInput>): void {
    this.rolloverOverrides.update((overrides) => ({
      ...overrides,
      [studentId]: { ...overrides[studentId], ...changes },
    }));
  }

  protected async commitRollover(): Promise<void> {
    if (!this.rolloverYearId()) {
      return;
    }

    const year = this.academicYears().find((item) => item.id === this.rolloverYearId());
    const overrides = Object.values(this.rolloverOverrides());
    const promotable = overrides.filter((row) => !row.skip && row.gradeId !== null).length;
    if (!(await this.confirmDialog.confirm(`Commit rollover for ${year?.name ?? 'this academic year'}? ${promotable} active student records will be promoted.`))) return;

    this.rolloverLoading.set(true);
    this.rolloverError.set('');
    this.rolloverMessage.set('');

    this.academicStructure.commitRollover(this.rolloverYearId(), this.rolloverForce(), overrides).subscribe({
      next: (result) => {
        this.rolloverLoading.set(false);
        this.rolloverPreview.set(null);
        this.rolloverMessage.set(`Promoted ${result.promoted} student(s); skipped ${result.skipped}.`);
        this.academicStructure.getRolloverRuns().subscribe((runs) => this.rolloverRuns.set(runs));
      },
      error: (err: HttpErrorResponse) => {
        this.rolloverLoading.set(false);
        this.rolloverError.set(err.error?.message ?? 'Unable to commit the rollover.');
      },
    });
  }

  protected downloadRolloverRun(run: RolloverRun): void {
    this.academicStructure.downloadRolloverRun(run.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `rollover-${run.academicYearName}-${run.id}.csv`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.rolloverError.set('Unable to download the rollover run.'),
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

  protected dependencySummary(resourceType: SetupResourceType, resourceId: number): string {
    const dependency = this.dependencies().find((item) => item.resourceType === resourceType && item.resourceId === resourceId);
    if (!dependency || dependency.totalCount === 0) return 'No linked records.';
    const parts = [
      dependency.studentCount ? `${dependency.studentCount} students` : '',
      dependency.sectionCount ? `${dependency.sectionCount} sections` : '',
      dependency.assessmentCount ? `${dependency.assessmentCount} assessments` : '',
      dependency.contentCount ? `${dependency.contentCount} content records` : '',
      dependency.assignmentCount ? `${dependency.assignmentCount} assignments` : '',
      dependency.issuedReportCount ? `${dependency.issuedReportCount} issued reports` : '',
    ].filter(Boolean);
    return `Linked: ${parts.join(', ')}.`;
  }
}
