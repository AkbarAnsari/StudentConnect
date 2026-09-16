import { DatePipe } from '@angular/common';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { EmailOutboxEntry, EmailOutboxFilters, EmailOutboxStatus } from '../email-outbox.model';
import { EmailOutboxService } from '../email-outbox.service';

@Component({
  selector: 'app-email-outbox-viewer',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './email-outbox-viewer.html',
  styleUrl: './email-outbox-viewer.css',
})
export class EmailOutboxViewer {
  private readonly emailOutboxService = inject(EmailOutboxService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly entries = signal<EmailOutboxEntry[]>([]);
  protected readonly statusFilter = signal<EmailOutboxStatus>('');
  protected readonly query = signal('');
  protected readonly fromDate = signal('');
  protected readonly toDate = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(true);
  protected readonly loadError = signal('');
  protected readonly actionError = signal('');
  protected readonly actionMessage = signal('');
  protected readonly retrying = signal(false);
  protected readonly selectedIds = signal<ReadonlySet<number>>(new Set());
  protected readonly selected = signal<EmailOutboxEntry | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly resolutionNotes = signal('');
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  protected readonly failedOnPage = computed(() => this.entries().filter((entry) => this.status(entry) === 'failed'));
  protected readonly resultStart = computed(() => this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1);
  protected readonly resultEnd = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  constructor() { this.reload(); }

  protected status(entry: EmailOutboxEntry): 'sent' | 'failed' | 'pending' {
    return entry.sentAtUtc ? 'sent' : entry.lastError ? 'failed' : 'pending';
  }

  protected applyFilters(): void { this.page.set(1); this.selectedIds.set(new Set()); this.reload(); }

  protected resetFilters(): void {
    this.statusFilter.set(''); this.query.set(''); this.fromDate.set(''); this.toDate.set(''); this.page.set(1); this.selectedIds.set(new Set()); this.reload();
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) return;
    this.page.set(page); this.selectedIds.set(new Set()); this.reload();
  }

  protected isSelected(id: number): boolean { return this.selectedIds().has(id); }

  protected toggleSelected(id: number, selected: boolean): void {
    const updated = new Set(this.selectedIds()); selected ? updated.add(id) : updated.delete(id); this.selectedIds.set(updated);
  }

  protected toggleAllFailed(selected: boolean): void {
    this.selectedIds.set(selected ? new Set(this.failedOnPage().map((entry) => entry.id)) : new Set());
  }

  protected async retryOne(entry: EmailOutboxEntry): Promise<void> {
    if (!(await this.confirmDialog.confirm(`Retry the failed email to ${entry.toEmail}?`))) return;
    await this.retryIds([entry.id]);
  }

  protected async retrySelected(): Promise<void> {
    const ids = [...this.selectedIds()];
    if (ids.length === 0 || !(await this.confirmDialog.confirm(`Retry ${ids.length} selected failed ${ids.length === 1 ? 'email' : 'emails'}?`))) return;
    await this.retryIds(ids);
  }

  protected openDetail(id: number): void {
    this.detailLoading.set(true);
    this.emailOutboxService.getById(id).subscribe({
      next: (entry) => { this.selected.set(entry); this.detailLoading.set(false); },
      error: () => { this.actionError.set('The email detail could not be opened.'); this.detailLoading.set(false); },
    });
  }

  protected closeDetail(): void { this.selected.set(null); }
  protected assignToMe(entry:EmailOutboxEntry):void{this.emailOutboxService.assignToMe(entry.id,this.resolutionNotes()).subscribe({next:()=>{this.actionMessage.set('Dead letter assigned.');this.resolutionNotes.set('');this.openDetail(entry.id);this.reload();},error:()=>this.actionError.set('Unable to assign this dead letter.')});}

  @HostListener('document:keydown.escape')
  protected onEscape(): void { this.closeDetail(); }

  protected reload(): void {
    this.loading.set(true); this.loadError.set('');
    this.emailOutboxService.getPaged(this.filters()).subscribe({
      next: (result) => { this.entries.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: (err) => { this.loadError.set(err?.error?.message ?? 'The email outbox could not be loaded.'); this.loading.set(false); },
    });
  }

  private async retryIds(ids: number[]): Promise<void> {
    this.retrying.set(true); this.actionError.set(''); this.actionMessage.set('');
    this.emailOutboxService.retryFailed(ids).subscribe({
      next: (result) => { this.retrying.set(false); this.selectedIds.set(new Set()); this.actionMessage.set(`${result.retried} failed ${result.retried === 1 ? 'email was' : 'emails were'} queued for retry.`); this.reload(); },
      error: (err) => { this.retrying.set(false); this.actionError.set(err?.error?.message ?? 'The selected emails could not be retried.'); },
    });
  }

  private filters(): EmailOutboxFilters {
    return {
      page: this.page(), pageSize: this.pageSize(), status: this.statusFilter(), query: this.query(),
      fromUtc: this.fromDate() ? new Date(`${this.fromDate()}T00:00:00Z`).toISOString() : '',
      toUtc: this.toDate() ? new Date(`${this.toDate()}T00:00:00Z`).toISOString() : '',
    };
  }
}
