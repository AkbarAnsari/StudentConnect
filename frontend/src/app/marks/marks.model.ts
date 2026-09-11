export interface MarkRecord {
  id: number;
  assessmentId: number;
  assessmentTitle: string;
  subjectId: number;
  subjectName: string;
  assessmentDate: string;
  marksObtained: number;
  maximumMarks: number;
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
  studentName: string;
  marksObtained: number;
  maximumMarks: number;
}

export type AssessmentType = 'UnitTest' | 'MidTerm' | 'FinalExam' | 'Quiz' | 'Assignment' | 'Practical';

export interface Assessment {
  id: number;
  title: string;
  subjectId: number;
  subjectName: string;
  date: string;
  maximumMarks: number;
  assessmentType: AssessmentType;
  description: string;
}

export interface AssessmentInput {
  title: string;
  subjectId: number;
  date: string;
  maximumMarks: number;
  assessmentType: AssessmentType;
  description: string;
}

export interface BulkMarkItem {
  studentId: number;
  marksObtained: number;
}

export interface BulkMarksInput {
  assessmentId: number;
  marks: BulkMarkItem[];
}
