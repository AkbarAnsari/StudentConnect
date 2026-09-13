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

  constructor() {
    this.emailOutboxService.getRecent().subscribe((entries) => this.entries.set(entries));
  }

  protected status(entry: EmailOutboxEntry): 'sent' | 'failed' | 'pending' {
    if (entry.sentAtUtc) {
      return 'sent';
    }
    return entry.lastError ? 'failed' : 'pending';
  }
}
