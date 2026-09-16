import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { triggerBlobDownload } from '../../shared/download.util';
import { SubmissionOverview } from '../homework.model';
import { HomeworkSubmissionService } from '../homework.service';

@Component({
  selector: 'app-homework-submissions',
  imports: [RouterLink],
  templateUrl: './homework-submissions.html',
  styleUrl: './homework-submissions.css',
})
export class HomeworkSubmissions {
  private readonly route = inject(ActivatedRoute);
  private readonly submissionService = inject(HomeworkSubmissionService);

  protected readonly homeworkId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly submissions = signal<SubmissionOverview[]>([]);
  protected readonly feedbackDrafts = signal<Record<number, string>>({});
  protected readonly savingFeedbackFor = signal<number | null>(null);
  protected readonly feedbackStatus = signal<Record<number, 'saved' | 'error'>>({});
  protected readonly reviewFilter = signal<'all' | 'needs-review' | 'reviewed' | 'pending'>('all');
  protected readonly downloadingPackage = signal(false);
  protected readonly packageError = signal(false);
  protected readonly submittedCount = computed(() => this.submissions().filter((row) => row.status !== 'Not Submitted').length);
  protected readonly pendingCount = computed(() => this.submissions().length - this.submittedCount());
  protected readonly filteredSubmissions = computed(() => this.submissions().filter((row) => {
    const filter = this.reviewFilter();
    const hasFeedback = Boolean(row.teacherFeedback?.trim());
    if (filter === 'pending') return row.status === 'Not Submitted';
    if (filter === 'needs-review') return row.status !== 'Not Submitted' && !hasFeedback;
    if (filter === 'reviewed') return row.status !== 'Not Submitted' && hasFeedback;
    return true;
  }));
  protected readonly feedbackDirty = computed(() => this.submissions().some((row) =>
    row.submissionId !== null && (this.feedbackDrafts()[row.submissionId] ?? '') !== (row.teacherFeedback ?? ''),
  ));

  constructor() {
    this.submissionService.getSubmissionsForHomework(this.homeworkId).subscribe({
      next: (submissions) => {
        this.submissions.set(submissions);
        const drafts: Record<number, string> = {};
        for (const row of submissions) {
          if (row.submissionId !== null) {
            drafts[row.submissionId] = row.teacherFeedback ?? '';
          }
        }
        this.feedbackDrafts.set(drafts);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  protected onFeedbackChange(submissionId: number, value: string): void {
    this.feedbackDrafts.update((drafts) => ({ ...drafts, [submissionId]: value }));
    this.feedbackStatus.update((statuses) => {
      const updated = { ...statuses };
      delete updated[submissionId];
      return updated;
    });
  }

  protected saveFeedback(submissionId: number): void {
    const feedback = this.feedbackDrafts()[submissionId] ?? '';
    this.savingFeedbackFor.set(submissionId);

    this.submissionService.addFeedback(submissionId, feedback).subscribe({
      next: () => {
        this.savingFeedbackFor.set(null);
        this.feedbackStatus.update((statuses) => ({ ...statuses, [submissionId]: 'saved' }));
        this.submissions.update((rows) =>
          rows.map((row) => (row.submissionId === submissionId ? { ...row, teacherFeedback: feedback } : row)),
        );
      },
      error: () => {
        this.savingFeedbackFor.set(null);
        this.feedbackStatus.update((statuses) => ({ ...statuses, [submissionId]: 'error' }));
      },
    });
  }

  protected download(submissionId: number): void {
    this.submissionService.downloadFile(submissionId).subscribe((blob) => triggerBlobDownload(blob, `submission-${submissionId}`));
  }

  protected downloadPackage(): void {
    this.downloadingPackage.set(true);
    this.packageError.set(false);
    this.submissionService.downloadHomeworkSubmissions(this.homeworkId).subscribe({
      next: (blob) => {
        triggerBlobDownload(blob, `homework-${this.homeworkId}-submissions.zip`);
        this.downloadingPackage.set(false);
      },
      error: () => {
        this.downloadingPackage.set(false);
        this.packageError.set(true);
      },
    });
  }

  hasUnsavedChanges(): boolean {
    return this.feedbackDirty() && this.savingFeedbackFor() === null;
  }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) event.preventDefault();
  }
}
