import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SectionStudent } from '../../academic-structure/academic-structure.model';
import { AuthService } from '../../auth/auth.service';
import { SectionClassReport } from '../../reports/report.model';
import { ReportService } from '../../reports/report.service';
import { TeacherSubjectAssignment } from '../teacher-subject-assignment.model';
import { TeacherSubjectAssignmentService } from '../teacher-subject-assignment.service';
import { triggerBlobDownload } from '../../shared/download.util';

@Component({
  selector: 'app-my-classes',
  imports: [RouterLink],
  templateUrl: './my-classes.html',
  styleUrl: './my-classes.css',
})
export class MyClasses {
  private readonly reportService = inject(ReportService);
  private readonly authService = inject(AuthService);
  private readonly teacherSubjectAssignments = inject(TeacherSubjectAssignmentService);

  protected readonly rosters = signal<Record<number, SectionStudent[]>>({});
  protected readonly expandedAssignmentId = signal<number | null>(null);
  protected readonly classReports = signal<Record<number, SectionClassReport>>({});
  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly teacherId = this.authService.currentUser()?.teacherId ?? null;

  constructor() {
    this.teacherSubjectAssignments.getMine().subscribe((assignments) => this.assignments.set(assignments));

    const teacherId = this.teacherId;
    if (teacherId) {
      this.reportService.getTeacherClassReports(teacherId).subscribe((reports) => {
        this.classReports.set(Object.fromEntries(reports.map((report) => [report.assignmentId, report])));
      });
    }
  }

  protected downloadCsv(): void {
    const teacherId = this.teacherId;
    if (!teacherId) {
      return;
    }
    this.reportService.exportTeacherClassReports(teacherId).subscribe((blob) => triggerBlobDownload(blob, 'class-reports.csv'));
  }

  protected toggleAssignment(assignmentId: number): void {
    if (this.expandedAssignmentId() === assignmentId) {
      this.expandedAssignmentId.set(null);
      return;
    }

    this.expandedAssignmentId.set(assignmentId);
    if (!this.rosters()[assignmentId]) {
      this.teacherSubjectAssignments.getStudents(assignmentId).subscribe((roster) => {
        this.rosters.update((current) => ({ ...current, [assignmentId]: roster }));
      });
    }
  }
}
