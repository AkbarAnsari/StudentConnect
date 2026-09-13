import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AcademicStructureService } from '../../academic-structure/academic-structure.service';
import { SectionStudent } from '../../academic-structure/academic-structure.model';
import { Section } from '../../students/student.model';

@Component({
  selector: 'app-my-classes',
  imports: [RouterLink],
  templateUrl: './my-classes.html',
  styleUrl: './my-classes.css',
})
export class MyClasses {
  private readonly academicStructure = inject(AcademicStructureService);

  protected readonly sections = signal<Section[]>([]);
  protected readonly rosters = signal<Record<number, SectionStudent[]>>({});
  protected readonly expandedSectionId = signal<number | null>(null);

  constructor() {
    this.academicStructure.getMySections().subscribe((sections) => this.sections.set(sections));
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
