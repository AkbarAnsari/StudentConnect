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
  children: ParentChild[];
}

export interface ParentCreateInput {
  username: string;
  password: string;
  childStudentIds: number[];
}
