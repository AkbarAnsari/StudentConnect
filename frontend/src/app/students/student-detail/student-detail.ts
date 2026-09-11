import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { AttendanceSummary } from '../../attendance/attendance.model';
import { AttendanceService } from '../../attendance/attendance.service';
import { MarksSummary } from '../../marks/marks.model';
import { MarksService } from '../../marks/marks.service';
import { StudentHomework } from '../../homework/homework.model';
import { HomeworkService } from '../../homework/homework.service';
import { LearningMaterial } from '../../learning-materials/learning-material.model';
import { LearningMaterialsService } from '../../learning-materials/learning-materials.service';
import { ActivityRegistration } from '../../activities/activity.model';
import { ActivityRegistrationService } from '../../activities/activity-registration.service';
import { Student } from '../student.model';
import { StudentService } from '../student.service';

interface StudentDetailState {
  loading: boolean;
  notFound: boolean;
  error: boolean;
  student: Student | null;
}

const LOADING_STATE: StudentDetailState = { loading: true, notFound: false, error: false, student: null };

interface AttendanceSummaryState {
  loading: boolean;
  error: boolean;
  summary: AttendanceSummary[];
}

const ATTENDANCE_LOADING_STATE: AttendanceSummaryState = { loading: true, error: false, summary: [] };

interface MarksSummaryState {
  loading: boolean;
  error: boolean;
  summary: MarksSummary[];
}

const MARKS_LOADING_STATE: MarksSummaryState = { loading: true, error: false, summary: [] };

interface HomeworkSummaryState {
  loading: boolean;
  error: boolean;
  homework: StudentHomework[];
}

const HOMEWORK_LOADING_STATE: HomeworkSummaryState = { loading: true, error: false, homework: [] };

interface MaterialsState {
  loading: boolean;
  error: boolean;
  materials: LearningMaterial[];
}

const MATERIALS_LOADING_STATE: MaterialsState = { loading: true, error: false, materials: [] };

interface ActivitiesState {
  loading: boolean;
  error: boolean;
  registrations: ActivityRegistration[];
}

const ACTIVITIES_LOADING_STATE: ActivitiesState = { loading: true, error: false, registrations: [] };

@Component({
  selector: 'app-student-detail',
  imports: [RouterLink],
  templateUrl: './student-detail.html',
  styleUrl: './student-detail.css',
})
export class StudentDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly studentService = inject(StudentService);
  private readonly attendanceService = inject(AttendanceService);
  private readonly marksService = inject(MarksService);
  private readonly homeworkService = inject(HomeworkService);
  private readonly materialsService = inject(LearningMaterialsService);
  private readonly activityRegistrationService = inject(ActivityRegistrationService);

  protected readonly state = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) =>
        this.studentService.getStudentById(id).pipe(
          map((student): StudentDetailState => ({ loading: false, notFound: false, error: false, student })),
          catchError((err: HttpErrorResponse) =>
            of<StudentDetailState>({
              loading: false,
              notFound: err.status === 404,
              error: err.status !== 404,
              student: null,
            }),
          ),
          startWith(LOADING_STATE),
        ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly attendanceState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) =>
        this.attendanceService.getStudentAttendanceSummary(id).pipe(
          map((summary): AttendanceSummaryState => ({ loading: false, error: false, summary })),
          catchError(() => of<AttendanceSummaryState>({ loading: false, error: true, summary: [] })),
          startWith(ATTENDANCE_LOADING_STATE),
        ),
      ),
    ),
    { initialValue: ATTENDANCE_LOADING_STATE },
  );

  protected readonly marksState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) =>
        this.marksService.getStudentMarksSummary(id).pipe(
          map((summary): MarksSummaryState => ({ loading: false, error: false, summary })),
          catchError(() => of<MarksSummaryState>({ loading: false, error: true, summary: [] })),
          startWith(MARKS_LOADING_STATE),
        ),
      ),
    ),
    { initialValue: MARKS_LOADING_STATE },
  );

  protected readonly homeworkState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) =>
        this.homeworkService.getStudentHomework(id).pipe(
          map((homework): HomeworkSummaryState => ({ loading: false, error: false, homework })),
          catchError(() => of<HomeworkSummaryState>({ loading: false, error: true, homework: [] })),
          startWith(HOMEWORK_LOADING_STATE),
        ),
      ),
    ),
    { initialValue: HOMEWORK_LOADING_STATE },
  );

  protected readonly materialsState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) =>
        this.materialsService.getForStudent(id).pipe(
          map((materials): MaterialsState => ({ loading: false, error: false, materials })),
          catchError(() => of<MaterialsState>({ loading: false, error: true, materials: [] })),
          startWith(MATERIALS_LOADING_STATE),
        ),
      ),
    ),
    { initialValue: MATERIALS_LOADING_STATE },
  );

  protected readonly activitiesState = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) =>
        this.activityRegistrationService.getForStudent(id).pipe(
          map((registrations): ActivitiesState => ({ loading: false, error: false, registrations })),
          catchError(() => of<ActivitiesState>({ loading: false, error: true, registrations: [] })),
          startWith(ACTIVITIES_LOADING_STATE),
        ),
      ),
    ),
    { initialValue: ACTIVITIES_LOADING_STATE },
  );

  protected readonly subjects = ['Mathematics', 'Science', 'English', 'Computer Science'];
  protected readonly deleteError = signal(false);

  protected onDelete(id: number): void {
    if (!confirm('Are you sure you want to delete this student?')) {
      return;
    }

    this.deleteError.set(false);
    this.studentService.deleteStudent(id).subscribe({
      next: () => this.router.navigate(['/students']),
      error: () => this.deleteError.set(true),
    });
  }
}
