export interface LearningMaterial {
  id: number;
  title: string;
  description: string;
  topic: string;
  subjectId: number;
  subjectName: string;
  gradeId: number;
  gradeName: string;
  sectionId: number | null;
  sectionName: string | null;
  teacherId: number;
  teacherName: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedDate: string;
  version: number;
  status: 'Draft' | 'Scheduled' | 'Published' | 'Closed' | 'Archived';
  scheduledForUtc: string | null;
  publishedAtUtc: string | null;
  closedAtUtc: string | null;
  archivedAtUtc: string | null;
}

export interface LearningMaterialVersion {
  id: number;
  version: number;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedAtUtc: string;
  uploadedByName: string | null;
  changeReason: string | null;
}

export interface LearningMaterialFilter {
  subjectId?: number | null;
  gradeId?: number | null;
}
