export interface OpsInfo {
  environmentName: string;
  version: string;
  serverTimeUtc: string;
  uptimeSeconds: number;
  databaseHealthy: boolean;
  databaseResponseMilliseconds: number;
  pendingEmailCount: number;
  failedEmailCount: number;
  sentEmailCountLast24Hours: number;
  oldestPendingEmailAtUtc: string | null;
  lastEmailSentAtUtc: string | null;
  lastEmailFailureAtUtc: string | null;
  overallStatus: 'Healthy' | 'Processing' | 'Attention';
  runbookUrl: string | null;
}

export interface OperationalSettings { alertEmail:string|null;alertWebhookUrl:string|null;failedEmailThreshold:number;auditRetentionDays:number;notificationRetentionDays:number;redactAuditDetails:boolean;runbookUrl:string|null; }
