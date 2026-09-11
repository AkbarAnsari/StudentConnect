import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { Homework } from '../homework.model';
import { HomeworkService } from '../homework.service';

interface TeacherHomeworkState {
  loading: boolean;
  error: boolean;
  homework: Homework[];
}

const LOADING_STATE: TeacherHomeworkState = { loading: true, error: false, homework: [] };

// Fallback teacher id for Admin users, who have no teacherId of their own — matches the
// backend's CanActAsTeacher rule (Admin may act as any teacher).
export const DEMO_TEACHER_ID = 1;

@Component({
  selector: 'app-teacher-homework',
  imports: [RouterLink],
  templateUrl: './teacher-homework.html',
  styleUrl: './teacher-homework.css',
})
export class TeacherHomework {
  private readonly homeworkService = inject(HomeworkService);
  private readonly authService = inject(AuthService);

  protected readonly state = toSignal(
    this.homeworkService.getTeacherHomework(this.authService.currentUser()?.teacherId ?? DEMO_TEACHER_ID).pipe(
      map((homework): TeacherHomeworkState => ({ loading: false, error: false, homework })),
      catchError(() => of<TeacherHomeworkState>({ loading: false, error: true, homework: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );
}

