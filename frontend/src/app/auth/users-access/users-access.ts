import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AccountService } from '../account.service';
import { UserAccess } from '../auth.model';
import { AuthService } from '../auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { Student } from '../../students/student.model';
import { StudentService } from '../../students/student.service';
import { Teacher } from '../../teachers/teacher.model';
import { TeacherService } from '../../teachers/teacher.service';

@Component({ selector: 'app-users-access', imports: [FormsModule, DatePipe], templateUrl: './users-access.html', styleUrl: './users-access.css' })
export class UsersAccess {
  private readonly accountService = inject(AccountService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly authService = inject(AuthService);
  private readonly studentService = inject(StudentService);
  private readonly teacherService = inject(TeacherService);
  protected readonly users = signal<UserAccess[]>([]);
  protected readonly loading = signal(true);
  protected readonly query = signal('');
  protected readonly roleFilter = signal('All');
  protected readonly statusFilter = signal('All');
  protected readonly actionId = signal<number | null>(null);
  protected readonly message = signal('');
  protected readonly error = signal('');
  protected readonly students = signal<Student[]>([]);
  protected readonly teachers = signal<Teacher[]>([]);
  protected readonly inviteUsername = signal('');
  protected readonly inviteEmail = signal('');
  protected readonly inviteRole = signal<'Admin' | 'Teacher' | 'Student' | 'Parent' | 'Coordinator'>('Parent');
  protected readonly inviteStudentId = signal(0);
  protected readonly inviteTeacherId = signal(0);
  protected readonly editingContactId = signal<number | null>(null);
  protected readonly contactEmail = signal('');
  protected readonly sessionUser = signal<UserAccess | null>(null);
  protected readonly sessions = signal<import('../auth.model').UserSession[]>([]);
  protected readonly sessionsLoading = signal(false);
  protected readonly filteredUsers = computed(() => { const query = this.query().trim().toLowerCase(); return this.users().filter(user => (!query || `${user.displayName} ${user.username} ${user.email ?? ''}`.toLowerCase().includes(query)) && (this.roleFilter()==='All'||user.role===this.roleFilter()) && (this.statusFilter()==='All'||user.status===this.statusFilter())); });
  protected readonly availableStudents = computed(() => this.students().filter(student => !this.users().some(user => user.studentId === student.id)));
  protected readonly availableTeachers = computed(() => this.teachers().filter(teacher => !this.users().some(user => user.teacherId === teacher.id)));
  constructor(){this.load();this.studentService.getStudents('All').subscribe(result=>this.students.set(result.items));this.teacherService.getTeachers(1,200).subscribe(result=>this.teachers.set(result.items));}
  protected isCurrentUser(user:UserAccess):boolean{return user.username===this.authService.currentUser()?.username;}
  protected async setStatus(user:UserAccess,status:'Active'|'Deactivated'):Promise<void>{const action=status==='Active'?'reactivate':'deactivate';if(!(await this.confirmDialog.confirm(`${action==='deactivate'?'Deactivate':'Reactivate'} ${user.displayName}?${action==='deactivate'?' Their refresh sessions will be revoked.':''}`)))return;this.run(user.id,action==='deactivate'?this.accountService.deactivate(user.id):this.accountService.reactivate(user.id),`${user.displayName} ${action}d.`);}
  protected sendInvitation(user:UserAccess):void{this.run(user.id,this.accountService.sendPasswordInvitation(user.id),`Password invitation sent to ${user.displayName}.`);}
  protected createInvitation():void{const role=this.inviteRole();const input={username:this.inviteUsername().trim(),email:this.inviteEmail().trim(),role,studentId:role==='Student'?this.inviteStudentId()||undefined:undefined,teacherId:role==='Teacher'?this.inviteTeacherId()||undefined:undefined};if(!input.username||!input.email){this.error.set('Username and email are required.');return;}this.run(0,this.accountService.createInvitation(input),`Invitation sent to ${input.email}.`);}
  protected startContactEdit(user:UserAccess):void{this.editingContactId.set(user.id);this.contactEmail.set(user.email??'');}
  protected saveContact(user:UserAccess):void{this.run(user.id,this.accountService.updateManagedContact(user.id,this.contactEmail().trim()||null),`Contact updated for ${user.displayName}.`);this.editingContactId.set(null);}
  protected openSessions(user:UserAccess):void{if(this.sessionUser()?.id===user.id){this.sessionUser.set(null);return;}this.sessionUser.set(user);this.sessionsLoading.set(true);this.error.set('');this.accountService.getSessions(user.id).subscribe({next:sessions=>{this.sessions.set(sessions);this.sessionsLoading.set(false);},error:()=>{this.sessionsLoading.set(false);this.error.set('Unable to load active sessions.');}});}
  protected async revokeSession(sessionId:string):Promise<void>{const user=this.sessionUser();if(!user||!(await this.confirmDialog.confirm(`Sign out this session for ${user.displayName}?`)))return;this.accountService.revokeSession(user.id,sessionId).subscribe({next:()=>{this.sessions.update(items=>items.filter(item=>item.sessionId!==sessionId));this.message.set('Session revoked.');},error:()=>this.error.set('Unable to revoke this session.')});}
  private run(id:number,request:{subscribe:(handlers:{next:()=>void;error:(error:HttpErrorResponse)=>void})=>unknown},message:string):void{this.actionId.set(id);this.error.set('');this.message.set('');request.subscribe({next:()=>{this.actionId.set(null);this.message.set(message);this.load();},error:(error)=>{this.actionId.set(null);this.error.set(error.error?.message??'Unable to update this account.');}});}
  private load():void{this.accountService.getUsers().subscribe({next:users=>{this.users.set(users);this.loading.set(false);this.inviteUsername.set('');this.inviteEmail.set('');this.inviteStudentId.set(0);this.inviteTeacherId.set(0);},error:()=>{this.error.set('Unable to load users and access.');this.loading.set(false);}});}
}
