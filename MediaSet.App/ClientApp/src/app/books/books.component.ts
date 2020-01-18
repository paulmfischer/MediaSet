import { Component, ViewChild, AfterViewInit } from '@angular/core';
import { Book, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { MatPaginator } from '@angular/material/paginator';
import { startWith, switchMap, map, catchError, merge } from 'rxjs/operators';
import { of, from } from 'rxjs';

@Component({
  selector: 'app-books-component',
  templateUrl: './books.component.html'
})
export class BooksComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'numberOfPages', 'publicationDate'];
  public books: Array<Book> = [];

  public resultsLength: number = 0;
  public isLoading: boolean = true;

  constructor(private client: HttpClient) { }

  @ViewChild(MatPaginator, { static: false }) paginator: MatPaginator;

  ngAfterViewInit() {
    from(this.paginator.page)
      .pipe(
        startWith({}),
        switchMap(() => {
          this.isLoading = true;
          return this.client.get('api/books/paged', { params: { skip: (this.paginator.pageIndex * this.paginator.pageSize).toString(), take: this.paginator.pageSize.toString() } });
        }),
        map((data: PagedResult<Book>) => {
          this.isLoading = false;
          this.resultsLength = data.total;

          return data.items;
        }),
        catchError(() => {
          this.isLoading = false;
          return of([]);
        })
      )
      .subscribe(data => this.books = data);
  }
}
