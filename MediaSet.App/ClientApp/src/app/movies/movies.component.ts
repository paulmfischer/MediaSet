import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { Movie, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { startWith, switchMap, map, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of, merge, fromEvent, Subject } from 'rxjs';
import { Router } from '@angular/router';

@Component({
  selector: 'app-movies-component',
  templateUrl: './movies.component.html',
  styleUrls: ['./movies.component.scss']
})
export class MoviesComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'studio', 'releaseDate', 'format'];
  public movies: Array<Movie> = [];
  
  public resultsLength: number = 0;
  public page: number = 1;
  public pageSize: number = 15;
  public isLoading: boolean = true;
  private pageChange: Subject<number> = new Subject<number>();

  constructor(private client: HttpClient, private router: Router) { }

  @ViewChild('filterInput', { static: true }) filterInput: ElementRef;

  ngAfterViewInit() {
    this.pageChange
      .pipe(
        startWith({}),
        switchMap(() => {
          return this.client.get('api/movies/paged', {
            params: {
              skip: (this.page * this.pageSize).toString(),
              take: this.pageSize.toString(),
              filterValue: this.filterInput.nativeElement.value
            }
          });
        }),
        map((data: PagedResult<Movie>) => {
          this.isLoading = false;
          this.resultsLength = data.total;

          return data.items;
        }),
        catchError(() => {
          this.isLoading = false;

          return of([]);
        })
      )
      .subscribe(data => this.movies = data);
  }

  pageChanged(page) {
    this.pageChange.next(page);
  }

  onRowClicked(row: Movie) {
    console.log('movie', row);
    //this.router.navigate(['/movies', row.id]);
  }
}
