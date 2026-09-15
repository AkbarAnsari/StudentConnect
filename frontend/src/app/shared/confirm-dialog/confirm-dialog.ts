import { Component, effect, ElementRef, HostListener, inject, viewChild } from '@angular/core';
import { ConfirmDialogService } from './confirm-dialog.service';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.css',
})
export class ConfirmDialog {
  protected readonly service = inject(ConfirmDialogService);
  private readonly cancelButton = viewChild<ElementRef<HTMLButtonElement>>('cancelButton');
  private readonly confirmButton = viewChild<ElementRef<HTMLButtonElement>>('confirmButton');
  private lastFocused: HTMLElement | null = null;

  constructor() {
    // This component is rendered once globally, so capture the caller's focus fresh on
    // each new request (not just once in the constructor) and restore it once closed.
    effect(() => {
      if (this.service.request()) {
        this.lastFocused = document.activeElement as HTMLElement | null;
      }
    });
  }

  protected onConfirm(): void {
    this.service.respond(true);
    this.lastFocused?.focus();
  }

  protected onCancel(): void {
    this.service.respond(false);
    this.lastFocused?.focus();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.service.request()) {
      this.onCancel();
    }
  }

  @HostListener('keydown', ['$event'])
  protected onTab(event: KeyboardEvent): void {
    if (event.key !== 'Tab') {
      return;
    }
    const cancel = this.cancelButton()?.nativeElement;
    const confirm = this.confirmButton()?.nativeElement;
    if (!cancel || !confirm) {
      return;
    }
    // Keeps focus cycling between the two dialog buttons instead of escaping to the page behind it.
    if (event.shiftKey && document.activeElement === cancel) {
      event.preventDefault();
      confirm.focus();
    } else if (!event.shiftKey && document.activeElement === confirm) {
      event.preventDefault();
      cancel.focus();
    }
  }
}
