export interface LearningMaterial {
  id: number;
  title: string;
  description: string;
  subjectId: number;
  subjectName: string;
  gradeId: number;
  gradeName: string;
  teacherId: number;
  teacherName: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  uploadedDate: string;
}

export interface LearningMaterialFilter {
  subjectId?: number | null;
  gradeId?: number | null;
}
