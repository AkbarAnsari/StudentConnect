import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { OperationalSettings, OpsInfo } from '../ops.model';
import { OpsService } from '../ops.service';

@Component({
  selector: 'app-ops-page',
  imports: [RouterLink, DatePipe, FormsModule],
  templateUrl: './ops-page.html',
  styleUrl: './ops-page.css',
})
export class OpsPage {
  private readonly opsService = inject(OpsService);

  protected readonly info = signal<OpsInfo | null>(null);
  protected readonly errorMessage = signal('');
  protected readonly loading = signal(true);
  protected readonly lastRefreshedAt = signal<Date | null>(null);
  protected readonly overallStatus = computed(() => this.info()?.overallStatus ?? 'Attention');
  protected readonly settings = signal<OperationalSettings>({alertEmail:null,alertWebhookUrl:null,failedEmailThreshold:5,auditRetentionDays:365,notificationRetentionDays:180,redactAuditDetails:true,runbookUrl:null});
  protected readonly actionMessage = signal('');

  constructor() {
    this.reload();
    this.opsService.getSettings().subscribe(settings=>this.settings.set(settings));
  }
  protected updateSetting<K extends keyof OperationalSettings>(key:K,value:OperationalSettings[K]):void{this.settings.update(item=>({...item,[key]:value}));}
  protected saveSettings():void{this.opsService.saveSettings(this.settings()).subscribe({next:()=>this.actionMessage.set('Operational settings saved.'),error:()=>this.errorMessage.set('Unable to save operational settings.')});}
  protected applyRetention():void{this.opsService.applyRetention().subscribe(result=>this.actionMessage.set(`Retention applied: ${result.auditRows} audit and ${result.notificationRows} notification row(s).`));}
  protected testAlert():void{this.opsService.testAlert().subscribe(result=>this.actionMessage.set(result.message));}

  protected reload(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.opsService.getInfo().subscribe({
      next: (info) => { this.info.set(info); this.lastRefreshedAt.set(new Date()); this.loading.set(false); },
      error: () => { this.errorMessage.set('Operations status could not be loaded.'); this.loading.set(false); },
    });
  }

  protected formatUptime(seconds: number): string {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    return `${hours}h ${minutes}m`;
  }

  protected pendingAge(createdAtUtc: string | null): string {
    if (!createdAtUtc) return 'No pending messages';
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(createdAtUtc).getTime()) / 60_000));
    return minutes < 60 ? `${minutes} minutes` : `${Math.floor(minutes / 60)}h ${minutes % 60}m`;
  }
}
