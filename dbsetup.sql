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

create table if not exists groups (
    id uuid default gen_random_uuid() primary key,
    group_id string not null,
    name string not null,
    creation_time timestamp default current_timestamp()
);

upsert into groups (name, group_id) values ('Norge', 'Norge');

create table if not exists group_admins (
    group_id uuid references groups(id),
    user_id string references users(id),
    constraint "primary" primary key (group_id, user_id)
);

create table if not exists group_members (
    groupId uuid references groups(id),
    user_id string references users(id),
    entered_at timestamp default current_timestamp(),
    left_at timestamp null,
    constraint "primary" primary key (group_id, user_id, entered_at)
);