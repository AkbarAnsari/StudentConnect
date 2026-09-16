import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { AccountService } from '../../auth/account.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { Student } from '../../students/student.model';
import { StudentService } from '../../students/student.service';
import { Parent, ParentStudentHistory } from '../parent.model';
import { ParentService } from '../parent.service';

@Component({
  selector: 'app-manage-parents',
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './manage-parents.html',
  styleUrl: './manage-parents.css',
})
export class ManageParents {
  private readonly fb = inject(FormBuilder);
  private readonly parentService = inject(ParentService);
  private readonly studentService = inject(StudentService);
  private readonly accountService = inject(AccountService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly students = toSignal(this.studentService.getStudents().pipe(map((r) => r.items)), { initialValue: [] });
  protected readonly parents = signal<Parent[]>([]);
  protected readonly selectedChildIds = signal<number[]>([]);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly successMessage = signal('');
  protected readonly loadingParents = signal(true);
  protected readonly actionId = signal<number | null>(null);
  protected readonly linkSelection = signal<Record<number, number>>({});
  protected readonly linkRelationship = signal<Record<number, string>>({});
  protected readonly linkPrimary = signal<Record<number, boolean>>({});
  protected readonly historyParentId = signal<number | null>(null);
  protected readonly history = signal<ParentStudentHistory[]>([]);

  // Unfiltered dropdowns/checkbox lists become unusable once a school has more than a
  // handful of students — both pickers below filter client-side by name or student code.
  protected readonly childSearchTerm = signal('');
  protected readonly linkSearchTerm = signal<Record<number, string>>({});

  private static matches(student: Student, term: string): boolean {
    const t = term.trim().toLowerCase();
    if (!t) {
      return true;
    }
    return (
      student.firstName.toLowerCase().includes(t) ||
      student.lastName.toLowerCase().includes(t) ||
      student.studentId.toLowerCase().includes(t)
    );
  }

  protected readonly filteredStudentsForCreate = computed(() =>
    this.students().filter((student) => ManageParents.matches(student, this.childSearchTerm())),
  );

  protected filteredStudentsForLink(parentId: number): Student[] {
    return this.students().filter((student) => ManageParents.matches(student, this.linkSearchTerm()[parentId] ?? ''));
  }

  protected onLinkSearchChange(parentId: number, value: string): void {
    this.linkSearchTerm.update((sel) => ({ ...sel, [parentId]: value }));
  }

  protected readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(8)]],
    email: ['', Validators.email],
  });

  constructor() {
    this.loadParents();
  }

  private loadParents(): void {
    this.parentService.getParents().subscribe({
      next: (parents) => { this.parents.set(parents); this.loadingParents.set(false); },
      error: () => { this.loadingParents.set(false); this.errorMessage.set('Unable to load family accounts.'); },
    });
  }

  protected toggleChild(studentId: number, checked: boolean): void {
    this.selectedChildIds.update((ids) => (checked ? [...ids, studentId] : ids.filter((id) => id !== studentId)));
  }

  protected onChildSearchChange(value: string): void {
    this.childSearchTerm.set(value);
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');

    const { username, password, email } = this.form.getRawValue();
    this.parentService.createParent({ username, password, email: email || undefined, childStudentIds: this.selectedChildIds() }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.form.reset();
        this.selectedChildIds.set([]);
        this.childSearchTerm.set('');
        this.successMessage.set('Family account created.');
        this.loadParents();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.message ?? 'Unable to create the family account.');
      },
    });
  }

  protected onLinkSelect(parentId: number, studentId: number): void {
    this.linkSelection.update((sel) => ({ ...sel, [parentId]: studentId }));
  }

  protected onRelationshipChange(parentId: number, value: string): void {
    this.linkRelationship.update((items) => ({ ...items, [parentId]: value }));
  }

  protected onPrimaryChange(parentId: number, value: boolean): void {
    this.linkPrimary.update((items) => ({ ...items, [parentId]: value }));
  }

  protected linkChild(parentId: number): void {
    const studentId = this.linkSelection()[parentId];
    if (!studentId) {
      return;
    }
    this.actionId.set(parentId); this.errorMessage.set(''); this.successMessage.set('');
    this.parentService.linkChild(parentId, studentId, this.linkRelationship()[parentId] ?? 'Guardian', this.linkPrimary()[parentId] ?? false).subscribe({
      next: () => { this.actionId.set(null); this.successMessage.set('Student access linked. Existing sessions were revoked.'); this.loadParents(); },
      error: (err: HttpErrorResponse) => { this.actionId.set(null); this.errorMessage.set(err.error?.message ?? 'Unable to link this student.'); },
    });
  }

  protected showHistory(parentId: number): void {
    if (this.historyParentId() === parentId) { this.historyParentId.set(null); return; }
    this.historyParentId.set(parentId);
    this.parentService.getHistory(parentId).subscribe({ next: history => this.history.set(history), error: () => this.errorMessage.set('Unable to load family-link history.') });
  }

  protected async unlinkChild(parentId: number, studentId: number, childName: string): Promise<void> {
    if (!(await this.confirmDialog.confirm(`Remove ${childName} from this family account? Existing refresh sessions will be revoked.`))) return;
    this.actionId.set(parentId); this.errorMessage.set(''); this.successMessage.set('');
    this.parentService.unlinkChild(parentId, studentId).subscribe({
      next: () => { this.actionId.set(null); this.successMessage.set('Student access removed and sessions revoked.'); this.loadParents(); },
      error: (err: HttpErrorResponse) => { this.actionId.set(null); this.errorMessage.set(err.error?.message ?? 'Unable to remove student access.'); },
    });
  }

  protected sendInvitation(parent: Parent): void {
    this.actionId.set(parent.id); this.errorMessage.set(''); this.successMessage.set('');
    this.accountService.sendPasswordInvitation(parent.id).subscribe({
      next: () => { this.actionId.set(null); this.successMessage.set(`Password invitation sent to ${parent.username}.`); },
      error: (err: HttpErrorResponse) => { this.actionId.set(null); this.errorMessage.set(err.error?.message ?? 'Unable to send the invitation.'); },
    });
  }

  protected async setStatus(parent: Parent, status: 'Active' | 'Deactivated'): Promise<void> {
    const action = status === 'Active' ? 'Reactivate' : 'Deactivate';
    if (!(await this.confirmDialog.confirm(`${action} ${parent.username}?${status === 'Deactivated' ? ' Existing sessions will be revoked.' : ''}`))) return;
    this.actionId.set(parent.id); this.errorMessage.set(''); this.successMessage.set('');
    const request = status === 'Active' ? this.accountService.reactivate(parent.id) : this.accountService.deactivate(parent.id);
    request.subscribe({
      next: () => { this.actionId.set(null); this.successMessage.set(`${parent.username} ${status === 'Active' ? 'reactivated' : 'deactivated'}.`); this.loadParents(); },
      error: (err: HttpErrorResponse) => { this.actionId.set(null); this.errorMessage.set(err.error?.message ?? 'Unable to update this account.'); },
    });
  }
}
