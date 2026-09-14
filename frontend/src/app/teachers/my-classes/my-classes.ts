import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { SectionStudent } from '../../academic-structure/academic-structure.model';
import { Section } from '../../students/student.model';
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
  private readonly academicStructure = inject(AcademicStructureService);
  private readonly reportService = inject(ReportService);
  private readonly authService = inject(AuthService);
  private readonly teacherSubjectAssignments = inject(TeacherSubjectAssignmentService);

  protected readonly sections = signal<Section[]>([]);
  protected readonly rosters = signal<Record<number, SectionStudent[]>>({});
  protected readonly expandedSectionId = signal<number | null>(null);
  protected readonly classReports = signal<Record<number, SectionClassReport>>({});
  protected readonly assignments = signal<TeacherSubjectAssignment[]>([]);
  protected readonly teacherId = this.authService.currentUser()?.teacherId ?? null;

  constructor() {
    this.academicStructure.getMySections().subscribe((sections) => this.sections.set(sections));
    this.teacherSubjectAssignments.getMine().subscribe((assignments) => this.assignments.set(assignments));

    const teacherId = this.teacherId;
    if (teacherId) {
      this.reportService.getTeacherClassReports(teacherId).subscribe((reports) => {
        this.classReports.set(Object.fromEntries(reports.map((r) => [r.sectionId, r])));
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

  protected toggleSection(sectionId: number): void {
    if (this.expandedSectionId() === sectionId) {
      this.expandedSectionId.set(null);
      return;
    }

    this.expandedSectionId.set(sectionId);
    if (!this.rosters()[sectionId]) {
      this.academicStructure.getStudentsInSection(sectionId).subscribe((roster) => {
        this.rosters.update((current) => ({ ...current, [sectionId]: roster }));
      });
    }
  }
}
