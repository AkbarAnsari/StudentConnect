import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { AttendanceSummary } from '../attendance.model';
import { AttendanceService } from '../attendance.service';
import { StudentRecordNav } from '../../students/student-record-nav/student-record-nav';

interface StudentAttendanceState {
  loading: boolean;
  error: boolean;
  summary: AttendanceSummary[];
}

const LOADING_STATE: StudentAttendanceState = { loading: true, error: false, summary: [] };
const dateValue = (date: Date) => date.toISOString().slice(0, 10);

@Component({
  selector: 'app-student-attendance',
  imports: [RouterLink, StudentRecordNav, FormsModule],
  templateUrl: './student-attendance.html',
  styleUrl: './student-attendance.css',
})
export class StudentAttendance {
  private readonly route = inject(ActivatedRoute);
  private readonly attendanceService = inject(AttendanceService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly dateTo = signal(dateValue(new Date()));
  protected readonly dateFrom = signal(dateValue(new Date(Date.now() - 90 * 86_400_000)));
  private readonly range$ = new BehaviorSubject({ dateFrom: this.dateFrom(), dateTo: this.dateTo() });

  protected readonly state = toSignal(
    this.range$.pipe(
      switchMap(({ dateFrom, dateTo }) => this.attendanceService.getStudentAttendanceSummary(this.studentId, dateFrom, dateTo).pipe(
        map((summary): StudentAttendanceState => ({ loading: false, error: false, summary })),
        catchError(() => of<StudentAttendanceState>({ loading: false, error: true, summary: [] })),
        startWith(LOADING_STATE),
      )),
    ),
    { initialValue: LOADING_STATE },
  );

  protected overallPercentage(summary: AttendanceSummary[]): number {
    const totalClasses = summary.reduce((sum, s) => sum + s.totalClasses, 0);
    const totalPresent = summary.reduce((sum, s) => sum + s.present, 0);
    return totalClasses === 0 ? 0 : Math.round((totalPresent * 100) / totalClasses);
  }

  protected totalClasses(summary: AttendanceSummary[]): number {
    return summary.reduce((sum, subject) => sum + subject.totalClasses, 0);
  }

  protected totalPresent(summary: AttendanceSummary[]): number {
    return summary.reduce((sum, subject) => sum + subject.present, 0);
  }

  protected totalAbsent(summary: AttendanceSummary[]): number {
    return summary.reduce((sum, subject) => sum + subject.absent, 0);
  }

  protected applyRange(): void {
    if (this.dateFrom() > this.dateTo()) return;
    this.range$.next({ dateFrom: this.dateFrom(), dateTo: this.dateTo() });
  }

  protected setPeriod(days: number): void {
    const end = new Date();
    this.dateTo.set(dateValue(end));
    this.dateFrom.set(dateValue(new Date(end.getTime() - days * 86_400_000)));
    this.applyRange();
  }
}

