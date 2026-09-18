import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';

@Component({
  selector: 'app-about',
  imports: [RouterLink, NgIcon],
  templateUrl: './about.html',
  styleUrl: './about.css',
})
export class About {}
