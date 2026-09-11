import { Routes } from '@angular/router';
import { Home } from './home/home';
import { About } from './about/about';
import { Students } from './students/students';
import { AddStudent } from './students/add-student/add-student';
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
import { Contact } from './contact/contact';
import { Login } from './auth/login/login';
import { Dashboard } from './dashboard/dashboard';
import { authGuard, roleGuard } from './auth/auth.guard';

const teacherOrAdmin = roleGuard(['Teacher', 'Admin']);
const adminOnly = roleGuard(['Admin']);

export const routes: Routes = [
  { path: '', component: Home },
  { path: 'about', component: About },
  { path: 'contact', component: Contact },
  { path: 'login', component: Login },
  { path: 'dashboard', component: Dashboard, canActivate: [authGuard] },

  { path: 'students', component: Students, canActivate: [authGuard, teacherOrAdmin] },
  { path: 'students/add', component: AddStudent, canActivate: [authGuard, adminOnly] },
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

  { path: '**', redirectTo: '' },
];

