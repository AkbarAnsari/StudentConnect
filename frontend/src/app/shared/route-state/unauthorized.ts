import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

@Component({
  selector: 'app-unauthorized',
  imports: [RouterLink],
  template: `
    <section class="route-state page" aria-labelledby="unauthorized-title">
      <p class="route-code">403</p>
      <h1 id="unauthorized-title">Access restricted</h1>
      <p>Your account does not have permission to open this page.</p>
      @if (attemptedPath) {
        <p class="attempted-path">Requested page: <code>{{ attemptedPath }}</code></p>
      }
      <div class="route-actions">
        <a class="btn" routerLink="/dashboard">Return to dashboard</a>
        <a class="btn btn-secondary" routerLink="/contact">Contact support</a>
      </div>
    </section>
  `,
  styleUrl: './route-state.css',
})
export class Unauthorized {
  private readonly route = inject(ActivatedRoute);
  protected readonly attemptedPath = this.route.snapshot.queryParamMap.get('from');
}
