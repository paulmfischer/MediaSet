import { Component, ViewChild, AfterViewInit } from '@angular/core';
import { Book, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { MatPaginator } from '@angular/material/paginator';
import { startWith, switchMap, map, catchError, tap } from 'rxjs/operators';
import { of, merge } from 'rxjs';
//import { MatInput } from '@angular/material/input';
//import { MatTableDataSource } from '@angular/material/table';

@Component({
  selector: 'app-books-component',
  templateUrl: './books.component.html',
  styleUrls: ['./books.component.css']
})
export class BooksComponent implements AfterViewInit {
  public displayedColumns: string[] = ['id', 'title', 'subTitle', 'numberOfPages', 'publicationDate'];
  public books: Array<Book> = [];
  //public source: MatTableDataSource<Book> = new MatTableDataSource<book>([]);
  
  public resultsLength: number = 0;
  public isLoading: boolean = true;

  constructor(private client: HttpClient) { }

  @ViewChild(MatPaginator, { static: false }) paginator: MatPaginator;
  //@ViewChild(MatInput, { static: true }) filter: MatInput;

  ngAfterViewInit() {
    of(this.paginator.page)
    //merge(this.paginator.page, this.source.filter) //, this.filter.value)
      .pipe(
        startWith({}),
        switchMap(() => {
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

  //applyFilter(filterValue) {
  //  console.log('filterValue', filterValue);
  //  this.source.filter = filterValue;
  //}
}
