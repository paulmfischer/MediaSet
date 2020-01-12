import { Component, OnInit } from '@angular/core';
import { Book } from '../Models';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { tap } from 'rxjs/operators';

@Component({
  selector: 'app-book-component',
  templateUrl: './book.component.html'
})
export class BookComponent implements OnInit {
  public book: Book;
  public bookId: number;
  public isLoading: boolean = true;

  constructor(private client: HttpClient, private activeRoute: ActivatedRoute) { }

  ngOnInit(): void {
    this.bookId = this.activeRoute.snapshot.params['id'];
    this.client.get<Book>(`api/books/${this.bookId}`)
      .pipe(tap(() => this.isLoading = false, () => this.isLoading = false))
      .subscribe(book => this.book = book);
  }
}
