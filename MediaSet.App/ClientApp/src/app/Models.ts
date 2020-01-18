export interface Book {
  Id: number;
  Title: string;
  SubTitle: string;
  Plot: string;
}

export interface PagedResult<T> {
  items: Array<T>;
  total: number;
}
