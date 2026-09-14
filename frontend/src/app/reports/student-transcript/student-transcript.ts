import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { triggerBlobDownload } from '../../shared/download.util';
import { StudentTranscript } from '../report.model';
import { ReportService } from '../report.service';

interface TranscriptState {
  loading: boolean;
  error: boolean;
  transcript: StudentTranscript | null;
}

const LOADING_STATE: TranscriptState = { loading: true, error: false, transcript: null };

@Component({
  selector: 'app-student-transcript',
  imports: [RouterLink],
  templateUrl: './student-transcript.html',
  styleUrl: './student-transcript.css',
})
export class StudentTranscriptPage {
  private readonly route = inject(ActivatedRoute);
  private readonly reportService = inject(ReportService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.reportService.getStudentTranscript(this.studentId).pipe(
      map((transcript): TranscriptState => ({ loading: false, error: false, transcript })),
      catchError(() => of<TranscriptState>({ loading: false, error: true, transcript: null })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected print(): void {
    window.print();
  }

  protected downloadCsv(): void {
    this.reportService.exportStudentTranscript(this.studentId).subscribe((blob) => triggerBlobDownload(blob, `transcript-${this.studentId}.csv`));
  }
}
