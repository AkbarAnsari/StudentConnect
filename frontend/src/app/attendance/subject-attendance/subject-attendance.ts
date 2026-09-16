import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, forkJoin, map, of, startWith, switchMap } from 'rxjs';
import { StudentRecordNav } from '../../students/student-record-nav/student-record-nav';
import { AttendanceCorrection, AttendanceRecord } from '../attendance.model';
import { AttendanceService } from '../attendance.service';

interface SubjectAttendanceState {
  loading: boolean;
  error: boolean;
  records: AttendanceRecord[];
  corrections: AttendanceCorrection[];
}

const LOADING_STATE: SubjectAttendanceState = { loading: true, error: false, records: [], corrections: [] };
const dateValue = (date: Date) => date.toISOString().slice(0, 10);

@Component({
  selector: 'app-subject-attendance',
  imports: [RouterLink, DatePipe, FormsModule, StudentRecordNav],
  templateUrl: './subject-attendance.html',
  styleUrl: './subject-attendance.css',
})
export class SubjectAttendance {
  private readonly route = inject(ActivatedRoute);
  private readonly attendanceService = inject(AttendanceService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly subjectId = Number(this.route.snapshot.paramMap.get('subjectId'));
  protected readonly dateTo = signal(dateValue(new Date()));
  protected readonly dateFrom = signal(dateValue(new Date(Date.now() - 90 * 86_400_000)));
  private readonly range$ = new BehaviorSubject({ dateFrom: this.dateFrom(), dateTo: this.dateTo() });

  protected readonly state = toSignal(
    this.range$.pipe(
      switchMap(({ dateFrom, dateTo }) => forkJoin({
        records: this.attendanceService.getStudentSubjectAttendance(this.studentId, this.subjectId, dateFrom, dateTo),
        corrections: this.attendanceService.getStudentSubjectAttendanceCorrections(this.studentId, this.subjectId, dateFrom, dateTo),
      }).pipe(
        map(({ records, corrections }): SubjectAttendanceState => ({ loading: false, error: false, records, corrections })),
        catchError(() => of<SubjectAttendanceState>({ loading: false, error: true, records: [], corrections: [] })),
        startWith(LOADING_STATE),
      )),
    ),
    { initialValue: LOADING_STATE },
  );

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
