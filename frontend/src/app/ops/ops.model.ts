export interface OpsInfo {
  environmentName: string;
  version: string;
  serverTimeUtc: string;
  uptimeSeconds: number;
  databaseHealthy: boolean;
  pendingEmailCount: number;
  failedEmailCount: number;
}
