export interface SearchResult {
  entityType: 'Student' | 'Teacher' | 'Homework' | 'LearningMaterial' | 'Activity';
  entityId: number;
  title: string;
  subtitle: string;
}
