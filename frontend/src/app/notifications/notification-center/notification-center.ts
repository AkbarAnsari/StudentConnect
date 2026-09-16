import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AppNotification } from '../notification.model';
import { NotificationService } from '../notification.service';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-notification-center',
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './notification-center.html',
  styleUrl: './notification-center.css',
})
export class NotificationCenter {
  protected readonly notificationService = inject(NotificationService);
  protected readonly items = signal<AppNotification[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly unreadOnly = signal(false);
  protected readonly typeFilter = signal('');
  protected readonly loading = signal(true);
  protected readonly error = signal('');
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));

  constructor() { this.load(); this.notificationService.loadPreference(); }

  protected applyFilters(): void { this.page.set(1); this.load(); }
  protected previous(): void { if (this.page() > 1) { this.page.update(value => value - 1); this.load(); } }
  protected next(): void { if (this.page() < this.totalPages()) { this.page.update(value => value + 1); this.load(); } }
  protected markRead(item: AppNotification): void { if (!item.isRead) this.notificationService.markAsReadRequest(item.id).subscribe(() => { this.notificationService.refresh(); this.load(); }); }
  protected markAllRead(): void { this.notificationService.markAllAsReadRequest().subscribe(() => { this.notificationService.refresh(); this.load(); }); }
  protected updatePreference(field:'digestEnabled'|'emailChannelEnabled'|'inAppChannelEnabled'|'quietHoursStart'|'quietHoursEnd',value:boolean|string|null):void{this.notificationService.updatePreference({...this.notificationService.preference(),[field]:value});}

  private load(): void {
    this.loading.set(true); this.error.set('');
    this.notificationService.getHistory(this.page(), PAGE_SIZE, this.unreadOnly(), this.typeFilter() || undefined).subscribe({
      next: result => { this.items.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: () => { this.error.set('Unable to load notification history.'); this.loading.set(false); },
    });
  }
}
