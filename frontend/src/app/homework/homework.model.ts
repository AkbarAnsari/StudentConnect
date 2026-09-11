export type HomeworkStatus = 'Not Submitted' | 'Submitted' | 'Late';

export interface StudentHomework {
  id: number;
  title: string;
  subjectName: string;
  dueDate: string;
  status: HomeworkStatus;
}

export interface Homework {
  id: number;
  title: string;
  description: string;
  subjectId: number;
  subjectName: string;
  gradeId: number;
  gradeName: string;
  teacherId: number;
  teacherName: string;
  createdDate: string;
  dueDate: string;
}

export interface HomeworkInput {
  title: string;
  description: string;
  subjectId: number;
  gradeId: number;
  teacherId: number;
  dueDate: string;
}

export interface HomeworkUpdateInput {
  title: string;
  description: string;
  dueDate: string;
}

export interface HomeworkSubmission {
  id: number;
  homeworkId: number;
  homeworkTitle: string;
  studentId: number;
  studentName: string;
  submittedDate: string;
  status: HomeworkStatus;
  comments: string;
}

export interface SubmissionOverview {
  studentId: number;
  studentName: string;
  status: HomeworkStatus;
  submittedDate: string | null;
  comments: string | null;
}

// StudentId is intentionally absent — the backend derives it from the authenticated user.
export interface HomeworkSubmissionInput {
  homeworkId: number;
  comments: string;
}
