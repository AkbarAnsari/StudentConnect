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
}

export interface ActivityInput {
  title: string;
  description: string;
  date: string;
  location: string;
  gradeId: number;
  capacity: number;
}

export interface ActivityUpdateInput {
  title: string;
  description: string;
  date: string;
  location: string;
  capacity: number;
}

export type RegistrationStatus = 'Registered' | 'Cancelled';

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
}

// StudentId is intentionally absent — the backend derives it from the authenticated user,
// so a client can never register a different student by editing this request.
export interface ActivityRegistrationInput {
  activityId: number;
}
