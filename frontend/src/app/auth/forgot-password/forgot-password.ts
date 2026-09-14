import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.html',
  styleUrl: './forgot-password.css',
})
export class ForgotPassword {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  protected readonly submitting = signal(false);
  protected readonly resultMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
  });

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.resultMessage.set('');

    // Same generic message on success or failure — never reveal account existence.
    this.authService.forgotPassword(this.form.getRawValue()).subscribe({
      next: (res) => {
        this.submitting.set(false);
        this.resultMessage.set(res.message);
      },
      error: () => {
        this.submitting.set(false);
        this.resultMessage.set('If an account exists for that username, a password reset email has been sent.');
      },
    });
  }
}
