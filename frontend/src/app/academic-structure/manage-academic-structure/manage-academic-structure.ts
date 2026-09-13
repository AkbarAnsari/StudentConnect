import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject } from '../../attendance/attendance.model';
import { Grade, Section } from '../../students/student.model';
import { Teacher } from '../../teachers/teacher.model';
import { TeacherService } from '../../teachers/teacher.service';
import { AcademicStructureService } from '../academic-structure.service';
import { AcademicYear, Term } from '../academic-structure.model';

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

  protected readonly grades = signal<Grade[]>([]);
  protected readonly subjects = signal<Subject[]>([]);
  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly academicYears = signal<AcademicYear[]>([]);
  protected readonly terms = signal<Term[]>([]);
  protected readonly sections = signal<Section[]>([]);
  protected readonly errorMessage = signal('');

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

  constructor() {
    this.reloadAll();
  }

  private reloadAll(): void {
    this.academicStructure.getGrades().subscribe((grades) => this.grades.set(grades));
    this.academicStructure.getSubjects().subscribe((subjects) => this.subjects.set(subjects));
    this.teacherService.getTeachers().subscribe((teachers) => this.teachers.set(teachers));
    this.academicStructure.getAcademicYears().subscribe((years) => this.academicYears.set(years));
    this.academicStructure.getTerms().subscribe((terms) => this.terms.set(terms));
    this.academicStructure.getSections().subscribe((sections) => this.sections.set(sections));
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
}
