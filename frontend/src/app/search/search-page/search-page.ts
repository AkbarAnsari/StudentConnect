import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { SearchResult } from '../search.model';
import { SearchService } from '../search.service';

@Component({
  selector: 'app-search-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './search-page.html',
  styleUrl: './search-page.css',
})
export class SearchPage {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);

  protected readonly query = new FormControl('', { nonNullable: true });

  protected readonly results = toSignal(
    this.query.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap((query) => (query.trim().length >= 2 ? this.searchService.search(query.trim()) : of<SearchResult[]>([]))),
    ),
    { initialValue: [] },
  );

  protected open(result: SearchResult): void {
    switch (result.entityType) {
      case 'Student':
        this.router.navigate(['/students', result.entityId]);
        break;
      case 'Teacher':
        this.router.navigate(['/manage-teachers']);
        break;
      case 'Homework':
        this.router.navigate(['/homework', result.entityId, 'submissions']);
        break;
      case 'LearningMaterial':
        this.router.navigate(['/teacher-materials']);
        break;
      case 'Activity':
        this.router.navigate(['/activity-management', result.entityId, 'edit']);
        break;
    }
  }
}
