import { Component, OnInit } from '@angular/core';
import {  Game } from '../Models';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { tap } from 'rxjs/operators';

@Component({
  selector: 'app-game-component',
  templateUrl: './game.component.html'
})
export class GameComponent implements OnInit {
  public game: Game;
  public gameId: number;
  public isLoading: boolean = true;

  constructor(private client: HttpClient, private activeRoute: ActivatedRoute) { }

  ngOnInit(): void {
    this.gameId = this.activeRoute.snapshot.params['id'];
    this.client.get<Game>(`api/games/${this.gameId}`)
      .pipe(tap(() => this.isLoading = false, () => this.isLoading = false))
      .subscribe(game => this.game = game);
  }
}
