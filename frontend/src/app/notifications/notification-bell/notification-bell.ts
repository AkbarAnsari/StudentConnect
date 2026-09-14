import { DatePipe } from '@angular/common';
import { Component, HostListener, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { NotificationService } from '../notification.service';

@Component({
  selector: 'app-notification-bell',
  imports: [DatePipe],
  templateUrl: './notification-bell.html',
  styleUrl: './notification-bell.css',
})
export class NotificationBell implements OnInit, OnDestroy {
  protected readonly notificationService = inject(NotificationService);
  protected readonly open = signal(false);

  ngOnInit(): void {
    this.notificationService.startPolling();
  }

  ngOnDestroy(): void {
    this.notificationService.stopPolling();
  }

  protected toggleOpen(): void {
    this.open.update((value) => !value);
  }

  protected markAsRead(id: number): void {
    this.notificationService.markAsRead(id);
  }

  protected markAllAsRead(): void {
    this.notificationService.markAllAsRead();
  }

  protected toggleDigest(checked: boolean): void {
    this.notificationService.setDigestEnabled(checked);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!(event.target as HTMLElement).closest('.notification-bell')) {
      this.open.set(false);
    }
  }
}
