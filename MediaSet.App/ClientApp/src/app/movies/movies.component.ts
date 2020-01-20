import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { Movie, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { MatPaginator } from '@angular/material/paginator';
import { startWith, switchMap, map, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of, merge, fromEvent } from 'rxjs';

@Component({
  selector: 'app-movies-component',
  templateUrl: './movies.component.html',
  styleUrls: ['./movies.component.css']
})
export class MoviesComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'studio', 'releaseDate', 'format'];
  public movies: Array<Movie> = [];
  
  public resultsLength: number = 0;
  public isLoading: boolean = true;

  constructor(private client: HttpClient) { }

  @ViewChild(MatPaginator, { static: false }) paginator: MatPaginator;
  @ViewChild('filterInput', { static: true }) filterInput: ElementRef;

  ngAfterViewInit() {
    merge(this.paginator.page, fromEvent(this.filterInput.nativeElement, 'keyup').pipe(debounceTime(300), distinctUntilChanged()))
      .pipe(
        startWith({}),
        switchMap(() => {
          return this.client.get('api/movies/paged', {
            params: {
              skip: (this.paginator.pageIndex * this.paginator.pageSize).toString(),
              take: this.paginator.pageSize.toString(),
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
}
