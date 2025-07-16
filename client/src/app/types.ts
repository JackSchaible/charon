export interface Game {
  AppId: string;
  Title: string;
  ImageUrl: string;
  Price: string;
  ReleaseDate: string;
  LastFetched: string;
}

export interface GameTag {
  GameId: string;
  TagId: number;
}

export interface User {
  Id: number;
  SteamId: string;
  CreatedAt: string;
  AvatarUrl: string;
  ProfileUrl: string;
  Username: string;
}

export interface UserTag {
  Id: number;
  UserId: number;
  Name: string;
  Description: string;
}

export interface WishlistItem {
  UserId: number;
  GameId: string;
  Notes: string;
  AddedAt: string;
}
