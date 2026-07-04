create database POLOGAME_DB;
Use POLOGAME_DB;

create table users(
    user_id       int identity(1,1) primary key,
    username      varchar(50)  not null unique,
    created_at    datetime     not null default getdate(),
    last_played   datetime     NULL
);

create table matches(
    match_id      int identity(1,1) primary key,
    user_id       int          not null,
    difficulty    varchar(10)  not null check (difficulty in ('low', 'medium', 'hard')),
    total_rounds  int          not null,
    winner_team   varchar(10)  null     check (winner_team in ('blue', 'red', 'draw')),
    played_at     datetime     not null default getdate(),
 
    constraint fk_matches_user foreign key (user_id) references users(user_id)
);

create table rounds (
    round_id         int identity(1,1) primary key,
    match_id         int not null,
    round_number     int not null,
    blue_goals       int not null default 0,
    red_goals        int not null default 0,
    winner_team      varchar(10)  null check (winner_team in ('blue', 'red', 'draw')),
    duration_seconds int not null default 0,
 
    constraint fk_rounds_match foreign key (match_id) references matches(match_id)
);


create table goal_log(
    goal_id              int identity(1,1) primary key,
    round_id             int not null,
    scoring_team         varchar(10)  not null check (scoring_team in ('blue', 'red')),
    scoring_player_name  varchar(20)  null,
    scoring_player_role  varchar(20)   null check (scoring_player_role in ('defender', 'scorer', 'captain', 'helper')),
    goal_minute          int not null default 0,
 
    constraint fk_goallog_round foreign key (round_id) references rounds(round_id)
);


create table player_stats(
    stat_id        int identity(1,1) primary key,
    user_id        int not null,
    player_name    varchar(20)  not null,
    player_role    varchar(20)  not null check (player_role in ('defender', 'scorer', 'captain', 'helper')),
    total_goals    int not null default 0,
    total_hits     int not null default 0,
    matches_played int not null default 0,
 
    constraint fk_playerstats_user foreign key (user_id) references users(user_id)
);

select * from users;
select * from matches;
select * from rounds;
select * from goal_log;
select * from player_stats;


