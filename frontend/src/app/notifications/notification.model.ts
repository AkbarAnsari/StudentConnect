export interface AppNotification {
  id: number;
  title: string;
  message: string;
  type: string;
  isRead: boolean;
  createdAtUtc: string;
  priority: 'Normal' | 'Important' | 'Urgent';
  actionUrl: string | null;
  announcementId: number | null;
  homeworkId: number | null;
  learningMaterialId: number | null;
}

export interface NotificationHistoryResult {
  items: AppNotification[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NotificationPreference {
  digestEnabled: boolean;
  emailChannelEnabled: boolean;
  inAppChannelEnabled: boolean;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
}
