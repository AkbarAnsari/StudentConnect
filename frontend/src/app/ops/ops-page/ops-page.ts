import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OpsInfo } from '../ops.model';
import { OpsService } from '../ops.service';

@Component({
  selector: 'app-ops-page',
  imports: [RouterLink],
  templateUrl: './ops-page.html',
  styleUrl: './ops-page.css',
})
export class OpsPage {
  private readonly opsService = inject(OpsService);

  protected readonly info = signal<OpsInfo | null>(null);
  protected readonly errorMessage = signal('');

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.errorMessage.set('');
    this.opsService.getInfo().subscribe({
      next: (info) => this.info.set(info),
      error: () => this.errorMessage.set('Unable to load operations info. Please check whether the StudentConnect API is running.'),
    });
  }

  protected formatUptime(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours}h ${minutes}m`;
  }
}
