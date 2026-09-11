import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { environment } from '../../environments/environment';
import { AppNotification } from './notification.model';

// Polls for new notifications every 30s while the user is logged in — a lightweight
// stand-in for push delivery, good enough for an in-app "Local" outbox (see
// backend Notifications/INotificationService.cs for the delivery-side design).
const POLL_INTERVAL_MS = 30_000;

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/notifications`;

  readonly notifications = signal<AppNotification[]>([]);
  readonly unreadCount = signal(0);

  private pollHandle: ReturnType<typeof setInterval> | null = null;

  startPolling(): void {
    if (this.pollHandle) {
      return;
    }
    this.refresh();
    this.pollHandle = setInterval(() => this.refresh(), POLL_INTERVAL_MS);
  }

  stopPolling(): void {
    if (this.pollHandle) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
    this.notifications.set([]);
    this.unreadCount.set(0);
  }

  refresh(): void {
    this.http.get<AppNotification[]>(this.baseUrl).subscribe({
      next: (list) => this.notifications.set(list),
      error: () => undefined,
    });
    this.http.get<number>(`${this.baseUrl}/unread-count`).subscribe({
      next: (count) => this.unreadCount.set(count),
      error: () => undefined,
    });
  }

  markAsRead(id: number): void {
    this.http.post<void>(`${this.baseUrl}/${id}/read`, {}).subscribe(() => this.refresh());
  }

  markAllAsRead(): void {
    this.http.post<void>(`${this.baseUrl}/read-all`, {}).subscribe(() => this.refresh());
  }
}
