export type ActivityStatus = 'Draft' | 'Open' | 'Closed' | 'Cancelled' | 'Completed' | 'Archived';

export interface Activity {
  id: number;
  title: string;
  description: string;
  date: string;
  location: string;
  gradeId: number;
  gradeName: string;
  capacity: number;
  currentRegistrations: number;
  availableSeats: number;
  currentWaitlisted: number;
  sectionId: number | null;
  sectionName: string | null;
  ownerUserId: number | null;
  organizerName: string;
  contactEmail: string | null;
  registrationOpensAtUtc: string | null;
  registrationClosesAtUtc: string | null;
  consentRequired: boolean;
  feeAmount: number | null;
  moderationStatus: 'Pending' | 'Approved' | 'Rejected';
  moderationReason: string | null;
  moderatedAtUtc: string | null;
  status: ActivityStatus;
  cancelledAtUtc: string | null;
  completedAtUtc: string | null;
}

export interface ActivityInput {
  title: string;
  description: string;
  date: string;
  location: string;
  gradeId: number;
  capacity: number;
  sectionId: number | null;
  organizerName: string;
  contactEmail: string | null;
  registrationOpensAtUtc: string | null;
  registrationClosesAtUtc: string | null;
  consentRequired: boolean;
  feeAmount: number | null;
  status: 'Draft' | 'Open';
}

export interface ActivityUpdateInput {
  title: string;
  description: string;
  date: string;
  location: string;
  capacity: number;
  sectionId: number | null;
  organizerName: string;
  contactEmail: string | null;
  registrationOpensAtUtc: string | null;
  registrationClosesAtUtc: string | null;
  consentRequired: boolean;
  feeAmount: number | null;
}

export type RegistrationStatus = 'Registered' | 'Cancelled' | 'Waitlisted';

export interface ActivityRegistration {
  id: number;
  studentId: number;
  studentName: string;
  activityId: number;
  activityTitle: string;
  activityDate: string;
  location: string;
  registrationDate: string;
  status: RegistrationStatus;
  consentConfirmed: boolean;
  checkedInAtUtc: string | null;
  feeStatus: 'NotApplicable' | 'Pending' | 'Paid';
  paidAtUtc: string | null;
  paymentReference: string | null;
  consentDocumentFileName: string | null;
  consentDocumentUploadedAtUtc: string | null;
}

// StudentId is intentionally absent — the backend derives it from the authenticated user,
// so a client can never register a different student by editing this request.
export interface ActivityRegistrationInput {
  activityId: number;
  consentConfirmed: boolean;
}
