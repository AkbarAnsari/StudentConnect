import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { TeacherService } from '../../teachers/teacher.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { LearningMaterial } from '../learning-material.model';
import { LearningMaterialsService } from '../learning-materials.service';

interface TeacherMaterialsState {
  loading: boolean;
  error: boolean;
  materials: LearningMaterial[];
}

const LOADING_STATE: TeacherMaterialsState = { loading: true, error: false, materials: [] };
const EMPTY_STATE: TeacherMaterialsState = { loading: false, error: false, materials: [] };

@Component({
  selector: 'app-teacher-materials',
  imports: [RouterLink],
  templateUrl: './teacher-materials.html',
  styleUrl: './teacher-materials.css',
})
export class TeacherMaterials {
  private readonly materialsService = inject(LearningMaterialsService);
  private readonly teacherService = inject(TeacherService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  protected readonly authService = inject(AuthService);

  protected readonly materials = signal<LearningMaterial[]>([]);
  protected readonly deleteError = signal(false);
  private readonly refreshTick = signal(0);

  // Admins have no teacherId of their own — they must explicitly pick which teacher's
  // materials to view instead of silently defaulting to an arbitrary teacher.
  protected readonly teachers = toSignal(this.teacherService.getTeachers().pipe(map((r) => r.items)), { initialValue: [] });
  protected readonly selectedTeacherId = signal<number | null>(this.authService.currentUser()?.teacherId ?? null);

  protected readonly state = toSignal(
    combineLatest([toObservable(this.selectedTeacherId), toObservable(this.refreshTick)]).pipe(
      switchMap(([teacherId]) =>
        teacherId === null
          ? of<TeacherMaterialsState>(EMPTY_STATE)
          : this.materialsService.getForTeacher(teacherId).pipe(
              map((materials): TeacherMaterialsState => ({ loading: false, error: false, materials })),
              catchError(() => of<TeacherMaterialsState>({ loading: false, error: true, materials: [] })),
              startWith(LOADING_STATE),
            ),
      ),
    ),
    { initialValue: LOADING_STATE },
  );

  protected onTeacherChange(value: string): void {
    this.selectedTeacherId.set(value ? Number(value) : null);
  }

  protected download(material: LearningMaterial): void {
    this.materialsService.downloadFile(material.id).subscribe((blob) => triggerBlobDownload(blob, material.fileName));
  }

  protected formatSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected async onDelete(id: number): Promise<void> {
    if (!(await this.confirmDialog.confirm('Are you sure you want to delete this learning material?'))) {
      return;
    }

    this.deleteError.set(false);
    this.materialsService.delete(id).subscribe({
      next: () => this.refreshTick.update((n) => n + 1),
      error: () => this.deleteError.set(true),
    });
  }
}
