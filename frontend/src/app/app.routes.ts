import { Routes } from '@angular/router';
import { Home } from './home/home';
import { About } from './about/about';
import { Students } from './students/students';
import { AddStudent } from './students/add-student/add-student';
import { ImportStudents } from './students/import-students/import-students';
import { EditStudent } from './students/edit-student/edit-student';
import { StudentDetail } from './students/student-detail/student-detail';
import { StudentAttendance } from './attendance/student-attendance/student-attendance';
import { SubjectAttendance } from './attendance/subject-attendance/subject-attendance';
import { RecordAttendance } from './attendance/record-attendance/record-attendance';
import { StudentMarks } from './marks/student-marks/student-marks';
import { SubjectMarks } from './marks/subject-marks/subject-marks';
import { AssessmentsList } from './marks/assessments-list/assessments-list';
import { CreateAssessment } from './marks/create-assessment/create-assessment';
import { EnterMarks } from './marks/enter-marks/enter-marks';
import { StudentHomeworkList } from './homework/student-homework/student-homework';
import { HomeworkDetail } from './homework/homework-detail/homework-detail';
import { TeacherHomework } from './homework/teacher-homework/teacher-homework';
import { HomeworkSubmissions } from './homework/homework-submissions/homework-submissions';
import { CreateHomework } from './homework/create-homework/create-homework';
import { StudentMaterials } from './learning-materials/student-materials/student-materials';
import { TeacherMaterials } from './learning-materials/teacher-materials/teacher-materials';
import { UploadMaterial } from './learning-materials/upload-material/upload-material';
import { StudentActivities } from './activities/student-activities/student-activities';
import { ActivityDetail } from './activities/activity-detail/activity-detail';
import { ActivityManagement } from './activities/activity-management/activity-management';
import { CreateActivity } from './activities/create-activity/create-activity';
import { EditActivity } from './activities/edit-activity/edit-activity';
import { ActivityRegistrationsView } from './activities/activity-registrations-view/activity-registrations-view';
import { ManageParents } from './parents/manage-parents/manage-parents';
import { AnalyticsDashboard } from './analytics/analytics-dashboard/analytics-dashboard';
import { ManageAcademicStructure } from './academic-structure/manage-academic-structure/manage-academic-structure';
import { ManageTeachers } from './teachers/manage-teachers/manage-teachers';
import { MyClasses } from './teachers/my-classes/my-classes';
import { AuditLogViewer } from './audit-log/audit-log-viewer/audit-log-viewer';
import { SearchPage } from './search/search-page/search-page';
import { StudentReportCardPage } from './reports/student-report-card/student-report-card';
import { StudentTranscriptPage } from './reports/student-transcript/student-transcript';
import { EmailOutboxViewer } from './email-outbox/email-outbox-viewer/email-outbox-viewer';
import { OpsPage } from './ops/ops-page/ops-page';
import { ComposeAnnouncement } from './announcements/compose-announcement/compose-announcement';
import { Contact } from './contact/contact';
import { Login } from './auth/login/login';
import { RegisterSchool } from './auth/register-school/register-school';
import { ForgotPassword } from './auth/forgot-password/forgot-password';
import { ResetPassword } from './auth/reset-password/reset-password';
import { VerifyEmail } from './auth/verify-email/verify-email';
import { Dashboard } from './dashboard/dashboard';
import { authGuard, roleGuard } from './auth/auth.guard';

const teacherOrAdmin = roleGuard(['Teacher', 'Admin']);
const teacherOnly = roleGuard(['Teacher']);
const adminOnly = roleGuard(['Admin']);

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'about', component: About },
  { path: 'contact', component: Contact },
  { path: 'login', component: Login },
  { path: 'register-school', component: RegisterSchool },
  { path: 'forgot-password', component: ForgotPassword },
  { path: 'reset-password/:token', component: ResetPassword },
  { path: 'verify-email/:token', component: VerifyEmail },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },

  { path: 'students', component: Students, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'students/add', component: AddStudent, canActivate: [authGuard, adminOnly] },
  { path: 'students/import', component: ImportStudents, canActivate: [authGuard, adminOnly] },
  { path: 'students/:id/edit', component: EditStudent, canActivate: [authGuard, adminOnly] },
  { path: 'students/:id/attendance', component: StudentAttendance, canActivate: [authGuard] },
  { path: 'students/:id/attendance/:subjectId', component: SubjectAttendance, canActivate: [authGuard] },
  { path: 'students/:id/marks', component: StudentMarks, canActivate: [authGuard] },
  { path: 'students/:id/marks/:subjectId', component: SubjectMarks, canActivate: [authGuard] },
  { path: 'students/:id/homework', component: StudentHomeworkList, canActivate: [authGuard] },
  { path: 'students/:id/homework/:homeworkId', component: HomeworkDetail, canActivate: [authGuard] },
  { path: 'students/:id/materials', component: StudentMaterials, canActivate: [authGuard] },
  { path: 'students/:id/activities', component: StudentActivities, canActivate: [authGuard] },
  { path: 'students/:id/activities/:activityId', component: ActivityDetail, canActivate: [authGuard] },
  { path: 'students/:id/report-card', component: StudentReportCardPage, canActivate: [authGuard] },
  { path: 'students/:id/transcript', component: StudentTranscriptPage, canActivate: [authGuard] },
  { path: 'students/:id', component: StudentDetail, canActivate: [authGuard] },

  { path: 'record-attendance', component: RecordAttendance, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'assessments', component: AssessmentsList, canActivate: [authGuard] },
  { path: 'assessments/add', component: CreateAssessment, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'enter-marks', component: EnterMarks, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'teacher-homework', component: TeacherHomework, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'create-homework', component: CreateHomework, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'homework/:id/submissions', component: HomeworkSubmissions, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'teacher-materials', component: TeacherMaterials, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'upload-material', component: UploadMaterial, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'activity-management', component: ActivityManagement, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'create-activity', component: CreateActivity, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'activity-management/:id/edit', component: EditActivity, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'activity-management/:id/registrations', component: ActivityRegistrationsView, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'manage-parents', component: ManageParents, canActivate: [authGuard, adminOnly] },
  { path: 'analytics', component: AnalyticsDashboard, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'manage-academic-structure', component: ManageAcademicStructure, canActivate: [authGuard, adminOnly] },
  { path: 'manage-teachers', component: ManageTeachers, canActivate: [authGuard, adminOnly] },
  { path: 'my-classes', component: MyClasses, canActivate: [authGuard, teacherOnly] },
  { path: 'audit-log', component: AuditLogViewer, canActivate: [authGuard, adminOnly] },
  { path: 'email-outbox', component: EmailOutboxViewer, canActivate: [authGuard, adminOnly] },
  { path: 'ops', component: OpsPage, canActivate: [authGuard, adminOnly] },
  { path: 'search', component: SearchPage, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'announcements', component: ComposeAnnouncement, canActivate: [authGuard, teacherOrAdmin] },

  { path: '**', redirectTo: '' },
];

