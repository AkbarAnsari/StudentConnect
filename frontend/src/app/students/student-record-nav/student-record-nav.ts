import { Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-student-record-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="record-nav" aria-label="Student record sections">
      <a [routerLink]="['/students', studentId()]" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Profile</a>
      <a [routerLink]="['/students', studentId(), 'attendance']" routerLinkActive="active">Attendance</a>
      <a [routerLink]="['/students', studentId(), 'marks']" routerLinkActive="active">Results</a>
      <a [routerLink]="['/students', studentId(), 'homework']" routerLinkActive="active">Homework</a>
      <a [routerLink]="['/students', studentId(), 'materials']" routerLinkActive="active">Materials</a>
      <a [routerLink]="['/students', studentId(), 'activities']" routerLinkActive="active">Activities</a>
    </nav>
  `,
  styles: [`
    .record-nav { display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); border: 1px solid var(--color-neutral-200); border-radius: 6px; overflow: hidden; background: #fff; }
    .record-nav a { padding: .65rem .45rem; border-right: 1px solid var(--color-neutral-200); color: var(--color-neutral-600); text-align: center; text-decoration: none; font-size: .86rem; font-weight: 700; }
    .record-nav a:last-child { border-right: 0; }
    .record-nav a:hover { color: var(--color-primary-900); background: var(--color-neutral-50); }
    .record-nav a.active { color: var(--color-accent-700); background: var(--color-accent-50); box-shadow: inset 0 -3px var(--color-accent-600); }
    @media (max-width: 640px) { .record-nav { grid-template-columns: repeat(3, minmax(0, 1fr)); }.record-nav a { border-bottom: 1px solid var(--color-neutral-200); }.record-nav a:nth-child(3n) { border-right: 0; }.record-nav a:nth-last-child(-n + 3) { border-bottom: 0; } }
  `],
})
export class StudentRecordNav {
  readonly studentId = input.required<number>();
}
