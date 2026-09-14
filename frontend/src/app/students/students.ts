import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Student } from './student.model';
import { StudentService } from './student.service';

interface StudentsState {
  loading: boolean;
  error: boolean;
  students: Student[];
}

@Component({
  selector: 'app-students',
  imports: [RouterLink],
  templateUrl: './students.html',
  styleUrl: './students.css',
})
export class Students {
  private readonly studentService = inject(StudentService);
  protected readonly authService = inject(AuthService);

  protected readonly state = toSignal(
    this.studentService.getStudents('All').pipe(
      map((result): StudentsState => ({ loading: false, error: false, students: result.items })),
      catchError(() => of<StudentsState>({ loading: false, error: true, students: [] })),
      startWith<StudentsState>({ loading: true, error: false, students: [] }),
    ),
    { initialValue: { loading: true, error: false, students: [] } },
  );

  protected readonly grades = computed(() =>
    [...new Map(this.state().students.map((student) => [student.gradeId, student.gradeName])).entries()]
      .sort((a, b) => a[0] - b[0]),
  );

  protected readonly searchTerm = signal('');
  protected readonly gradeFilter = signal<number | null>(null);
  protected readonly statusFilter = signal<string>('Active');

  protected readonly filteredStudents = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const grade = this.gradeFilter();
    const status = this.statusFilter();

    return this.state().students.filter((student) => {
      const matchesTerm =
        !term ||
        student.firstName.toLowerCase().includes(term) ||
        student.lastName.toLowerCase().includes(term) ||
        student.studentId.toLowerCase().includes(term);
      const matchesGrade = grade === null || student.gradeId === grade;
      const matchesStatus = status === 'All' || student.status === status;
      return matchesTerm && matchesGrade && matchesStatus;
    });
  });

  protected onSearchChange(value: string): void {
    this.searchTerm.set(value);
  }

  protected onGradeChange(value: string): void {
    this.gradeFilter.set(value ? Number(value) : null);
  }

  protected onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
  }
}
