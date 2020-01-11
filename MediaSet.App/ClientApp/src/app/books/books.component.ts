import { Component, OnInit } from '@angular/core';
import { Book } from '../Models';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-books-component',
  templateUrl: './books.component.html'
})
export class BooksComponent implements OnInit {
  public books: Array<Book> = [];

  constructor(private client: HttpClient) { }

  ngOnInit(): void {
    this.client.get<Array<Book>>('api/books/getall')
      .subscribe(books => this.books = books);
  }
}
