import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { AttendanceRecord } from '../attendance.model';
import { AttendanceService } from '../attendance.service';

interface SubjectAttendanceState {
  loading: boolean;
  error: boolean;
  records: AttendanceRecord[];
}

const LOADING_STATE: SubjectAttendanceState = { loading: true, error: false, records: [] };

@Component({
  selector: 'app-subject-attendance',
  imports: [RouterLink],
  templateUrl: './subject-attendance.html',
  styleUrl: './subject-attendance.css',
})
export class SubjectAttendance {
  private readonly route = inject(ActivatedRoute);
  private readonly attendanceService = inject(AttendanceService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly subjectId = Number(this.route.snapshot.paramMap.get('subjectId'));

  protected readonly state = toSignal(
    this.attendanceService.getStudentSubjectAttendance(this.studentId, this.subjectId).pipe(
      map((records): SubjectAttendanceState => ({ loading: false, error: false, records })),
      catchError(() => of<SubjectAttendanceState>({ loading: false, error: true, records: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );
}
