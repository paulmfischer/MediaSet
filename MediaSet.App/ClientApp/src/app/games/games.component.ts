import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { Game, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { startWith, switchMap, map, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of, merge, fromEvent, Subject } from 'rxjs';
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
  public page: number = 1;
  public pageSize: number = 15;
  public isLoading: boolean = true;
  private pageChange: Subject<number> = new Subject<number>();

  @ViewChild('filterInput', { static: true }) filterInput: ElementRef;

  constructor(private client: HttpClient, private router: Router) { }

  ngAfterViewInit() {
    merge(this.pageChange, fromEvent(this.filterInput.nativeElement, 'keyup').pipe(debounceTime(300), distinctUntilChanged()))
      .pipe(
        startWith({}),
        switchMap(() => {
          return this.client.get('api/games/paged', {
            params: {
              skip: (this.page * this.pageSize).toString(),
              take: this.pageSize.toString(),
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

  pageChanged(page) {
    this.pageChange.next(page);
  }

  onRowClicked(row: Game) {
    this.router.navigate(['/games', row.id]);
  }
}
