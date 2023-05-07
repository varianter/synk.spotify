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

create table if not exists recentlyPlayed (
    userId string not null references users(id),
    playedAt timestamp not null,
    trackId string not null,
    constraint "primary" primary key (userId, playedAt, trackId)
);
