import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { SectionStudent } from '../../academic-structure/academic-structure.model';
import { Section } from '../../students/student.model';
import { AuthService } from '../../auth/auth.service';
import { SectionClassReport } from '../../reports/report.model';
import { ReportService } from '../../reports/report.service';

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

  protected readonly sections = signal<Section[]>([]);
  protected readonly rosters = signal<Record<number, SectionStudent[]>>({});
  protected readonly expandedSectionId = signal<number | null>(null);
  protected readonly classReports = signal<Record<number, SectionClassReport>>({});

  constructor() {
    this.academicStructure.getMySections().subscribe((sections) => this.sections.set(sections));

    const teacherId = this.authService.currentUser()?.teacherId;
    if (teacherId) {
      this.reportService.getTeacherClassReports(teacherId).subscribe((reports) => {
        this.classReports.set(Object.fromEntries(reports.map((r) => [r.sectionId, r])));
      });
    }
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
