import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { ConfirmDialogService } from './confirm-dialog/confirm-dialog.service';

export interface PendingChangesComponent {
  hasUnsavedChanges(): boolean;
}

export const pendingChangesGuard: CanDeactivateFn<PendingChangesComponent> = (component) =>
  !component.hasUnsavedChanges() ||
  inject(ConfirmDialogService).confirm('Leave this page? Your unsaved changes will be lost.');
