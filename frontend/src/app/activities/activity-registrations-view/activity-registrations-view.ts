import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, combineLatest, map, of, startWith } from 'rxjs';
import { Activity, ActivityRegistration } from '../activity.model';
import { ActivityService } from '../activity.service';
import { ActivityRegistrationService } from '../activity-registration.service';

interface RegistrationsState {
  loading: boolean;
  error: boolean;
  activity: Activity | null;
  registrations: ActivityRegistration[];
}

const LOADING_STATE: RegistrationsState = { loading: true, error: false, activity: null, registrations: [] };

@Component({
  selector: 'app-activity-registrations-view',
  imports: [RouterLink],
  templateUrl: './activity-registrations-view.html',
  styleUrl: './activity-registrations-view.css',
})
export class ActivityRegistrationsView {
  private readonly route = inject(ActivatedRoute);
  private readonly activityService = inject(ActivityService);
  private readonly registrationService = inject(ActivityRegistrationService);

  protected readonly activityId = Number(this.route.snapshot.paramMap.get('id'));

  protected readonly state = toSignal(
    combineLatest([
      this.activityService.getById(this.activityId),
      this.registrationService.getForActivity(this.activityId),
    ]).pipe(
      map(([activity, registrations]): RegistrationsState => ({ loading: false, error: false, activity, registrations })),
      catchError(() => of<RegistrationsState>({ loading: false, error: true, activity: null, registrations: [] })),
      startWith(LOADING_STATE),
    ),
    { initialValue: LOADING_STATE },
  );
}

