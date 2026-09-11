import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { SubmissionOverview } from '../homework.model';
import { HomeworkSubmissionService } from '../homework.service';

interface SubmissionsState {
  loading: boolean;
  error: boolean;
  submissions: SubmissionOverview[];
}

const LOADING_STATE: SubmissionsState = { loading: true, error: false, submissions: [] };

@Component({
  selector: 'app-homework-submissions',
  imports: [RouterLink],
  templateUrl: './homework-submissions.html',
  styleUrl: './homework-submissions.css',
})
export class HomeworkSubmissions {
  private readonly route = inject(ActivatedRoute);
  private readonly submissionService = inject(HomeworkSubmissionService);

  protected readonly homeworkId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.submissionService.getSubmissionsForHomework(this.homeworkId).pipe(
      map((submissions): SubmissionsState => ({ loading: false, error: false, submissions })),
      catchError(() => of<SubmissionsState>({ loading: false, error: true, submissions: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );
}
