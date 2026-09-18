import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon } from '@ng-icons/core';

@Component({
  selector: 'app-contact',
  imports: [RouterLink, NgIcon],
  templateUrl: './contact.html',
  styleUrl: './contact.css',
})
export class Contact {}
