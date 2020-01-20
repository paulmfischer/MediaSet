import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { Book, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { MatPaginator } from '@angular/material/paginator';
import { startWith, switchMap, map, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of, merge, fromEvent } from 'rxjs';
import { MatSort } from '@angular/material/sort';

@Component({
  selector: 'app-books-component',
  templateUrl: './books.component.html',
  styleUrls: ['./books.component.css']
})
export class BooksComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'subTitle', 'numberOfPages', 'publicationDate'];
  public books: Array<Book> = [];
  public filter: string = '';
  
  public resultsLength: number = 0;
  public isLoading: boolean = true;

  constructor(private client: HttpClient) { }

  @ViewChild(MatPaginator, { static: false }) paginator: MatPaginator;
  @ViewChild(MatSort, { static: true }) sort: MatSort;
  @ViewChild('filterInput', { static: true }) filterInput: ElementRef;

  ngAfterViewInit() {
    merge(this.paginator.page, fromEvent(this.filterInput.nativeElement, 'keyup').pipe(debounceTime(300), distinctUntilChanged()))
      .pipe(
        startWith({}),
        switchMap(() => {
          return this.client.get('api/books/paged', {
            params: {
              skip: (this.paginator.pageIndex * this.paginator.pageSize).toString(),
              take: this.paginator.pageSize.toString(),
              filterValue: this.filterInput.nativeElement.value
            }
          });
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

  onRowClicked(row) {
    console.log('Row clicked: ', row);
  }
}
