import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { catchError, combineLatest, debounceTime, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ConfirmDialogService } from '../shared/confirm-dialog/confirm-dialog.service';
import { triggerBlobDownload } from '../shared/download.util';
import { Student, StudentLifecycleStatus } from './student.model';
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
  imports: [RouterLink, NgIcon],
  templateUrl: './students.html',
  styleUrl: './students.css',
})
export class Students {
  private readonly studentService = inject(StudentService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly authService = inject(AuthService);

  protected readonly searchTerm = signal('');
  protected readonly gradeFilter = signal<number | null>(null);
  protected readonly sectionFilter = signal<number | null>(null);
  protected readonly statusFilter = signal<string>('Active');
  protected readonly sortOrder = signal('name');
  protected readonly currentPage = signal(1);
  private readonly refreshTick = signal(0);
  protected readonly selectedIds = signal<ReadonlySet<number>>(new Set());
  protected readonly bulkStatus = signal<StudentLifecycleStatus>('Transferred');
  protected readonly bulkReason = signal('');
  protected readonly actionError = signal('');
  protected readonly actionMessage = signal('');
  protected readonly bulkUpdating = signal(false);
  protected readonly exporting = signal(false);

  private readonly debouncedSearchTerm = toObservable(this.searchTerm).pipe(
    map((value) => value.trim()),
    debounceTime(250),
    distinctUntilChanged(),
  );

  protected readonly state = toSignal(
    combineLatest([
      this.debouncedSearchTerm,
      toObservable(this.gradeFilter),
      toObservable(this.sectionFilter),
      toObservable(this.statusFilter),
      toObservable(this.sortOrder),
      toObservable(this.currentPage),
      toObservable(this.refreshTick),
    ]).pipe(
      switchMap(([query, gradeId, sectionId, status, sort, page]) =>
        this.studentService.getStudents(status, page, PAGE_SIZE, gradeId ?? undefined, query || undefined, sort, sectionId ?? undefined).pipe(
          map((result): StudentsState => ({ loading: false, error: false, students: result.items, totalCount: result.totalCount })),
          catchError(() => of<StudentsState>({ loading: false, error: true, students: [], totalCount: 0 })),
          startWith(LOADING_STATE),
        ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly grades = toSignal(this.studentService.getGrades(), { initialValue: [] });
  protected readonly sections = toSignal(
    toObservable(this.gradeFilter).pipe(switchMap((gradeId) => gradeId ? this.studentService.getSectionsByGrade(gradeId) : of([]))),
    { initialValue: [] },
  );

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.state().totalCount / PAGE_SIZE)));

  protected readonly rangeStart = computed(() => (this.state().totalCount === 0 ? 0 : (this.currentPage() - 1) * PAGE_SIZE + 1));
  protected readonly rangeEnd = computed(() => Math.min(this.currentPage() * PAGE_SIZE, this.state().totalCount));
  protected readonly allPageSelected = computed(() => this.state().students.length > 0 && this.state().students.every((student) => this.selectedIds().has(student.id)));

  protected onSearchChange(value: string): void {
    this.searchTerm.set(value);
    this.currentPage.set(1);
    this.selectedIds.set(new Set());
  }

  protected onGradeChange(value: string): void {
    this.gradeFilter.set(value ? Number(value) : null);
    this.sectionFilter.set(null);
    this.currentPage.set(1);
    this.selectedIds.set(new Set());
  }

  protected onSectionChange(value: string): void {
    this.sectionFilter.set(value ? Number(value) : null);
    this.currentPage.set(1);
    this.selectedIds.set(new Set());
  }

  protected onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.currentPage.set(1);
    this.selectedIds.set(new Set());
  }

  protected onSortChange(value: string): void {
    this.sortOrder.set(value);
    this.currentPage.set(1);
    this.selectedIds.set(new Set());
  }

  protected clearFilters(): void {
    this.searchTerm.set('');
    this.gradeFilter.set(null);
    this.sectionFilter.set(null);
    this.statusFilter.set('Active');
    this.sortOrder.set('name');
    this.currentPage.set(1);
    this.selectedIds.set(new Set());
  }

  protected retry(): void {
    this.refreshTick.update((value) => value + 1);
  }

  protected previousPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update((p) => p - 1);
      this.selectedIds.set(new Set());
    }
  }

  protected nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update((p) => p + 1);
      this.selectedIds.set(new Set());
    }
  }

  protected isSelected(id: number): boolean { return this.selectedIds().has(id); }

  protected toggleSelected(id: number, selected: boolean): void {
    const updated = new Set(this.selectedIds()); selected ? updated.add(id) : updated.delete(id); this.selectedIds.set(updated);
  }

  protected togglePage(selected: boolean): void {
    this.selectedIds.set(selected ? new Set(this.state().students.map((student) => student.id)) : new Set());
  }

  protected async applyBulkStatus(): Promise<void> {
    const ids = [...this.selectedIds()];
    if (ids.length === 0) return;
    if (this.bulkStatus() !== 'Active' && !this.bulkReason().trim()) {
      this.actionError.set('Enter a reason for this lifecycle change.');
      return;
    }
    if (!(await this.confirmDialog.confirm(`Change ${ids.length} selected ${ids.length === 1 ? 'student' : 'students'} to ${this.bulkStatus()}?`))) return;
    this.bulkUpdating.set(true); this.actionError.set(''); this.actionMessage.set('');
    this.studentService.updateStatuses(ids, this.bulkStatus(), this.bulkReason().trim()).subscribe({
      next: (result) => { this.bulkUpdating.set(false); this.selectedIds.set(new Set()); this.bulkReason.set(''); this.actionMessage.set(`${result.updated} student ${result.updated === 1 ? 'record was' : 'records were'} updated.`); this.retry(); },
      error: (err) => { this.bulkUpdating.set(false); this.actionError.set(err?.error?.message ?? 'The selected students could not be updated.'); },
    });
  }

  protected exportRoster(): void {
    this.exporting.set(true); this.actionError.set('');
    this.studentService.exportStudents(this.statusFilter(), this.gradeFilter() ?? undefined, this.sectionFilter() ?? undefined, this.searchTerm().trim() || undefined, this.sortOrder()).subscribe({
      next: (blob) => { this.exporting.set(false); triggerBlobDownload(blob, `student-roster-${new Date().toISOString().slice(0, 10)}.csv`); },
      error: () => { this.exporting.set(false); this.actionError.set('The roster export could not be downloaded.'); },
    });
  }
}
