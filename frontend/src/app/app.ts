import { Component, HostListener, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideActivity,
  lucideArrowLeft,
  lucideArrowRight,
  lucideBell,
  lucideBookOpen,
  lucideCalendarCheck,
  lucideChartNoAxesCombined,
  lucideCheck,
  lucideClipboardList,
  lucideEye,
  lucideEyeOff,
  lucideFileText,
  lucideGraduationCap,
  lucideHeartHandshake,
  lucideLayoutDashboard,
  lucideLifeBuoy,
  lucideLock,
  lucideLogOut,
  lucideMail,
  lucideMegaphone,
  lucideMenu,
  lucideSchool,
  lucideSearch,
  lucideSettings,
  lucideShieldCheck,
  lucideUserRound,
  lucideUsers,
  lucideX,
} from '@ng-icons/lucide';
import { AuthService } from './auth/auth.service';
import { NotificationBell } from './notifications/notification-bell/notification-bell';
import { ConfirmDialog } from './shared/confirm-dialog/confirm-dialog';

interface ShellNavItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
}

interface ShellNavGroup {
  label: string;
  items: ShellNavItem[];
}

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NotificationBell, ConfirmDialog, NgIcon],
  providers: [
    provideIcons({
      lucideActivity,
      lucideArrowLeft,
      lucideArrowRight,
      lucideBell,
      lucideBookOpen,
      lucideCalendarCheck,
      lucideChartNoAxesCombined,
      lucideCheck,
      lucideClipboardList,
      lucideEye,
      lucideEyeOff,
      lucideFileText,
      lucideGraduationCap,
      lucideHeartHandshake,
      lucideLayoutDashboard,
      lucideLifeBuoy,
      lucideLock,
      lucideLogOut,
      lucideMail,
      lucideMegaphone,
      lucideMenu,
      lucideSchool,
      lucideSearch,
      lucideSettings,
      lucideShieldCheck,
      lucideUserRound,
      lucideUsers,
      lucideX,
    }),
  ],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('StudentConnect');
  protected readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly mobileMenuOpen = signal(false);
  protected readonly online = signal(navigator.onLine);

  @HostListener('window:online')
  protected onOnline(): void {
    this.online.set(true);
  }

  @HostListener('window:offline')
  protected onOffline(): void {
    this.online.set(false);
  }

  protected readonly workspaceLabel = computed(() => {
    switch (this.authService.role()) {
      case 'Admin': return 'School operations';
      case 'Teacher': return 'Teaching workspace';
      case 'Student': return 'Student workspace';
      case 'Parent': return 'Family workspace';
      case 'Coordinator': return 'Activity coordination';
      default: return 'School workspace';
    }
  });

  protected readonly navigationGroups = computed<ShellNavGroup[]>(() => {
    const user = this.authService.currentUser();
    if (!user) {
      return [];
    }

    const overview: ShellNavGroup = {
      label: 'Workspace',
      items: [
        { label: 'Overview', route: '/dashboard', icon: 'lucideLayoutDashboard' },
        { label: 'Notifications', route: '/notifications', icon: 'lucideMail' },
        { label: 'Account', route: '/account', icon: 'lucideUserRound' },
      ],
    };

    if (user.role === 'Admin') {
      return [
        overview,
        {
          label: 'People',
          items: [
            { label: 'Users & access', route: '/users-access', icon: 'lucideShieldCheck' },
            { label: 'Students', route: '/students', icon: 'lucideUsers' },
            { label: 'Teachers', route: '/manage-teachers', icon: 'lucideGraduationCap' },
            { label: 'Families', route: '/manage-parents', icon: 'lucideHeartHandshake' },
          ],
        },
        {
          label: 'Academics',
          items: [
            { label: 'Academic structure', route: '/manage-academic-structure', icon: 'lucideSchool' },
            { label: 'Attendance', route: '/record-attendance', icon: 'lucideCalendarCheck' },
            { label: 'Leave requests', route: '/leave-requests', icon: 'lucideCalendarCheck' },
            { label: 'Assessments', route: '/assessments', icon: 'lucideClipboardList' },
            { label: 'Enter marks', route: '/enter-marks', icon: 'lucideFileText' },
          ],
        },
        {
          label: 'Teaching',
          items: [
            { label: 'Homework', route: '/teacher-homework', icon: 'lucideBookOpen' },
            { label: 'Learning materials', route: '/teacher-materials', icon: 'lucideFileText' },
            { label: 'Activities', route: '/activity-management', icon: 'lucideActivity' },
            { label: 'Announcements', route: '/announcements', icon: 'lucideMegaphone' },
          ],
        },
        {
          label: 'Insights',
          items: [
            { label: 'Analytics', route: '/analytics', icon: 'lucideChartNoAxesCombined' },
            { label: 'Search', route: '/search', icon: 'lucideSearch' },
          ],
        },
        {
          label: 'System',
          items: [
            { label: 'Audit log', route: '/audit-log', icon: 'lucideShieldCheck' },
            { label: 'Email delivery', route: '/email-outbox', icon: 'lucideMail' },
            { label: 'Operations', route: '/ops', icon: 'lucideSettings' },
            { label: 'Report administration', route: '/report-administration', icon: 'lucideFileText' },
          ],
        },
      ];
    }

    if (user.role === 'Teacher') {
      return [
        overview,
        {
          label: 'My work',
          items: [
            { label: 'My classes', route: '/my-classes', icon: 'lucideSchool' },
            { label: 'Attendance', route: '/record-attendance', icon: 'lucideCalendarCheck' },
            { label: 'Assessments', route: '/assessments', icon: 'lucideClipboardList' },
            { label: 'Enter marks', route: '/enter-marks', icon: 'lucideFileText' },
          ],
        },
        {
          label: 'Teaching',
          items: [
            { label: 'Homework', route: '/teacher-homework', icon: 'lucideBookOpen' },
            { label: 'Learning materials', route: '/teacher-materials', icon: 'lucideFileText' },
            { label: 'Activities', route: '/activity-management', icon: 'lucideActivity' },
            { label: 'Announcements', route: '/announcements', icon: 'lucideMegaphone' },
          ],
        },
        {
          label: 'Insights',
          items: [
            { label: 'Analytics', route: '/analytics', icon: 'lucideChartNoAxesCombined' },
            { label: 'Search', route: '/search', icon: 'lucideSearch' },
          ],
        },
      ];
    }

    if (user.role === 'Coordinator') {
      return [overview, { label: 'Programs', items: [{ label: 'Activity moderation', route: '/activity-management', icon: 'lucideActivity' }] }];
    }

    if (user.role === 'Student' && user.studentId) {
      const studentRoot = `/students/${user.studentId}`;
      return [
        overview,
        {
          label: 'My learning',
          items: [
            { label: 'Profile', route: studentRoot, icon: 'lucideUserRound' },
            { label: 'Attendance', route: `${studentRoot}/attendance`, icon: 'lucideCalendarCheck' },
            { label: 'Marks', route: `${studentRoot}/marks`, icon: 'lucideFileText' },
            { label: 'Homework', route: `${studentRoot}/homework`, icon: 'lucideBookOpen' },
            { label: 'Materials', route: `${studentRoot}/materials`, icon: 'lucideFileText' },
            { label: 'Activities', route: `${studentRoot}/activities`, icon: 'lucideActivity' },
            { label: 'Leave requests', route: '/leave-requests', icon: 'lucideCalendarCheck' },
          ],
        },
      ];
    }

    if(user.role==='Parent')return[overview,{label:'Family',items:[{label:'Leave requests',route:'/leave-requests',icon:'lucideCalendarCheck'}]}];
    return [overview];
  });

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

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    if (this.mobileMenuOpen()) {
      this.closeMobileMenu();
    }
  }

  protected onLogout(): void {
    this.closeMobileMenu();
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

