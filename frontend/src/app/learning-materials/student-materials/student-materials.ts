import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { triggerBlobDownload } from '../../shared/download.util';
import { LearningMaterial } from '../learning-material.model';
import { LearningMaterialsService } from '../learning-materials.service';
import { StudentRecordNav } from '../../students/student-record-nav/student-record-nav';

interface StudentMaterialsState {
  loading: boolean;
  error: boolean;
  materials: LearningMaterial[];
}

const LOADING_STATE: StudentMaterialsState = { loading: true, error: false, materials: [] };

@Component({
  selector: 'app-student-materials',
  imports: [RouterLink, StudentRecordNav],
  templateUrl: './student-materials.html',
  styleUrl: './student-materials.css',
})
export class StudentMaterials {
  private readonly route = inject(ActivatedRoute);
  private readonly materialsService = inject(LearningMaterialsService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    this.materialsService.getForStudent(this.studentId).pipe(
      map((materials): StudentMaterialsState => ({ loading: false, error: false, materials })),
      catchError(() => of<StudentMaterialsState>({ loading: false, error: true, materials: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected readonly subjectFilter = signal<number | null>(null);

  protected readonly subjects = computed(() =>
    [...new Map(this.state().materials.map((m) => [m.subjectId, m.subjectName])).entries()],
  );

  protected readonly filteredMaterials = computed(() => {
    const subject = this.subjectFilter();
    return this.state().materials.filter((m) => subject === null || m.subjectId === subject);
  });

  protected onSubjectChange(value: string): void {
    this.subjectFilter.set(value ? Number(value) : null);
  }

  protected download(material: LearningMaterial): void {
    this.materialsService.downloadFile(material.id).subscribe((blob) => triggerBlobDownload(blob, material.fileName));
  }

  protected formatSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
