import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { StudentHomework } from '../homework.model';
import { HomeworkService } from '../homework.service';

interface StudentHomeworkState {
  loading: boolean;
  error: boolean;
  homework: StudentHomework[];
}

const LOADING_STATE: StudentHomeworkState = { loading: true, error: false, homework: [] };

@Component({
  selector: 'app-student-homework',
  imports: [RouterLink],
  templateUrl: './student-homework.html',
  styleUrl: './student-homework.css',
})
export class StudentHomeworkList {
  private readonly route = inject(ActivatedRoute);
  private readonly homeworkService = inject(HomeworkService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.homeworkService.getStudentHomework(this.studentId).pipe(
      map((homework): StudentHomeworkState => ({ loading: false, error: false, homework })),
      catchError(() => of<StudentHomeworkState>({ loading: false, error: true, homework: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );
}
