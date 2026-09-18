import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';

@Component({
  selector: 'app-home',
  imports: [RouterLink, NgIcon],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {}
