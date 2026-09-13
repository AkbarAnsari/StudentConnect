export interface AuditLog {
  id: number;
  timestampUtc: string;
  username: string;
  role: string;
  action: string;
  entityType: string;
  entityId: string;
  details: string | null;
}
