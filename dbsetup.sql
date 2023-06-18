 create table if not exists users (
    id string primary key,
    lastSync timestamp
);

create table if not exists tokens (
    id uuid default gen_random_uuid() primary key,
    userId string references users(id),
    accessToken string not null,
    refreshToken string not null,
    expiresAt timestamp not null
);

create table if not exists artists (
    id string primary key,
    name string not null,
    imageUrl string null
);

create table if not exists albums (
    id string primary key,
    name string not null,
    imageUrl string not null,
    vibrant_color string null
);

create table if not exists tracks (
    id string primary key,
    name string not null,
    albumId string not null references albums(id),
    duration int not null,
    genre string null,
);

create table if not exists trackArtists (
    trackId string references tracks(id),
    artistId string references artists(id),
    constraint "primary" primary key (trackId, artistId)
);

create table if not exists recentlyPlayed (
    userId string not null references users(id),
    playedAt timestamp not null,
    trackId string not null references tracks(id),
    constraint "primary" primary key (userId, playedAt, trackId)
);
