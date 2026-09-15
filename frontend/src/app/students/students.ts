import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Student } from './student.model';
import { StudentService } from './student.service';

const PAGE_SIZE = 50;

interface StudentsState {
  loading: boolean;
  error: boolean;
  students: Student[];
  totalCount: number;
}

const LOADING_STATE: StudentsState = { loading: true, error: false, students: [], totalCount: 0 };

@Component({
  selector: 'app-students',
  imports: [RouterLink],
  templateUrl: './students.html',
  styleUrl: './students.css',
})
export class Students {
  private readonly studentService = inject(StudentService);
  protected readonly authService = inject(AuthService);

  protected readonly searchTerm = signal('');
  protected readonly gradeFilter = signal<number | null>(null);
  protected readonly statusFilter = signal<string>('Active');
  protected readonly currentPage = signal(1);

  // Re-fetches from the server whenever grade, status, or page changes — grade/status are now
  // real server-side filters (previously the whole "All" roster was fetched once and filtered
  // client-side, which silently hid any student past the first 200 rows).
  protected readonly state = toSignal(
    combineLatest([toObservable(this.gradeFilter), toObservable(this.statusFilter), toObservable(this.currentPage)]).pipe(
      switchMap(([gradeId, status, page]) =>
        this.studentService.getStudents(status, page, PAGE_SIZE, gradeId ?? undefined).pipe(
          map((result): StudentsState => ({ loading: false, error: false, students: result.items, totalCount: result.totalCount })),
          catchError(() => of<StudentsState>({ loading: false, error: true, students: [], totalCount: 0 })),
          startWith(LOADING_STATE),
        ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });

  // Search still applies only within the current page — full server-side search across the
  // whole roster is a follow-up enhancement, not part of this pagination fix.
  protected readonly filteredStudents = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) {
      return this.state().students;
    }
    return this.state().students.filter(
      (student) =>
        student.firstName.toLowerCase().includes(term) ||
        student.lastName.toLowerCase().includes(term) ||
        student.studentId.toLowerCase().includes(term),
    );
  });

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.state().totalCount / PAGE_SIZE)));

  protected readonly rangeStart = computed(() => (this.state().totalCount === 0 ? 0 : (this.currentPage() - 1) * PAGE_SIZE + 1));
  protected readonly rangeEnd = computed(() => Math.min(this.currentPage() * PAGE_SIZE, this.state().totalCount));

  protected onSearchChange(value: string): void {
    this.searchTerm.set(value);
  }

  protected onGradeChange(value: string): void {
    this.gradeFilter.set(value ? Number(value) : null);
    this.currentPage.set(1);
  }

  protected onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.currentPage.set(1);
  }

  protected previousPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
    }
  }

  protected nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update((p) => p + 1);
    }
  }
}
