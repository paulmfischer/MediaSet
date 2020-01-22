import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { Game, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { MatPaginator } from '@angular/material/paginator';
import { startWith, switchMap, map, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of, merge, fromEvent } from 'rxjs';
import { Router } from '@angular/router';

@Component({
  selector: 'app-games-component',
  templateUrl: './games.component.html',
  styleUrls: ['./games.component.scss']
})
export class GamesComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'subTitle', 'platform', 'releaseDate'];
  public games: Array<Game> = [];
  
  public resultsLength: number = 0;
  public isLoading: boolean = true;

  constructor(private client: HttpClient, private router: Router) { }

  @ViewChild(MatPaginator, { static: false }) paginator: MatPaginator;
  @ViewChild('filterInput', { static: true }) filterInput: ElementRef;

  ngAfterViewInit() {
    merge(this.paginator.page, fromEvent(this.filterInput.nativeElement, 'keyup').pipe(debounceTime(300), distinctUntilChanged()))
      .pipe(
        startWith({}),
        switchMap(() => {
          return this.client.get('api/games/paged', {
            params: {
              skip: (this.paginator.pageIndex * this.paginator.pageSize).toString(),
              take: this.paginator.pageSize.toString(),
              filterValue: this.filterInput.nativeElement.value
            }
          });
        }),
        map((data: PagedResult<Game>) => {
          this.isLoading = false;
          this.resultsLength = data.total;

          return data.items;
        }),
        catchError(() => {
          this.isLoading = false;

          return of([]);
        })
      )
      .subscribe(data => this.games = data);
  }

  onRowClicked(row: Game) {
    this.router.navigate(['/games', row.id]);
  }
}
