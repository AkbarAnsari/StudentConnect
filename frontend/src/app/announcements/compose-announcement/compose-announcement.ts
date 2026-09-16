import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { AuthService } from '../../auth/auth.service';
import { Grade, Section } from '../../students/student.model';
import { TeacherSubjectAssignmentService } from '../../teachers/teacher-subject-assignment.service';
import { Announcement } from '../announcement.model';
import { AnnouncementService } from '../announcement.service';
import { validateFile } from '../../shared/file-validation.util';
import { triggerBlobDownload } from '../../shared/download.util';

@Component({
  selector: 'app-compose-announcement',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './compose-announcement.html',
  styleUrl: './compose-announcement.css',
})
export class ComposeAnnouncement {
  private readonly fb = inject(FormBuilder);
  private readonly announcementService = inject(AnnouncementService);
  private readonly academicStructure = inject(AcademicStructureService);
  private readonly assignmentService = inject(TeacherSubjectAssignmentService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly authService = inject(AuthService);

  protected readonly grades = signal<Grade[]>([]);
  protected readonly sections = signal<Section[]>([]);
  protected readonly isAdmin = computed(() => this.authService.role() === 'Admin');

  protected readonly submitting = signal(false);
  protected readonly successMessage = signal('');
  protected readonly errorMessage = signal('');
  protected readonly recent = signal<Announcement[]>([]);
  protected readonly recentLoading = signal(true);
  protected readonly recentError = signal('');
  protected readonly audiencePreview = signal<{ studentCount: number; recipientCount: number } | null>(null);
  protected readonly previewLoading = signal(false);
  protected readonly previewError = signal('');
  protected readonly statusFilter = signal('All');
  protected readonly actionId = signal<number | null>(null);
  protected readonly attachment = signal<File | null>(null);
  protected readonly attachmentError = signal('');
  protected readonly editingId = signal<number | null>(null);
  protected readonly filteredRecent = computed(() => this.recent().filter((item) =>
    this.statusFilter() === 'All' || item.status === this.statusFilter(),
  ));

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    body: ['', Validators.required],
    scopeType: [this.authService.role() === 'Admin' ? 'School' : 'Section', Validators.required],
    scopeGradeId: [0],
    scopeSectionId: [0],
    priority: ['Normal' as 'Normal' | 'Important' | 'Urgent'],
    deliveryMode: ['Sent' as 'Draft' | 'Scheduled' | 'Sent'],
    scheduledFor: [''],
    expiresAt: [''],
  });

  constructor() {
    this.loadRecent();
    if (this.authService.role() === 'Admin') this.previewAudience();
    if (this.authService.role() === 'Admin') {
      this.academicStructure.getGrades().subscribe((grades) => this.grades.set(grades));
      this.academicStructure.getSections().subscribe((sections) => this.sections.set(sections));
    } else {
      this.assignmentService.getMine().subscribe({
        next: (assignments) => {
          const sections = new Map<number, Section>();
          for (const assignment of assignments) {
            sections.set(assignment.sectionId, {
              id: assignment.sectionId,
              gradeId: assignment.gradeId,
              gradeName: assignment.gradeName,
              name: assignment.sectionName,
              homeroomTeacherId: null,
              homeroomTeacherName: null,
              isArchived: false,
            });
          }
          this.sections.set([...sections.values()]);
        },
        error: () => this.errorMessage.set('Unable to load assigned sections.'),
      });
    }
  }

  private loadRecent(): void {
    this.recentLoading.set(true);
    this.recentError.set('');
    this.announcementService.getRecent(100).subscribe({
      next: (recent) => { this.recent.set(recent); this.recentLoading.set(false); },
      error: () => { this.recentError.set('Unable to load announcement history.'); this.recentLoading.set(false); },
    });
  }

  protected previewAudience(): void {
    const raw = this.form.getRawValue();
    if ((raw.scopeType === 'Grade' && !raw.scopeGradeId) || (raw.scopeType === 'Section' && !raw.scopeSectionId)) {
      this.audiencePreview.set(null); return;
    }
    this.previewLoading.set(true); this.previewError.set('');
    this.announcementService.previewAudience(
      raw.scopeType as Announcement['scopeType'],
      raw.scopeType === 'Grade' ? raw.scopeGradeId : undefined,
      raw.scopeType === 'Section' ? raw.scopeSectionId : undefined,
    ).subscribe({
      next: (preview) => { this.audiencePreview.set(preview); this.previewLoading.set(false); },
      error: (error: HttpErrorResponse) => { this.audiencePreview.set(null); this.previewLoading.set(false); this.previewError.set(error.error?.message ?? 'Unable to preview this audience.'); },
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    if (raw.deliveryMode === 'Scheduled' && !raw.scheduledFor) {
      this.errorMessage.set('Choose a future delivery date and time.'); return;
    }
    this.submitting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    const editingId = this.editingId();
    const request = editingId
      ? this.announcementService.updateDraft(editingId, raw.title, raw.body, raw.priority, raw.expiresAt ? new Date(raw.expiresAt).toISOString() : null)
      : this.announcementService.create({
        title: raw.title,
        body: raw.body,
        scopeType: raw.scopeType as Announcement['scopeType'],
        scopeGradeId: raw.scopeType === 'Grade' ? raw.scopeGradeId : undefined,
        scopeSectionId: raw.scopeType === 'Section' ? raw.scopeSectionId : undefined,
        status: raw.deliveryMode,
        priority: raw.priority,
        scheduledForUtc: raw.deliveryMode === 'Scheduled' ? new Date(raw.scheduledFor).toISOString() : null,
        expiresAtUtc: raw.expiresAt ? new Date(raw.expiresAt).toISOString() : null,
      }, this.attachment());
    request.subscribe({
        next: () => {
          this.submitting.set(false);
          this.successMessage.set(raw.deliveryMode === 'Draft' ? 'Announcement saved as Draft.' : raw.deliveryMode === 'Scheduled' ? 'Announcement scheduled.' : 'Announcement sent.');
          this.form.reset({
            title: '',
            body: '',
            scopeType: this.isAdmin() ? 'School' : 'Section',
            scopeGradeId: 0,
            scopeSectionId: 0,
            priority: 'Normal',
            deliveryMode: 'Sent',
            scheduledFor: '',
            expiresAt: '',
          });
          this.audiencePreview.set(null);
          this.attachment.set(null);
          this.editingId.set(null);
          if (this.isAdmin()) this.previewAudience();
          this.loadRecent();
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.errorMessage.set(err.error?.message ?? 'Unable to send the announcement.');
        },
      });
  }

  protected onAttachment(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    const error = validateFile(file, ['.pdf', '.docx', '.png', '.jpg', '.jpeg'], 10 * 1024 * 1024);
    this.attachmentError.set(error ?? ''); this.attachment.set(error ? null : file);
  }

  protected editDraft(item: Announcement): void {
    this.editingId.set(item.id);
    this.form.patchValue({ title:item.title, body:item.body, priority:item.priority, deliveryMode:'Draft', expiresAt:item.expiresAtUtc ? item.expiresAtUtc.slice(0,16) : '' });
  }

  protected retry(item: Announcement): void { this.actionId.set(item.id); this.announcementService.retry(item.id).subscribe({next:()=>{this.actionId.set(null);this.successMessage.set('Announcement queued for retry.');this.loadRecent();},error:(err:HttpErrorResponse)=>{this.actionId.set(null);this.errorMessage.set(err.error?.message??'Unable to retry announcement.');}}); }
  protected downloadAttachment(item: Announcement): void { this.announcementService.downloadAttachment(item.id).subscribe(blob=>triggerBlobDownload(blob,item.attachmentFileName??'announcement-attachment')); }

  protected async changeLifecycle(item: Announcement, action: 'publish' | 'withdraw'): Promise<void> {
    const prompt = action === 'publish'
      ? `Send ${item.title} now to its current audience?`
      : `Withdraw ${item.title}? Existing recipient history will remain auditable.`;
    if (!(await this.confirmDialog.confirm(prompt))) return;
    this.actionId.set(item.id); this.errorMessage.set(''); this.successMessage.set('');
    const request = action === 'publish' ? this.announcementService.publish(item.id) : this.announcementService.withdraw(item.id);
    request.subscribe({
      next: () => { this.actionId.set(null); this.successMessage.set(action === 'publish' ? 'Announcement sent.' : 'Announcement withdrawn.'); this.loadRecent(); },
      error: (error: HttpErrorResponse) => { this.actionId.set(null); this.errorMessage.set(error.error?.message ?? 'Unable to update the announcement.'); },
    });
  }
}
