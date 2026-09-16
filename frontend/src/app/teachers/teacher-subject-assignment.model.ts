export interface TeacherSubjectAssignment {
  id: number;
  teacherId: number;
  teacherName: string;
  subjectId: number;
  subjectName: string;
  sectionId: number;
  sectionName: string;
  gradeId: number;
  gradeName: string;
}

export interface TeacherSubjectAssignmentInput {
  teacherId: number;
  subjectId: number;
  sectionId: number;
}
