import { Component, HostListener, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './auth/auth.service';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('StudentConnect');
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly mobileMenuOpen = signal(false);
  protected readonly manageMenuOpen = signal(false);

  protected readonly initials = () => {
    const name = this.authService.currentUser()?.displayName ?? '';
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');
  };

  protected toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
  }

  protected closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  protected toggleManageMenu(): void {
    this.manageMenuOpen.update((open) => !open);
  }

  protected closeManageMenu(): void {
    this.manageMenuOpen.set(false);
  }

  // Closing the dropdown on outside clicks keeps it from staying open when the
  // user interacts with anything else on the page.
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!(event.target as HTMLElement).closest('.nav-dropdown')) {
      this.manageMenuOpen.set(false);
    }
  }

  protected onLogout(): void {
    this.closeMobileMenu();
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

