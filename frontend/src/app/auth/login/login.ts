import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, isDevMode, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { AuthService } from '../auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, NgIcon],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  protected readonly showDevelopmentAccounts = isDevMode();
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  // Pre-filled when the auth interceptor redirects here after a rejected/expired token.
  protected readonly errorMessage = signal(
    this.route.snapshot.queryParamMap.get('sessionExpired') ? 'Your session has expired. Please log in again.' : '',
  );

  protected readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected toggleShowPassword(): void {
    this.showPassword.update((value) => !value);
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/dashboard');
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        // Deliberately generic — never reveal whether the username exists.
        this.errorMessage.set(err.error?.message ?? 'Invalid username or password.');
      },
    });
  }
}
