import { Component, ViewChild, AfterViewInit } from '@angular/core';
import { Game, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { MatPaginator } from '@angular/material/paginator';
import { startWith, switchMap, map, catchError } from 'rxjs/operators';
import { of, from } from 'rxjs';

@Component({
  selector: 'app-games-component',
  templateUrl: './games.component.html',
  styleUrls: ['./games.component.css']
})
export class GamesComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'subTitle', 'platform', 'releaseDate'];
  public games: Array<Game> = [];
  
  public resultsLength: number = 0;
  public isLoading: boolean = true;

  constructor(private client: HttpClient) { }

  @ViewChild(MatPaginator, { static: false }) paginator: MatPaginator;

  ngAfterViewInit() {
    from(this.paginator.page)
      .pipe(
        startWith({}),
        switchMap(() => {
          return this.client.get('api/games/paged', { params: { skip: (this.paginator.pageIndex * this.paginator.pageSize).toString(), take: this.paginator.pageSize.toString() } });
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
}
