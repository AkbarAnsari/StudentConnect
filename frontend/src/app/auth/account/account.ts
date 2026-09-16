import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AccountProfile } from '../auth.model';
import { AccountService } from '../account.service';
import { AuthService } from '../auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';

@Component({
  selector: 'app-account',
  imports: [ReactiveFormsModule, DatePipe],
  templateUrl: './account.html',
  styleUrl: './account.css',
})
export class Account {
  private readonly fb = inject(FormBuilder);
  private readonly accountService = inject(AccountService);
  private readonly authService = inject(AuthService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly router = inject(Router);

  protected readonly profile = signal<AccountProfile | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly message = signal('');
  protected readonly error = signal('');

  protected readonly profileForm = this.fb.nonNullable.group({ email: ['', Validators.email] });
  protected readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', Validators.required],
  });

  constructor() { this.load(); }

  protected saveProfile(): void {
    if (this.profileForm.invalid) { this.profileForm.markAllAsTouched(); return; }
    this.saving.set(true); this.error.set(''); this.message.set('');
    this.accountService.updateAccount(this.profileForm.controls.email.value || null).subscribe({
      next: (profile) => { this.profile.set(profile); this.authService.markEmailUnverified(); this.saving.set(false); this.message.set('Account details updated.'); },
      error: (err: HttpErrorResponse) => { this.saving.set(false); this.error.set(err.error?.message ?? 'Unable to update account details.'); },
    });
  }

  protected changePassword(): void {
    if (this.passwordForm.invalid) { this.passwordForm.markAllAsTouched(); return; }
    const value = this.passwordForm.getRawValue();
    if (value.newPassword !== value.confirmPassword) { this.error.set('New password and confirmation do not match.'); return; }
    this.saving.set(true); this.error.set(''); this.message.set('');
    this.accountService.changePassword(value.currentPassword, value.newPassword).subscribe({
      next: () => { this.authService.logout(); this.router.navigate(['/login']); },
      error: (err: HttpErrorResponse) => { this.saving.set(false); this.error.set(err.error?.message ?? 'Unable to change password.'); },
    });
  }

  protected async revokeSessions(): Promise<void> {
    if (!(await this.confirmDialog.confirm('Sign out all refresh sessions for this account? You will need to sign in again.'))) return;
    this.accountService.revokeSessions().subscribe({ next: () => { this.authService.logout(); this.router.navigate(['/login']); } });
  }

  private load(): void {
    this.accountService.getAccount().subscribe({
      next: (profile) => { this.profile.set(profile); this.profileForm.patchValue({ email: profile.email ?? '' }); this.loading.set(false); },
      error: () => { this.error.set('Unable to load account details.'); this.loading.set(false); },
    });
  }
}
