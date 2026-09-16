import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../auth/auth.service';

interface LeaveRequest { id:number; studentId:number; studentName:string; leaveDate:string; reason:string; status:string; decisionReason:string|null; requestedAtUtc:string; decidedAtUtc:string|null; }

@Component({ selector:'app-leave-requests', imports:[FormsModule], templateUrl:'./leave-requests.html', styleUrl:'./leave-requests.css' })
export class LeaveRequests {
  private readonly http=inject(HttpClient); protected readonly auth=inject(AuthService); private readonly url=`${environment.apiBaseUrl}/leave-requests`;
  protected readonly items=signal<LeaveRequest[]>([]); protected readonly loading=signal(true); protected readonly message=signal(''); protected readonly error=signal('');
  protected readonly studentId=signal(this.auth.currentUser()?.studentId??this.auth.currentUser()?.children?.[0]?.id??0); protected readonly leaveDate=signal(''); protected readonly reason=signal(''); protected readonly decisionReason=signal<Record<number,string>>({});
  protected readonly pending=computed(()=>this.items().filter(item=>item.status==='Pending'));
  constructor(){this.load();}
  private load():void{this.http.get<LeaveRequest[]>(this.url).subscribe({next:items=>{this.items.set(items);this.loading.set(false);},error:()=>{this.error.set('Unable to load leave requests.');this.loading.set(false);}});}
  protected submit():void{if(!this.studentId()||!this.leaveDate()||!this.reason().trim()){this.error.set('Student, date, and reason are required.');return;}this.http.post<LeaveRequest>(this.url,{studentId:this.studentId(),leaveDate:this.leaveDate(),reason:this.reason()}).subscribe({next:()=>{this.reason.set('');this.leaveDate.set('');this.message.set('Leave request submitted.');this.load();},error:(err)=>this.error.set(err.error?.message??'Unable to submit leave request.')});}
  protected setDecisionReason(id:number,value:string):void{this.decisionReason.update(items=>({...items,[id]:value}));}
  protected decide(id:number,decision:'Approved'|'Rejected'):void{this.http.put(`${this.url}/${id}/decision`,{decision,reason:this.decisionReason()[id]||null}).subscribe({next:()=>{this.message.set(`Leave request ${decision.toLowerCase()}.`);this.load();},error:(err:any)=>this.error.set(err.error?.message??'Unable to decide leave request.')});}
}
