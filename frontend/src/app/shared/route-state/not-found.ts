import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `
    <section class="route-state page" aria-labelledby="not-found-title">
      <p class="route-code">404</p>
      <h1 id="not-found-title">Page not found</h1>
      <p>The address may be incorrect, or the page may have moved.</p>
      <div class="route-actions">
        <a class="btn" [routerLink]="authService.isAuthenticated() ? '/dashboard' : '/'">
          {{ authService.isAuthenticated() ? 'Return to dashboard' : 'Return home' }}
        </a>
        <a class="btn btn-secondary" routerLink="/contact">Contact support</a>
      </div>
    </section>
  `,
  styleUrl: './route-state.css',
})
export class NotFound {
  protected readonly authService = inject(AuthService);
}
