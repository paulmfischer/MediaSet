import { Component, OnInit } from '@angular/core';
import { Book } from '../Models';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-books-component',
  templateUrl: './books.component.html'
})
export class BooksComponent implements OnInit {
  public books: Array<Book> = [];
  public pageSize = 10;
  public page = 1;

  constructor(private client: HttpClient) { }

  ngOnInit(): void {
    this.loadBooks();
  }

  loadBooks(): void {
    this.client.get<Array<Book>>('api/books/paged', { params: { skip: ((this.page - 1) * 10).toString(), take: this.pageSize.toString() } })
      .subscribe(books => this.books = books);
  }

  loadPrevious(): void {
    this.page -= 1;
    this.loadBooks();
  }

  loadNext(): void {
    this.page += 1;
    this.loadBooks();
  }
}
