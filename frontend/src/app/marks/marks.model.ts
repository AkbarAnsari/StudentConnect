export interface MarkRecord {
  id: number;
  assessmentId: number;
  assessmentTitle: string;
  subjectId: number;
  subjectName: string;
  assessmentDate: string;
  marksObtained: number;
  maximumMarks: number;
  comment: string | null;
  version: number;
  updatedAtUtc: string | null;
}

export interface MarksSummary {
  subjectId: number;
  subjectName: string;
  assessments: number;
  totalMarksObtained: number;
  totalMaximumMarks: number;
  percentage: number;
  resultCategory: string;
}

export interface AssessmentResult {
  markId: number;
  studentId: number;
  studentCode: string;
  studentName: string;
  marksObtained: number;
  maximumMarks: number;
  comment: string | null;
  version: number;
  updatedAtUtc: string | null;
}

export type AssessmentType = 'UnitTest' | 'MidTerm' | 'FinalExam' | 'Quiz' | 'Assignment' | 'Practical';
export type AssessmentStatus = 'Draft' | 'Published';

export interface Assessment {
  id: number;
  title: string;
  subjectId: number;
  subjectName: string;
  date: string;
  maximumMarks: number;
  assessmentType: AssessmentType;
  description: string;
  termId: number | null;
  termName: string | null;
  weight: number;
  status: AssessmentStatus;
  publishedAtUtc: string | null;
  teacherSubjectAssignmentId: number | null;
  sectionId: number | null;
  sectionName: string | null;
  gradeName: string | null;
}

export interface AssessmentInput {
  title: string;
  subjectId: number;
  date: string;
  maximumMarks: number;
  assessmentType: AssessmentType;
  description: string;
  termId?: number | null;
  weight?: number;
  publish?: boolean;
  teacherSubjectAssignmentId?: number;
}

export interface AssessmentUpdateInput {
  title: string;
  subjectId: number;
  date: string;
  maximumMarks: number;
  assessmentType: AssessmentType;
  description: string;
  termId: number | null;
  weight: number;
}

export interface BulkMarkItem {
  studentId: number;
  marksObtained: number;
  comment: string | null;
  expectedVersion?: number;
}

export interface MarkHistory {
  id: number;
  markId: number;
  previousMarks: number;
  newMarks: number;
  previousComment: string | null;
  newComment: string | null;
  version: number;
  changedByUserId: number;
  changedByName: string;
  changedAtUtc: string;
}

export interface BulkMarksInput {
  sectionId: number;
  assessmentId: number;
  marks: BulkMarkItem[];
}
