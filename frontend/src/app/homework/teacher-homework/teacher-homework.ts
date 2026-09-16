import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { Teacher } from '../../teachers/teacher.model';
import { TeacherService } from '../../teachers/teacher.service';
import { Homework } from '../homework.model';
import { HomeworkService } from '../homework.service';

interface TeacherHomeworkState {
  loading: boolean;
  error: boolean;
  homework: Homework[];
}

const LOADING_STATE: TeacherHomeworkState = { loading: true, error: false, homework: [] };
const EMPTY_STATE: TeacherHomeworkState = { loading: false, error: false, homework: [] };

@Component({
  selector: 'app-teacher-homework',
  imports: [RouterLink, FormsModule, DatePipe],
  templateUrl: './teacher-homework.html',
  styleUrl: './teacher-homework.css',
})
export class TeacherHomework {
  private readonly homeworkService = inject(HomeworkService);
  private readonly teacherService = inject(TeacherService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly authService = inject(AuthService);

  // Admins have no teacherId of their own — they must explicitly pick which teacher's
  // homework to view instead of silently defaulting to an arbitrary teacher.
  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly selectedTeacherId = signal<number | null>(this.authService.currentUser()?.teacherId ?? null);

  private readonly refresh$ = new BehaviorSubject<void>(undefined);

  protected readonly state = toSignal(
    combineLatest([toObservable(this.selectedTeacherId), this.refresh$]).pipe(
      switchMap(([teacherId]) =>
        teacherId === null
          ? of<TeacherHomeworkState>(EMPTY_STATE)
          : this.homeworkService.getTeacherHomework(teacherId).pipe(
              map((homework): TeacherHomeworkState => ({ loading: false, error: false, homework })),
              catchError(() => of<TeacherHomeworkState>({ loading: false, error: true, homework: [] })),
              startWith(LOADING_STATE),
            ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly selectedIds = signal<number[]>([]);
  protected readonly bulkDueDate = signal('');
  protected readonly bulkSubmitting = signal(false);
  protected readonly bulkMessage = signal('');
  protected readonly bulkError = signal('');
  protected readonly statusFilter = signal('All');
  protected readonly actionId = signal<number | null>(null);
  protected readonly actionError = signal('');
  protected readonly filteredHomework = computed(() => this.state().homework.filter((item) =>
    this.statusFilter() === 'All' || item.status === this.statusFilter(),
  ));

  constructor() {
    if (this.authService.role() === 'Admin') {
      this.teacherService.getTeachers().subscribe((result) => this.teachers.set(result.items));
    }
  }

  protected onTeacherChange(value: string): void {
    this.selectedTeacherId.set(value ? Number(value) : null);
  }

  protected toggleSelected(id: number, checked: boolean): void {
    this.selectedIds.update((ids) => (checked ? [...ids, id] : ids.filter((existing) => existing !== id)));
  }

  protected toggleSelectAll(checked: boolean): void {
    this.selectedIds.set(checked ? this.filteredHomework().filter((item) => item.status === 'Draft').map((item) => item.id) : []);
  }

  protected onBulkDueDateChange(value: string): void {
    this.bulkDueDate.set(value);
  }

  protected applyBulkDueDate(): void {
    const homeworkIds = this.selectedIds();
    const dueDate = this.bulkDueDate();
    if (homeworkIds.length === 0 || !dueDate) {
      this.bulkError.set('Select at least one homework item and a due date.');
      return;
    }

    this.bulkSubmitting.set(true);
    this.bulkError.set('');
    this.bulkMessage.set('');

    this.homeworkService.bulkUpdateDueDate({ homeworkIds, dueDate }).subscribe({
      next: () => {
        this.bulkSubmitting.set(false);
        this.bulkMessage.set(`Due date updated for ${homeworkIds.length} item(s).`);
        this.selectedIds.set([]);
        this.refresh$.next();
      },
      error: () => {
        this.bulkSubmitting.set(false);
        this.bulkError.set('Unable to update due dates. Please try again.');
      },
    });
  }

  protected async changeLifecycle(item: Homework, action: 'publish' | 'close' | 'archive' | 'reopen'): Promise<void> {
    const messages = {
      publish: `Publish ${item.title}? Learners and families will be able to see it.`,
      close: `Close ${item.title}? Learners can still review it, but submissions will stop.`,
      archive: `Archive ${item.title}? It will be removed from active learner work.`,
      reopen: `Return ${item.title} to Draft? It will be hidden from learners while you edit it.`,
    };
    if (!(await this.confirmDialog.confirm(messages[action]))) return;

    this.actionId.set(item.id);
    this.actionError.set('');
    const request = action === 'publish' ? this.homeworkService.publishHomework(item.id)
      : action === 'close' ? this.homeworkService.closeHomework(item.id)
      : action === 'archive' ? this.homeworkService.archiveHomework(item.id)
      : this.homeworkService.reopenHomework(item.id);
    request.subscribe({
      next: () => { this.actionId.set(null); this.selectedIds.set([]); this.refresh$.next(); },
      error: (error: HttpErrorResponse) => { this.actionId.set(null); this.actionError.set(error.error?.message ?? 'Unable to update homework status.'); },
    });
  }
}

