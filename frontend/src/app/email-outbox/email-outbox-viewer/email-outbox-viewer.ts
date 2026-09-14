import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmailOutboxEntry } from '../email-outbox.model';
import { EmailOutboxService } from '../email-outbox.service';

@Component({
  selector: 'app-email-outbox-viewer',
  imports: [RouterLink, DatePipe],
  templateUrl: './email-outbox-viewer.html',
  styleUrl: './email-outbox-viewer.css',
})
export class EmailOutboxViewer {
  private readonly emailOutboxService = inject(EmailOutboxService);

  protected readonly entries = signal<EmailOutboxEntry[]>([]);
  protected readonly retryingId = signal<number | null>(null);
  protected readonly retryError = signal('');

  constructor() {
    this.emailOutboxService.getRecent().subscribe((entries) => this.entries.set(entries));
  }

  protected status(entry: EmailOutboxEntry): 'sent' | 'failed' | 'pending' {
    if (entry.sentAtUtc) {
      return 'sent';
    }
    return entry.lastError ? 'failed' : 'pending';
  }

  protected retry(id: number): void {
    this.retryingId.set(id);
    this.retryError.set('');
    this.emailOutboxService.retry(id).subscribe({
      next: () => {
        this.retryingId.set(null);
        this.emailOutboxService.getRecent().subscribe((entries) => this.entries.set(entries));
      },
      error: () => {
        this.retryingId.set(null);
        this.retryError.set('Unable to retry this email. Please try again.');
      },
    });
  }
}
