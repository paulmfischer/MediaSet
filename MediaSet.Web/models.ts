export interface Book {
  id?: string;
  title?: string;
  isbn?: string;
  format?: string;
  pages?: number;
  publicationDate?: string;
  author?: string[];
  publisher?: string[];
  genre?: string[];
  plot?: string;
  subTitle?: string;
}