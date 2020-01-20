import { Component, OnInit } from '@angular/core';
import { Movie } from '../Models';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { tap } from 'rxjs/operators';

@Component({
  selector: 'app-movie-component',
  templateUrl: './movie.component.html'
})
export class MovieComponent implements OnInit {
  public movie: Movie;
  public movieId: number;
  public isLoading: boolean = true;

  constructor(private client: HttpClient, private activeRoute: ActivatedRoute) { }

  ngOnInit(): void {
    this.movieId = this.activeRoute.snapshot.params['id'];
    this.client.get<Movie>(`api/movies/${this.movieId}`)
      .pipe(tap(() => this.isLoading = false, () => this.isLoading = false))
      .subscribe(movie => this.movie = movie);
  }
}
