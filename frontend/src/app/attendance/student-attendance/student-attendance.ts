import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { AttendanceSummary } from '../attendance.model';
import { AttendanceService } from '../attendance.service';

interface StudentAttendanceState {
  loading: boolean;
  error: boolean;
  summary: AttendanceSummary[];
}

const LOADING_STATE: StudentAttendanceState = { loading: true, error: false, summary: [] };

@Component({
  selector: 'app-student-attendance',
  imports: [RouterLink],
  templateUrl: './student-attendance.html',
  styleUrl: './student-attendance.css',
})
export class StudentAttendance {
  private readonly route = inject(ActivatedRoute);
  private readonly attendanceService = inject(AttendanceService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.attendanceService.getStudentAttendanceSummary(this.studentId).pipe(
      map((summary): StudentAttendanceState => ({ loading: false, error: false, summary })),
      catchError(() => of<StudentAttendanceState>({ loading: false, error: true, summary: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected overallPercentage(summary: AttendanceSummary[]): number {
    const totalClasses = summary.reduce((sum, s) => sum + s.totalClasses, 0);
    const totalPresent = summary.reduce((sum, s) => sum + s.present, 0);
    return totalClasses === 0 ? 0 : Math.round((totalPresent * 100) / totalClasses);
  }
}

