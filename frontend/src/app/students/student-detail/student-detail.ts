import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { Student, StudentHistoryEntry, StudentSubject } from '../student.model';
import { StudentRecordNav } from '../student-record-nav/student-record-nav';
import { StudentService } from '../student.service';

interface StudentDetailState { loading: boolean; notFound: boolean; error: boolean; student: Student | null; }
interface SubjectsState { loading: boolean; error: boolean; subjects: StudentSubject[]; }
interface HistoryState { loading: boolean; error: boolean; entries: StudentHistoryEntry[]; }

const LOADING_STATE: StudentDetailState = { loading: true, notFound: false, error: false, student: null };
const SUBJECTS_LOADING_STATE: SubjectsState = { loading: true, error: false, subjects: [] };
const HISTORY_LOADING_STATE: HistoryState = { loading: true, error: false, entries: [] };

@Component({
  selector: 'app-student-detail',
  imports: [RouterLink, NgIcon, DatePipe, StudentRecordNav],
  templateUrl: './student-detail.html',
  styleUrl: './student-detail.css',
})
export class StudentDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly studentService = inject(StudentService);
  protected readonly authService = inject(AuthService);
  protected readonly canManageStudent = computed(() => this.authService.role() === 'Admin');
  protected readonly backRoute = computed(() => this.authService.role() === 'Admin' || this.authService.role() === 'Teacher' ? '/students' : '/dashboard');
  protected readonly backLabel = computed(() => this.authService.role() === 'Admin' || this.authService.role() === 'Teacher' ? 'Back to Students' : 'Back to Dashboard');

  protected readonly state = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) => this.studentService.getStudentById(id).pipe(
        map((student): StudentDetailState => ({ loading: false, notFound: false, error: false, student })),
        catchError((err: HttpErrorResponse) => of<StudentDetailState>({ loading: false, notFound: err.status === 404, error: err.status !== 404, student: null })),
        startWith(LOADING_STATE),
      )),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly subjectsState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) => this.studentService.getStudentSubjects(id).pipe(
        map((subjects): SubjectsState => ({ loading: false, error: false, subjects })),
        catchError(() => of<SubjectsState>({ loading: false, error: true, subjects: [] })),
        startWith(SUBJECTS_LOADING_STATE),
      )),
    ),
    { initialValue: SUBJECTS_LOADING_STATE },
  );

  protected readonly historyState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) => this.studentService.getStudentHistory(id).pipe(
        map((entries): HistoryState => ({ loading: false, error: false, entries })),
        catchError(() => of<HistoryState>({ loading: false, error: true, entries: [] })),
        startWith(HISTORY_LOADING_STATE),
      )),
    ),
    { initialValue: HISTORY_LOADING_STATE },
  );
}
