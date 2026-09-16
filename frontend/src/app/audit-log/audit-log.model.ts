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

export interface AuditLogFilters {
  page: number;
  pageSize: number;
  entityType: string;
  action: string;
  username: string;
  query: string;
  fromUtc: string;
  toUtc: string;
}
