import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { StudentReportCard } from '../report.model';
import { ReportService } from '../report.service';

interface ReportCardState {
  loading: boolean;
  error: boolean;
  reportCard: StudentReportCard | null;
}

const LOADING_STATE: ReportCardState = { loading: true, error: false, reportCard: null };

@Component({
  selector: 'app-student-report-card',
  imports: [RouterLink],
  templateUrl: './student-report-card.html',
  styleUrl: './student-report-card.css',
})
export class StudentReportCardPage {
  private readonly route = inject(ActivatedRoute);
  private readonly reportService = inject(ReportService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.reportService.getStudentReportCard(this.studentId).pipe(
      map((reportCard): ReportCardState => ({ loading: false, error: false, reportCard })),
      catchError(() => of<ReportCardState>({ loading: false, error: true, reportCard: null })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected print(): void {
    window.print();
  }
}
