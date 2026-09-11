export interface AppNotification {
  id: number;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAtUtc: string;
}
