import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { NgbPaginationModule } from '@ng-bootstrap/ng-bootstrap';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { BooksComponent } from './books/books.component';
import { BookComponent } from './books/book.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { MovieComponent } from './movies/movie.component';
import { MoviesComponent } from './movies/movies.component';
import { GamesComponent } from './games/games.component';
import { GameComponent } from './games/game.component';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    BooksComponent,
    BookComponent,
    MovieComponent,
    MoviesComponent,
    GamesComponent,
    GameComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot([
      { path: 'books', component: BooksComponent },
      { path: 'books/:id', component: BookComponent },
      { path: 'movies', component: MoviesComponent },
      { path: 'movies/:id', component: MovieComponent },
      { path: 'games', component: GamesComponent },
      { path: 'games/:id', component: GameComponent },
      //{ path: '', redirectTo: '/books', pathMatch: 'full' },
    ]),
    BrowserAnimationsModule,
    NgbPaginationModule
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }
