import { Component, ViewChild, AfterViewInit, ElementRef } from '@angular/core';
import { Book, PagedResult } from '../Models';
import { HttpClient } from '@angular/common/http';
import { startWith, switchMap, map, catchError, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { of, merge, fromEvent, from, Subject } from 'rxjs';
import { Router } from '@angular/router';

@Component({
  selector: 'app-books-component',
  templateUrl: './books.component.html',
  styleUrls: ['./books.component.scss']
})
export class BooksComponent implements AfterViewInit {
  public displayedColumns: string[] = ['title', 'subTitle', 'numberOfPages', 'publicationDate'];
  public books: Array<Book> = [];
  public filter: string = '';
  
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
          return this.client.get('api/books/paged', {
            params: {
              skip: (this.page * this.pageSize).toString(),
              take: this.pageSize.toString(),
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

  pageChanged(page) {
    this.pageChange.next(page);
  }

  onRowClicked(row: Book) {
    console.log('book', row);
    //this.router.navigate(['/books', row.id]);
  }
}
