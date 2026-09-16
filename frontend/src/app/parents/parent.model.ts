export interface ParentChild {
  id: number;
  studentId: string;
  firstName: string;
  lastName: string;
  gradeName: string;
  section: string;
  relationshipType: string;
  isPrimaryContact: boolean;
}

export interface ParentStudentHistory {
  id: number;
  studentId: number;
  studentName: string;
  action: string;
  relationshipType: string;
  isPrimaryContact: boolean;
  reason: string | null;
  changedByName: string;
  changedAtUtc: string;
}

export interface Parent {
  id: number;
  username: string;
  email: string | null;
  status: 'Active' | 'Deactivated';
  children: ParentChild[];
}

export interface ParentCreateInput {
  username: string;
  password: string;
  email?: string;
  childStudentIds: number[];
}
