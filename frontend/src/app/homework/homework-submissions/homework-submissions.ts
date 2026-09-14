import { Component, inject, signal } from '@angular/core';
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
  }

  protected saveFeedback(submissionId: number): void {
    const feedback = this.feedbackDrafts()[submissionId] ?? '';
    this.savingFeedbackFor.set(submissionId);

    this.submissionService.addFeedback(submissionId, feedback).subscribe({
      next: () => {
        this.savingFeedbackFor.set(null);
        this.submissions.update((rows) =>
          rows.map((row) => (row.submissionId === submissionId ? { ...row, teacherFeedback: feedback } : row)),
        );
      },
      error: () => this.savingFeedbackFor.set(null),
    });
  }

  protected download(submissionId: number): void {
    this.submissionService.downloadFile(submissionId).subscribe((blob) => triggerBlobDownload(blob, `submission-${submissionId}`));
  }
}
