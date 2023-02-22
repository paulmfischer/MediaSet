import { BadRequestError } from "./request.ts";

export interface BookItem {
  id: number;
  title: string;
  publishDate: string;
  numberOfPages: number;
  isbn: string;
}

export type NewBook = Partial<Omit<BookItem, 'id'>>;

export interface BookOperationProps {
  book: BookItem;
  errors?: BadRequestError;
}