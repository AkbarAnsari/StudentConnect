import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { Assessment } from '../marks.model';
import { MarksService } from '../marks.service';

interface AssessmentsState {
  loading: boolean;
  error: boolean;
  assessments: Assessment[];
}

const LOADING_STATE: AssessmentsState = { loading: true, error: false, assessments: [] };

@Component({
  selector: 'app-assessments-list',
  imports: [RouterLink, FormsModule],
  templateUrl: './assessments-list.html',
  styleUrl: './assessments-list.css',
})
export class AssessmentsList {
  private readonly marksService = inject(MarksService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly state = signal<AssessmentsState>(LOADING_STATE);
  protected readonly statusFilter = signal('All');
  protected readonly subjectFilter = signal('All');
  protected readonly typeFilter = signal('All');
  protected readonly actionId = signal<number | null>(null);
  protected readonly actionError = signal('');

  constructor() {
    this.loadAssessments();
  }

  protected readonly subjects = computed(() => [...new Set(this.state().assessments.map((assessment) => assessment.subjectName))].sort());
  protected readonly types = computed(() => [...new Set(this.state().assessments.map((assessment) => assessment.assessmentType))].sort());
  protected readonly filteredAssessments = computed(() => {
    return this.state().assessments.filter((assessment) =>
      (this.statusFilter() === 'All' || assessment.status === this.statusFilter()) &&
      (this.subjectFilter() === 'All' || assessment.subjectName === this.subjectFilter()) &&
      (this.typeFilter() === 'All' || assessment.assessmentType === this.typeFilter()),
    );
  });

  protected clearFilters(): void {
    this.statusFilter.set('All');
    this.subjectFilter.set('All');
    this.typeFilter.set('All');
  }

  protected async publish(assessment: Assessment): Promise<void> {
    if (!(await this.confirmDialog.confirm(`Publish ${assessment.title}? Students and families will be able to see its marks.`))) return;
    this.changeStatus(assessment, 'Published');
  }

  protected async reopen(assessment: Assessment): Promise<void> {
    if (!(await this.confirmDialog.confirm(`Return ${assessment.title} to Draft? Its marks will be hidden from students and families.`))) return;
    this.changeStatus(assessment, 'Draft');
  }

  private loadAssessments(): void {
    this.state.set(LOADING_STATE);
    this.marksService.getAssessments().subscribe({
      next: (assessments) => this.state.set({ loading: false, error: false, assessments }),
      error: () => this.state.set({ loading: false, error: true, assessments: [] }),
    });
  }

  private changeStatus(assessment: Assessment, status: 'Draft' | 'Published'): void {
    this.actionId.set(assessment.id);
    this.actionError.set('');
    const request = status === 'Published'
      ? this.marksService.publishAssessment(assessment.id)
      : this.marksService.reopenAssessment(assessment.id);
    request.subscribe({
      next: (updated) => {
        this.state.update((current) => ({ ...current, assessments: current.assessments.map((item) => item.id === updated.id ? updated : item) }));
        this.actionId.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.actionId.set(null);
        this.actionError.set(error.error?.message ?? 'Unable to update the assessment status.');
      },
    });
  }
}
