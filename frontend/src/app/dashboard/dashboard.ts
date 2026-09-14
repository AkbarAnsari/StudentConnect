import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.css',
})
export class Dashboard {
  protected readonly authService = inject(AuthService);

  protected readonly resendSent = signal(false);
  protected readonly resendError = signal('');

  protected resendVerification(): void {
    this.resendError.set('');
    this.authService.resendVerification().subscribe({
      next: () => this.resendSent.set(true),
      error: (err: HttpErrorResponse) =>
        this.resendError.set(err.error?.message ?? 'Unable to resend the verification email. Please try again later.'),
    });
  }
}
