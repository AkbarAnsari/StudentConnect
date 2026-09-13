export interface ParentChild {
  id: number;
  studentId: string;
  firstName: string;
  lastName: string;
  gradeName: string;
  section: string;
}

export interface Parent {
  id: number;
  username: string;
  email: string | null;
  children: ParentChild[];
}

export interface ParentCreateInput {
  username: string;
  password: string;
  email?: string;
  childStudentIds: number[];
}
