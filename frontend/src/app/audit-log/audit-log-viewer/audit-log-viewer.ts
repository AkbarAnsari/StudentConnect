import { DatePipe } from '@angular/common';
import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { triggerBlobDownload } from '../../shared/download.util';
import { AuditLog, AuditLogFilters } from '../audit-log.model';
import { AuditLogService } from '../audit-log.service';

@Component({
  selector: 'app-audit-log-viewer',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './audit-log-viewer.html',
  styleUrl: './audit-log-viewer.css',
})
export class AuditLogViewer {
  private readonly auditLogService = inject(AuditLogService);
  protected readonly entityTypes = ['Student', 'Attendance', 'Mark', 'Assessment', 'Homework', 'LearningMaterial', 'Activity', 'Announcement', 'Teacher', 'Parent', 'User', 'ReportCard', 'EmailOutbox'];
  protected readonly actions = ['Create', 'Update', 'Delete', 'Publish', 'Reopen', 'Close', 'Archive', 'Issue', 'Retry', 'BulkRetry', 'Deactivate', 'Reactivate'];
  protected readonly entityType = signal('');
  protected readonly action = signal('');
  protected readonly username = signal('');
  protected readonly query = signal('');
  protected readonly fromDate = signal('');
  protected readonly toDate = signal('');
  protected readonly page = signal(1);
  protected readonly pageSize = signal(25);
  protected readonly totalCount = signal(0);
  protected readonly logs = signal<AuditLog[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal('');
  protected readonly exportError = signal('');
  protected readonly selected = signal<AuditLog | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize())));
  protected readonly resultStart = computed(() => this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1);
  protected readonly resultEnd = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  constructor() { this.reload(); }

  protected applyFilters(): void { this.page.set(1); this.reload(); }

  protected resetFilters(): void {
    this.entityType.set(''); this.action.set(''); this.username.set(''); this.query.set(''); this.fromDate.set(''); this.toDate.set(''); this.page.set(1);
    this.reload();
  }

  protected goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) return;
    this.page.set(page); this.reload();
  }

  protected openDetail(id: number): void {
    this.detailLoading.set(true);
    this.auditLogService.getById(id).subscribe({
      next: (item) => { this.selected.set(item); this.detailLoading.set(false); },
      error: () => { this.loadError.set('The audit entry could not be opened.'); this.detailLoading.set(false); },
    });
  }

  protected closeDetail(): void { this.selected.set(null); }

  @HostListener('document:keydown.escape')
  protected onEscape(): void { this.closeDetail(); }

  protected exportCsv(): void {
    this.exportError.set('');
    this.auditLogService.export(this.filters()).subscribe({
      next: (blob) => triggerBlobDownload(blob, `audit-log-${new Date().toISOString().slice(0, 10)}.csv`),
      error: () => this.exportError.set('The audit export could not be downloaded.'),
    });
  }

  protected reload(): void {
    this.loading.set(true); this.loadError.set('');
    this.auditLogService.getPaged(this.filters()).subscribe({
      next: (result) => { this.logs.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: (err) => { this.loadError.set(err?.error?.message ?? 'Audit entries could not be loaded.'); this.loading.set(false); },
    });
  }

  private filters(): AuditLogFilters {
    return {
      page: this.page(), pageSize: this.pageSize(), entityType: this.entityType(), action: this.action(), username: this.username(), query: this.query(),
      fromUtc: this.fromDate() ? new Date(`${this.fromDate()}T00:00:00Z`).toISOString() : '',
      toUtc: this.toDate() ? new Date(`${this.toDate()}T00:00:00Z`).toISOString() : '',
    };
  }
}
