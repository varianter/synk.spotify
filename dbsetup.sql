 create table if not exists users (
    id string primary key,
    last_sync timestamp
);

create table if not exists tokens (
    id uuid default gen_random_uuid() primary key,
    user_id string references users(id),
    access_token string not null,
    refresh_token string not null
);

create table if not exists artists (
    id string primary key,
    name string not null,
    image_url string null
);

create table if not exists albums (
    id string primary key,
    name string not null,
    image_url string not null,
    vibrant_color string null
);

create table if not exists tracks (
    id string primary key,
    name string not null,
    album_id string not null references albums(id),
    duration int not null,
    genre string null,
);

create table if not exists track_artists (
    track_id string references tracks(id),
    artist_id string references artists(id),
    constraint "primary" primary key (track_id, artist_id)
);

create table if not exists played_tracks (
    user_id string not null references users(id),
    played_at timestamp not null,
    track_id string not null references tracks(id),
    constraint "primary" primary key (userId, played_at, track_id)
);
