import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { MarksSummary } from '../marks.model';
import { MarksService } from '../marks.service';
import { StudentRecordNav } from '../../students/student-record-nav/student-record-nav';

interface StudentMarksState {
  loading: boolean;
  error: boolean;
  summary: MarksSummary[];
}

const LOADING_STATE: StudentMarksState = { loading: true, error: false, summary: [] };

@Component({
  selector: 'app-student-marks',
  imports: [RouterLink, StudentRecordNav],
  templateUrl: './student-marks.html',
  styleUrl: './student-marks.css',
})
export class StudentMarks {
  private readonly route = inject(ActivatedRoute);
  private readonly marksService = inject(MarksService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.marksService.getStudentMarksSummary(this.studentId).pipe(
      map((summary): StudentMarksState => ({ loading: false, error: false, summary })),
      catchError(() => of<StudentMarksState>({ loading: false, error: true, summary: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected overallPercentage(summary: MarksSummary[]): number {
    const totalMax = summary.reduce((sum, s) => sum + s.totalMaximumMarks, 0);
    const totalObtained = summary.reduce((sum, s) => sum + s.totalMarksObtained, 0);
    return totalMax === 0 ? 0 : Math.round((totalObtained * 100) / totalMax);
  }

  protected totalAssessments(summary: MarksSummary[]): number {
    return summary.reduce((sum, subject) => sum + subject.assessments, 0);
  }
}
