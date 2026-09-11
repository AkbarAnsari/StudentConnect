import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ActivityService } from '../activity.service';

@Component({
  selector: 'app-edit-activity',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './edit-activity.html',
  styleUrl: './edit-activity.css',
})
export class EditActivity {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly activityService = inject(ActivityService);

  private readonly activityId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly activity = toSignal(this.activityService.getById(this.activityId), { initialValue: null });
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal('');

  protected readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    date: ['', Validators.required],
    location: ['', Validators.required],
    capacity: [1, [Validators.required, Validators.min(1)]],
  });

  constructor() {
    effect(() => {
      const a = this.activity();
      if (a) {
        this.form.setValue({
          title: a.title,
          description: a.description,
          date: a.date.slice(0, 16),
          location: a.location,
          capacity: a.capacity,
        });
      }
    });
  }

  protected onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    const raw = this.form.getRawValue();
    this.activityService.update(this.activityId, { ...raw, date: new Date(raw.date).toISOString() }).subscribe({
      next: () => this.router.navigate(['/activity-management']),
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.errorMessage.set(
          err.error?.message ?? 'Unable to update the activity. Please check whether the StudentConnect API is running.',
        );
      },
    });
  }
}
