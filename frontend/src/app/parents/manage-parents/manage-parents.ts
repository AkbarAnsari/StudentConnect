import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { StudentService } from '../../students/student.service';
import { Parent } from '../parent.model';
import { ParentService } from '../parent.service';

@Component({
  selector: 'app-manage-parents',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './manage-parents.html',
  styleUrl: './manage-parents.css',
})
export class ManageParents {
  private readonly fb = inject(FormBuilder);
  private readonly parentService = inject(ParentService);
  private readonly studentService = inject(StudentService);

  protected readonly students = toSignal(this.studentService.getStudents(), { initialValue: [] });
  protected readonly parents = signal<Parent[]>([]);
  protected readonly selectedChildIds = signal<number[]>([]);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly linkSelection = signal<Record<number, number>>({});

  protected readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(6)]],
    email: ['', Validators.email],
  });

  constructor() {
    this.loadParents();
  }

  private loadParents(): void {
    this.parentService.getParents().subscribe((parents) => this.parents.set(parents));
  }

  protected toggleChild(studentId: number, checked: boolean): void {
    this.selectedChildIds.update((ids) => (checked ? [...ids, studentId] : ids.filter((id) => id !== studentId)));
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const { username, password, email } = this.form.getRawValue();
    this.parentService.createParent({ username, password, email: email || undefined, childStudentIds: this.selectedChildIds() }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.form.reset();
        this.selectedChildIds.set([]);
        this.loadParents();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.status === 409
            ? 'That username is already taken.'
            : 'Unable to create the parent account. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }

  protected onLinkSelect(parentId: number, studentId: number): void {
    this.linkSelection.update((sel) => ({ ...sel, [parentId]: studentId }));
  }

  protected linkChild(parentId: number): void {
    const studentId = this.linkSelection()[parentId];
    if (!studentId) {
      return;
    }
    this.parentService.linkChild(parentId, studentId).subscribe(() => this.loadParents());
  }

  protected unlinkChild(parentId: number, studentId: number): void {
    this.parentService.unlinkChild(parentId, studentId).subscribe(() => this.loadParents());
  }
}
