import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { StudentHomework } from '../homework.model';
import { HomeworkService } from '../homework.service';
import { StudentRecordNav } from '../../students/student-record-nav/student-record-nav';

interface StudentHomeworkState {
  loading: boolean;
  error: boolean;
  homework: StudentHomework[];
}

const LOADING_STATE: StudentHomeworkState = { loading: true, error: false, homework: [] };

@Component({
  selector: 'app-student-homework',
  imports: [RouterLink, StudentRecordNav],
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

  protected readonly groups = computed(() => {
    const today = new Date().toISOString().slice(0, 10);
    const soon = new Date();
    soon.setDate(soon.getDate() + 7);
    const soonDate = soon.toISOString().slice(0, 10);
    const homework = this.state().homework;
    return [
      { label: 'Overdue', items: homework.filter((item) => item.status === 'Not Submitted' && item.dueDate < today) },
      { label: 'Due soon', items: homework.filter((item) => item.status === 'Not Submitted' && item.dueDate >= today && item.dueDate <= soonDate) },
      { label: 'Upcoming', items: homework.filter((item) => item.status === 'Not Submitted' && item.dueDate > soonDate) },
      { label: 'Completed', items: homework.filter((item) => item.status !== 'Not Submitted') },
    ].filter((group) => group.items.length > 0);
  });
}
