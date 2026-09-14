import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { Announcement } from '../announcement.model';
import { AnnouncementService } from '../announcement.service';

@Component({
  selector: 'app-compose-announcement',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './compose-announcement.html',
  styleUrl: './compose-announcement.css',
})
export class ComposeAnnouncement {
  private readonly fb = inject(FormBuilder);
  private readonly announcementService = inject(AnnouncementService);
  private readonly academicStructure = inject(AcademicStructureService);

  protected readonly grades = toSignal(this.academicStructure.getGrades(), { initialValue: [] });
  protected readonly sections = toSignal(this.academicStructure.getSections(), { initialValue: [] });

  protected readonly submitting = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');
  protected readonly recent = signal<Announcement[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    body: ['', Validators.required],
    scopeType: ['School', Validators.required],
    scopeGradeId: [0],
    scopeSectionId: [0],
  });

  constructor() {
    this.loadRecent();
  }

  private loadRecent(): void {
    this.announcementService.getRecent().subscribe((recent) => this.recent.set(recent));
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.submitting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    this.announcementService
      .create({
        title: raw.title,
        body: raw.body,
        scopeType: raw.scopeType as Announcement['scopeType'],
        scopeGradeId: raw.scopeType === 'Grade' ? raw.scopeGradeId : undefined,
        scopeSectionId: raw.scopeType === 'Section' ? raw.scopeSectionId : undefined,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.successMessage.set('Announcement sent.');
          this.form.reset({ title: '', body: '', scopeType: 'School', scopeGradeId: 0, scopeSectionId: 0 });
          this.loadRecent();
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(err.error?.message ?? 'Unable to send the announcement.');
        },
      });
  }
}
