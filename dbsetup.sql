 create table if not exists users (
    id string primary key,
    lastSync timestamp
);

create table if not exists tokens (
    id int primary key,
    userId string not null references users(id),
    accessToken string,
    refreshToken string,
    expiresAt timestamp
);
