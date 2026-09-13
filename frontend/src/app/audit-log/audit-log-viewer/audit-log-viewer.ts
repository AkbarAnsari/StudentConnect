import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuditLog } from '../audit-log.model';
import { AuditLogService } from '../audit-log.service';

@Component({
  selector: 'app-audit-log-viewer',
  imports: [RouterLink, DatePipe],
  templateUrl: './audit-log-viewer.html',
  styleUrl: './audit-log-viewer.css',
})
export class AuditLogViewer {
  private readonly auditLogService = inject(AuditLogService);

  protected readonly entityTypes = ['Student', 'Mark', 'Attendance', 'Teacher', 'Parent'];
  protected readonly selectedEntityType = signal('');
  protected readonly logs = signal<AuditLog[]>([]);

  constructor() {
    this.reload();
  }

  protected onFilterChange(entityType: string): void {
    this.selectedEntityType.set(entityType);
    this.reload();
  }

  private reload(): void {
    this.auditLogService.getRecent(this.selectedEntityType() || undefined).subscribe((logs) => this.logs.set(logs));
  }
}
