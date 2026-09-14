import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { DEMO_TEACHER_ID } from '../../homework/teacher-homework/teacher-homework';
import { triggerBlobDownload } from '../../shared/download.util';
import { LearningMaterial } from '../learning-material.model';
import { LearningMaterialsService } from '../learning-materials.service';

interface TeacherMaterialsState {
  loading: boolean;
  error: boolean;
  materials: LearningMaterial[];
}

const LOADING_STATE: TeacherMaterialsState = { loading: true, error: false, materials: [] };

@Component({
  selector: 'app-teacher-materials',
  imports: [RouterLink],
  templateUrl: './teacher-materials.html',
  styleUrl: './teacher-materials.css',
})
export class TeacherMaterials {
  private readonly materialsService = inject(LearningMaterialsService);
  private readonly authService = inject(AuthService);

  protected readonly materials = signal<LearningMaterial[]>([]);
  protected readonly deleteError = signal(false);

  protected readonly state = toSignal(
    this.materialsService.getForTeacher(this.authService.currentUser()?.teacherId ?? DEMO_TEACHER_ID).pipe(
      map((materials): TeacherMaterialsState => ({ loading: false, error: false, materials })),
      catchError(() => of<TeacherMaterialsState>({ loading: false, error: true, materials: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );

  protected download(material: LearningMaterial): void {
    this.materialsService.downloadFile(material.id).subscribe((blob) => triggerBlobDownload(blob, material.fileName));
  }

  protected formatSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected onDelete(id: number): void {
    if (!confirm('Are you sure you want to delete this learning material?')) {
      return;
    }

    this.deleteError.set(false);
    this.materialsService.delete(id).subscribe({
      next: () => window.location.reload(),
      error: () => this.deleteError.set(true),
    });
  }
}
