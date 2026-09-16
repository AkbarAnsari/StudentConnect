import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../auth/auth.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { StudentTranscript, TranscriptIssuanceSummary } from '../report.model';
import { ReportService } from '../report.service';

interface TranscriptState {
  loading: boolean;
  error: boolean;
  transcript: StudentTranscript | null;
}

const LOADING_STATE: TranscriptState = { loading: true, error: false, transcript: null };

@Component({
  selector: 'app-student-transcript',
  imports: [RouterLink, DatePipe],
  templateUrl: './student-transcript.html',
  styleUrl: './student-transcript.css',
})
export class StudentTranscriptPage {
  private readonly route = inject(ActivatedRoute);
  private readonly reportService = inject(ReportService);
  protected readonly auth = inject(AuthService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = signal<TranscriptState>(LOADING_STATE);
  protected readonly history = signal<TranscriptIssuanceSummary[]>([]);
  protected readonly selectedIssuanceId = signal<number | null>(null);
  protected readonly issuing = signal(false);

  constructor(){this.load();this.reportService.getTranscriptHistory(this.studentId).subscribe(items=>this.history.set(items));}

  protected load(issuanceId?:number):void{this.state.set(LOADING_STATE);this.reportService.getStudentTranscript(this.studentId,issuanceId).subscribe({next:transcript=>{this.selectedIssuanceId.set(issuanceId??null);this.state.set({loading:false,error:false,transcript});},error:()=>this.state.set({loading:false,error:true,transcript:null})});}
  protected issue():void{this.issuing.set(true);this.reportService.issueTranscript(this.studentId).subscribe({next:item=>{this.issuing.set(false);this.history.update(items=>[item,...items]);this.load(item.id);},error:()=>{this.issuing.set(false);}});}

  protected print(): void {
    window.print();
  }

  protected downloadCsv(): void {
    this.reportService.exportStudentTranscript(this.studentId).subscribe((blob) => triggerBlobDownload(blob, `transcript-${this.studentId}.csv`));
  }
}
