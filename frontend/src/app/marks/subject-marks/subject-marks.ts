import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { MarkRecord } from '../marks.model';
import { MarksService } from '../marks.service';

interface SubjectMarksState {
  loading: boolean;
  error: boolean;
  records: MarkRecord[];
}

const LOADING_STATE: SubjectMarksState = { loading: true, error: false, records: [] };

@Component({
  selector: 'app-subject-marks',
  imports: [RouterLink],
  templateUrl: './subject-marks.html',
  styleUrl: './subject-marks.css',
})
export class SubjectMarks {
  private readonly route = inject(ActivatedRoute);
  private readonly marksService = inject(MarksService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly subjectId = Number(this.route.snapshot.paramMap.get('subjectId'));

  protected readonly state = toSignal(
    this.marksService.getStudentSubjectMarks(this.studentId, this.subjectId).pipe(
      map((records): SubjectMarksState => ({ loading: false, error: false, records })),
      catchError(() => of<SubjectMarksState>({ loading: false, error: true, records: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );
}
