import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { Teacher } from '../../teachers/teacher.model';
import { TeacherService } from '../../teachers/teacher.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { validateFile } from '../../shared/file-validation.util';
import { LearningMaterial, LearningMaterialVersion } from '../learning-material.model';
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
  imports: [RouterLink, FormsModule, DatePipe],
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
  protected readonly statusFilter = signal('All');
  protected readonly actionId = signal<number | null>(null);
  protected readonly actionError = signal('');
  protected readonly selectedMaterialId = signal<number | null>(null);
  protected readonly versions = signal<LearningMaterialVersion[]>([]);
  protected readonly versionsLoading = signal(false);
  protected readonly replacementFile = signal<File | null>(null);
  protected readonly replacementReason = signal('');
  protected readonly replacementError = signal('');
  private readonly refreshTick = signal(0);

  // Admins have no teacherId of their own — they must explicitly pick which teacher's
  // materials to view instead of silently defaulting to an arbitrary teacher.
  protected readonly teachers = signal<Teacher[]>([]);
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
  protected readonly filteredMaterials = computed(() => this.state().materials.filter((material) =>
    this.statusFilter() === 'All' || material.status === this.statusFilter(),
  ));
  protected readonly selectedMaterial = computed(() => this.state().materials.find((material) => material.id === this.selectedMaterialId()) ?? null);

  constructor() {
    if (this.authService.role() === 'Admin') {
      this.teacherService.getTeachers().subscribe((result) => this.teachers.set(result.items));
    }
  }

  protected onTeacherChange(value: string): void {
    this.selectedTeacherId.set(value ? Number(value) : null);
  }

  protected download(material: LearningMaterial): void {
    this.materialsService.downloadFile(material.id).subscribe((blob) => triggerBlobDownload(blob, material.fileName));
  }

  protected openVersions(material: LearningMaterial): void {
    if (this.selectedMaterialId() === material.id) {
      this.selectedMaterialId.set(null);
      return;
    }
    this.selectedMaterialId.set(material.id);
    this.replacementFile.set(null);
    this.replacementReason.set('');
    this.replacementError.set('');
    this.loadVersions(material.id);
  }

  private loadVersions(materialId: number): void {
    this.versionsLoading.set(true);
    this.materialsService.getVersions(materialId).subscribe({
      next: (versions) => { this.versions.set(versions); this.versionsLoading.set(false); },
      error: () => { this.versions.set([]); this.versionsLoading.set(false); this.replacementError.set('Unable to load version history.'); },
    });
  }

  protected onReplacementFile(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    const error = validateFile(file, ['.pdf', '.docx', '.png', '.jpg', '.jpeg'], 10 * 1024 * 1024);
    this.replacementError.set(error ?? '');
    this.replacementFile.set(error ? null : file);
  }

  protected replaceFile(material: LearningMaterial): void {
    const file = this.replacementFile();
    const reason = this.replacementReason().trim();
    if (!file || !reason) {
      this.replacementError.set(!file ? 'Select a valid replacement file.' : 'Enter a replacement reason.');
      return;
    }
    const formData = new FormData();
    formData.append('file', file);
    formData.append('changeReason', reason);
    this.actionId.set(material.id);
    this.materialsService.replaceFile(material.id, formData).subscribe({
      next: () => {
        this.actionId.set(null);
        this.replacementFile.set(null);
        this.replacementReason.set('');
        this.refreshTick.update((value) => value + 1);
        this.loadVersions(material.id);
      },
      error: (error: HttpErrorResponse) => { this.actionId.set(null); this.replacementError.set(error.error?.message ?? 'Unable to replace this file.'); },
    });
  }

  protected downloadVersion(materialId: number, version: LearningMaterialVersion): void {
    this.materialsService.downloadVersion(materialId, version.id).subscribe((blob) => triggerBlobDownload(blob, version.fileName));
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

  protected async changeLifecycle(material: LearningMaterial, action: 'publish' | 'close' | 'archive' | 'reopen'): Promise<void> {
    const messages = {
      publish: `Publish ${material.title}? Learners will be able to download it.`,
      close: `Close ${material.title}? Learners will no longer see it.`,
      archive: `Archive ${material.title}? The file will be retained but hidden from active content.`,
      reopen: `Return ${material.title} to Draft? It will be hidden from learners.`,
    };
    if (!(await this.confirmDialog.confirm(messages[action]))) return;

    this.actionId.set(material.id);
    this.actionError.set('');
    const request = action === 'publish' ? this.materialsService.publish(material.id)
      : action === 'close' ? this.materialsService.close(material.id)
      : action === 'archive' ? this.materialsService.archive(material.id)
      : this.materialsService.reopen(material.id);
    request.subscribe({
      next: () => { this.actionId.set(null); this.refreshTick.update((value) => value + 1); },
      error: (error: HttpErrorResponse) => { this.actionId.set(null); this.actionError.set(error.error?.message ?? 'Unable to update material status.'); },
    });
  }
}
