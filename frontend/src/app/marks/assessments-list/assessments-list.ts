import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
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
  imports: [RouterLink],
  templateUrl: './assessments-list.html',
  styleUrl: './assessments-list.css',
})
export class AssessmentsList {
  private readonly marksService = inject(MarksService);

  protected readonly state = toSignal(
    this.marksService.getAssessments().pipe(
      map((assessments): AssessmentsState => ({ loading: false, error: false, assessments })),
      catchError(() => of<AssessmentsState>({ loading: false, error: true, assessments: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly bySubject = computed(() => {
    const groups = new Map<string, Assessment[]>();
    for (const assessment of this.state().assessments) {
      const list = groups.get(assessment.subjectName) ?? [];
      list.push(assessment);
      groups.set(assessment.subjectName, list);
    }
    return [...groups.entries()];
  });
}
