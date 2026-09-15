import { Injectable, signal } from '@angular/core';

interface ConfirmRequest {
  message: string;
  resolve: (confirmed: boolean) => void;
}

// A single global confirm-dialog instance (rendered once in app.html) replaces every
// native confirm()/alert() call site with a styled, accessible, focus-managed dialog.
@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  readonly request = signal<ConfirmRequest | null>(null);

  confirm(message: string): Promise<boolean> {
    return new Promise((resolve) => {
      this.request.set({ message, resolve });
    });
  }

  respond(confirmed: boolean): void {
    this.request()?.resolve(confirmed);
    this.request.set(null);
  }
}
