import { HttpErrorResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BehaviorSubject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { triggerBlobDownload } from '../../shared/download.util';
import { ReportIssuanceSummary, ReportPeriod, ReportTemplate, StudentReportCard } from '../report.model';
import { ReportService } from '../report.service';

interface ReportCardState {
  loading: boolean;
  error: string;
  reportCard: StudentReportCard | null;
}

@Component({
  selector: 'app-student-report-card',
  imports: [DatePipe, FormsModule, RouterLink],
  templateUrl: './student-report-card.html',
  styleUrl: './student-report-card.css',
})
export class StudentReportCardPage {
  private readonly route = inject(ActivatedRoute);
  private readonly reportService = inject(ReportService);
  protected readonly authService = inject(AuthService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  protected readonly studentId = Number(this.route.snapshot.paramMap.get('id'));
  protected readonly periods = signal<ReportPeriod[]>([]);
  protected readonly history = signal<ReportIssuanceSummary[]>([]);
  protected readonly selectedTermId = signal<number | null>(Number(this.route.snapshot.queryParamMap.get('termId')) || null);
  protected readonly selectedIssuanceId = signal<number | null>(Number(this.route.snapshot.queryParamMap.get('issuanceId')) || null);
  protected readonly comments = signal('');
  protected readonly issuing = signal(false);
  protected readonly issueError = signal('');
  protected readonly exportError = signal('');
  protected readonly templates = signal<ReportTemplate[]>([]);
  protected readonly selectedTemplateId = signal<number | null>(null);
  protected readonly signatureLabel = signal('Principal');
  protected readonly signedName = signal('');
  protected readonly canIssue = computed(() => this.authService.role() === 'Teacher' || this.authService.role() === 'Admin');
  private readonly selection$ = new BehaviorSubject<{ termId: number | null; issuanceId: number | null }>({
    termId: this.selectedTermId(),
    issuanceId: this.selectedIssuanceId(),
  });

  protected readonly state = signal<ReportCardState>({ loading: true, error: '', reportCard: null });

  constructor() {
    this.selection$.pipe(
      switchMap(({ termId, issuanceId }) => this.reportService.getStudentReportCard(this.studentId, termId, issuanceId).pipe(
        map((reportCard): ReportCardState => ({ loading: false, error: '', reportCard })),
        catchError((err: HttpErrorResponse) => of<ReportCardState>({
          loading: false,
          error: err.error?.message ?? 'Unable to load the report card.',
          reportCard: null,
        })),
        startWith<ReportCardState>({ loading: true, error: '', reportCard: null }),
      )),
    ).subscribe((state) => this.state.set(state));

    this.reportService.getReportPeriods().subscribe((periods) => {
      this.periods.set(periods);
      if (this.selectedTermId() === null && this.selectedIssuanceId() === null && this.canIssue()) {
        const today = new Date().toISOString().slice(0, 10);
        const current = periods.find((period) => period.startDate <= today && period.endDate >= today) ?? periods[0];
        if (current) this.onPeriodChange(current.id);
      }
    });
    this.loadHistory();
    if(this.authService.role()==='Admin')this.reportService.getTemplates().subscribe(items=>{this.templates.set(items);this.selectedTemplateId.set(items.find(item=>item.isDefault)?.id??null);});
  }

  protected onPeriodChange(termId: number | null): void {
    this.selectedTermId.set(termId);
    this.selectedIssuanceId.set(null);
    this.selection$.next({ termId, issuanceId: null });
  }

  protected viewIssuance(item: ReportIssuanceSummary): void {
    this.selectedTermId.set(item.termId);
    this.selectedIssuanceId.set(item.id);
    this.selection$.next({ termId: item.termId, issuanceId: item.id });
  }

  protected viewDraft(): void {
    this.selectedIssuanceId.set(null);
    this.selection$.next({ termId: this.selectedTermId(), issuanceId: null });
  }

  protected retry(): void {
    this.selection$.next(this.selection$.value);
  }

  protected async issue(): Promise<void> {
    const termId = this.selectedTermId();
    if (!termId || !(await this.confirmDialog.confirm('Issue this report card? The finalized snapshot cannot be changed.'))) return;

    this.issuing.set(true);
    this.issueError.set('');
    this.reportService.issueStudentReportCard(this.studentId, termId, this.comments(), this.selectedTemplateId()).subscribe({
      next: (reportCard) => {
        this.issuing.set(false);
        this.comments.set('');
        this.selectedIssuanceId.set(reportCard.issuanceId);
        this.selection$.next({ termId, issuanceId: reportCard.issuanceId });
        this.loadHistory();
      },
      error: (err: HttpErrorResponse) => {
        this.issuing.set(false);
        this.issueError.set(err.error?.message ?? 'Unable to issue the report card.');
      },
    });
  }

  protected sign():void{const issuanceId=this.selectedIssuanceId();if(!issuanceId||!this.signatureLabel().trim()||!this.signedName().trim())return;this.reportService.signIssuance(issuanceId,this.signatureLabel(),this.signedName()).subscribe(()=>{this.signedName.set('');this.loadHistory();});}

  protected print(): void {
    window.print();
  }

  protected downloadCsv(): void {
    this.exportError.set('');
    this.reportService.exportStudentReportCard(this.studentId, this.selectedTermId(), this.selectedIssuanceId()).subscribe({
      next: (blob) => triggerBlobDownload(blob, `report-card-${this.studentId}.csv`),
      error: () => this.exportError.set('The report export could not be downloaded.'),
    });
  }

  private loadHistory(): void {
    this.reportService.getReportCardHistory(this.studentId).subscribe({ next: (history) => this.history.set(history) });
  }
}
