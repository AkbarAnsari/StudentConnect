import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { Parent } from '../../parents/parent.model';
import { ParentService } from '../../parents/parent.service';
import { StudentService } from '../student.service';

@Component({
  selector: 'app-add-student',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './add-student.html',
  styleUrl: './add-student.css',
})
export class AddStudent {
  private readonly fb = inject(FormBuilder);
  private readonly studentService = inject(StudentService);
  private readonly router = inject(Router);
  private readonly parentService = inject(ParentService);

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly subjects = toSignal(this.studentService.getSubjects(), { initialValue: [] });
  protected readonly selectedSubjectIds = signal<number[]>([]);
  protected readonly subjectError = signal('');
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly parents = signal<Parent[]>([]);
  protected readonly curriculumNotice = signal('Select a Grade and Section to apply curriculum defaults.');
  private saved = false;
  private subjectsTouched = false;
  private curriculumRequestId = 0;

  protected readonly form = this.fb.nonNullable.group({
    studentId: ['', Validators.required],
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    gradeId: [0, [Validators.required, Validators.min(1)]],
    sectionId: [0, [Validators.required, Validators.min(1)]],
    dateOfBirth: [''],
    phone: [''],
    address: [''],
    createLogin: [false],
    username: [''],
    guardianParentUserId: [0],
    guardianRelationshipType: ['Guardian'],
    guardianIsPrimaryContact: [false],
  });

  // Refetched every time the selected grade changes — a Section only makes sense
  // scoped to a Grade, so the dropdown options must follow the Grade selection.
  protected readonly sections = toSignal(
    this.form.controls.gradeId.valueChanges.pipe(
      switchMap((gradeId) => (gradeId > 0 ? this.studentService.getSectionsByGrade(gradeId) : of([]))),
    ),
    { initialValue: [] },
  );

  constructor() {
    // A section from the previous grade would no longer be valid once the grade changes.
    this.form.controls.gradeId.valueChanges.subscribe(() => {
      this.form.controls.sectionId.setValue(0);
      this.subjectsTouched = false;
      this.selectedSubjectIds.set([]);
      this.loadCurriculumDefaults();
    });
    this.form.controls.sectionId.valueChanges.subscribe(() => {
      this.subjectsTouched = false;
      this.loadCurriculumDefaults();
    });
    this.form.controls.createLogin.valueChanges.subscribe((createLogin) => {
      this.form.controls.username.setValidators(createLogin ? [Validators.required] : []);
      this.form.controls.username.updateValueAndValidity();
    });
    this.parentService.getParents().subscribe({ next: parents => this.parents.set(parents.filter(parent => parent.status === 'Active')) });
  }

  private loadCurriculumDefaults(): void {
    const gradeId = this.form.controls.gradeId.value;
    const sectionId = this.form.controls.sectionId.value;
    if (!gradeId) return;
    const requestId = ++this.curriculumRequestId;
    this.curriculumNotice.set('Loading curriculum defaults...');
    this.studentService.getResolvedCurriculumDefaults(gradeId, sectionId || undefined).subscribe({
      next: defaults => {
        if (requestId !== this.curriculumRequestId || this.subjectsTouched) return;
        this.selectedSubjectIds.set(defaults.subjectIds);
        this.curriculumNotice.set(defaults.subjectIds.length > 0 ? `${defaults.subjectIds.length} default subject(s) applied.` : 'No defaults configured for this class. Select subjects manually.');
      },
      error: () => { if (requestId === this.curriculumRequestId) this.curriculumNotice.set('Unable to load defaults. Select subjects manually.'); },
    });
  }

  protected toggleSubject(subjectId: number, selected: boolean): void {
    this.subjectsTouched = true;
    this.selectedSubjectIds.update((current) =>
      selected ? [...new Set([...current, subjectId])] : current.filter((id) => id !== subjectId),
    );
    this.subjectError.set('');
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.selectedSubjectIds().length === 0) {
      this.subjectError.set('Select at least one subject.');
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const { dateOfBirth, phone, address, createLogin, username, guardianParentUserId, guardianRelationshipType, guardianIsPrimaryContact, ...rest } = this.form.getRawValue();
    this.studentService
      .createStudent({
        ...rest,
        dateOfBirth: dateOfBirth || null,
        phone: phone || null,
        address: address || null,
        subjectIds: this.selectedSubjectIds(),
        createLogin,
        username: createLogin ? username : null,
        guardianParentUserId: guardianParentUserId || null,
        guardianRelationshipType,
        guardianIsPrimaryContact,
      })
      .subscribe({
        next: (student) => {
          this.saved = true;
          this.router.navigate(['/students', student.id]);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(err.error?.message ?? 'Unable to save the student. Please try again.');
        },
      });
  }

  hasUnsavedChanges(): boolean {
    return (this.form.dirty || this.subjectsTouched) && !this.saved && !this.submitting();
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
