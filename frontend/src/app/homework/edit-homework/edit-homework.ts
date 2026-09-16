import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { HomeworkService } from '../homework.service';

@Component({
  selector: 'app-edit-homework',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './edit-homework.html',
  styleUrl: '../create-homework/create-homework.css',
})
export class EditHomework {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly homeworkService = inject(HomeworkService);
  private readonly homeworkId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly homework = toSignal(this.homeworkService.getHomeworkById(this.homeworkId), { initialValue: null });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');
  private saved = false;

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    dueDate: ['', Validators.required],
  });

  constructor() {
    effect(() => {
      const homework = this.homework();
      if (!homework) return;
      this.form.setValue({ title: homework.title, description: homework.description, dueDate: homework.dueDate });
      this.form.markAsPristine();
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.errorMessage.set('');
    this.homeworkService.updateHomework(this.homeworkId, this.form.getRawValue()).subscribe({
      next: () => { this.saved = true; this.router.navigate(['/teacher-homework']); },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.message ?? 'Unable to update the homework.');
      },
    });
  }

  hasUnsavedChanges(): boolean { return this.form.dirty && !this.saved && !this.submitting(); }

  @HostListener('window:beforeunload', ['$event'])
  protected onBeforeUnload(event: BeforeUnloadEvent): void { if (this.hasUnsavedChanges()) event.preventDefault(); }
}
