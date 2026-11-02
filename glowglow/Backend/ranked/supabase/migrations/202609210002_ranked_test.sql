-- App ID 480 development data only. Separate from verified production ranks.
-- Only the authenticated Edge Function may call these RPCs. No client-supplied ratings.
begin;
revoke create on schema public from public, anon, authenticated;
create table public.ranked_test_players (
  steam_id text primary key check (steam_id ~ '^[0-9]{17}$'),
  rating integer not null default 1000 check (rating between 0 and 4000),
  games integer not null default 0,
  wins integer not null default 0,
  active_match uuid,
  updated_at timestamptz not null default now()
);
create table public.ranked_test_queue (
  steam_id text primary key references public.ranked_test_players,
  protocol text not null,
  region text not null,
  joined_at timestamptz not null default now(),
  heartbeat timestamptz not null default now()
);
create index ranked_test_queue_search on public.ranked_test_queue(protocol, region, heartbeat);
create table public.ranked_test_matches (
  id uuid primary key default gen_random_uuid(),
  host text not null references public.ranked_test_players,
  guest text not null references public.ranked_test_players,
  host_rating integer not null,
  guest_rating integer not null,
  state text not null default 'reserved' check (state in ('reserved','running','complete','disputed','cancelled','expired')),
  lobby text not null default '',
  host_started boolean not null default false,
  guest_started boolean not null default false,
  host_result text,
  guest_result text,
  winner text,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default now() + interval '3 minutes',
  finished_at timestamptz,
  check (host <> guest)
);
create index ranked_test_matches_expiry on public.ranked_test_matches(expires_at) where state in ('reserved','running');
alter table public.ranked_test_players enable row level security;
alter table public.ranked_test_queue enable row level security;
alter table public.ranked_test_matches enable row level security;
revoke all on public.ranked_test_players, public.ranked_test_queue, public.ranked_test_matches from anon, authenticated;

create function public.ranked_test_window(p_since timestamptz) returns integer
language sql stable set search_path = public as $$
  select least(600, 100 + greatest(0, floor(extract(epoch from (now() - p_since)) / 15))::integer * 50)
$$;

create function public.ranked_test_view(p_player text, p_match uuid default null) returns jsonb
language plpgsql stable set search_path = public as $$
declare p ranked_test_players; m ranked_test_matches; q ranked_test_queue;
begin
  select * into p from ranked_test_players where steam_id = p_player;
  if p_match is not null then
    select * into m from ranked_test_matches where id = p_match and p_player in (host,guest);
  end if;
  if m.id is not null then
    return jsonb_build_object('status',m.state,'rating',p.rating,'games',p.games,
      'matchId',m.id,'host',m.host,'guest',m.guest,'lobby',m.lobby,'winner',coalesce(m.winner,''));
  end if;
  select * into q from ranked_test_queue where steam_id = p_player;
  return jsonb_build_object('status',case when q.steam_id is null then 'idle' else 'searching' end,
    'rating',p.rating,'games',p.games,'range',case when q.steam_id is null then 100 else ranked_test_window(q.joined_at) end);
end $$;

create function public.ranked_test_finish(p_match uuid, p_winner text) returns void
language plpgsql set search_path = public as $$
declare m ranked_test_matches; delta integer; score numeric;
begin
  select * into m from ranked_test_matches where id = p_match for update;
  if m.state = 'complete' then return; end if;
  if m.state not in ('reserved','running') or p_winner not in (m.host,m.guest,'draw') then
    raise exception 'Invalid match result';
  end if;
  score := case when p_winner = m.host then 1 when p_winner = 'draw' then 0.5 else 0 end;
  delta := round(32 * (score - 1 / (1 + power(10::numeric,(m.guest_rating-m.host_rating)/400.0))));
  update ranked_test_players set rating = greatest(0,least(4000,rating + delta)), games = games + 1,
    wins = wins + case when p_winner = m.host then 1 else 0 end, updated_at = now() where steam_id = m.host;
  update ranked_test_players set rating = greatest(0,least(4000,rating - delta)), games = games + 1,
    wins = wins + case when p_winner = m.guest then 1 else 0 end, updated_at = now() where steam_id = m.guest;
  update ranked_test_matches set state = 'complete', winner = p_winner, finished_at = now() where id = p_match;
end $$;

create function public.ranked_test_action(p_player text, p_action text, p_data jsonb default '{}') returns jsonb
language plpgsql security definer set search_path = public as $$
declare p ranked_test_players; q ranked_test_queue; candidate record; m ranked_test_matches;
  target uuid; protocol text; region text; outcome text; lobby_id text;
begin
  -- Small-population queue: serialize short transactions to prevent double pairing and double awards.
  perform pg_advisory_xact_lock(724022);
  if p_player !~ '^[0-9]{17}$' then raise exception 'Invalid Steam identity'; end if;
  insert into ranked_test_players(steam_id) values(p_player) on conflict do nothing;
  update ranked_test_matches set state = 'expired', finished_at = now()
    where state in ('reserved','running') and expires_at < now();
  delete from ranked_test_queue where heartbeat < now() - interval '30 seconds';
  select * into p from ranked_test_players where steam_id = p_player;
  if p.active_match is not null then select * into m from ranked_test_matches where id = p.active_match; end if;

  if p_action = 'queue' then
    if m.id is not null and m.state in ('reserved','running') then return ranked_test_view(p_player,m.id); end if;
    update ranked_test_players set active_match = null where steam_id = p_player;
    protocol := p_data->>'protocol'; region := p_data->>'region';
    if protocol is null or length(protocol) > 64 or region not in ('asia','europe','americas','oceania') then
      raise exception 'Invalid queue';
    end if;
    insert into ranked_test_queue(steam_id,protocol,region) values(p_player,protocol,region)
      on conflict(steam_id) do update set heartbeat = now(),
        joined_at = case when ranked_test_queue.protocol <> excluded.protocol or ranked_test_queue.region <> excluded.region
          then now() else ranked_test_queue.joined_at end,
        protocol = excluded.protocol, region = excluded.region;
    select * into q from ranked_test_queue where steam_id = p_player;
    select other.steam_id, rp.rating into candidate from ranked_test_queue other
      join ranked_test_players rp on rp.steam_id = other.steam_id
      where other.steam_id <> p_player and other.protocol = q.protocol and other.region = q.region
        and rp.active_match is null
        and abs(rp.rating-p.rating) <= least(ranked_test_window(q.joined_at),ranked_test_window(other.joined_at))
      order by abs(rp.rating-p.rating), other.joined_at, other.steam_id limit 1;
    if found then
      insert into ranked_test_matches(host,guest,host_rating,guest_rating)
        values(candidate.steam_id,p_player,candidate.rating,p.rating) returning * into m;
      update ranked_test_players set active_match = m.id where steam_id in (m.host,m.guest);
      delete from ranked_test_queue where steam_id in (m.host,m.guest);
      return ranked_test_view(p_player,m.id);
    end if;
    return ranked_test_view(p_player);
  end if;

  if p_action = 'cancel' then
    delete from ranked_test_queue where steam_id = p_player;
    -- A delayed leave request from an older match cannot concede a newer assignment.
    if m.id is not null and (p_data->>'matchId') is distinct from m.id::text then
      return ranked_test_view(p_player,m.id);
    end if;
    if m.state = 'reserved' and not m.host_started and not m.guest_started then
      update ranked_test_matches set state = 'cancelled', finished_at = now() where id = m.id;
    elsif m.state in ('reserved','running') then
      -- Explicitly leaving after a player has started is a concession by the caller.
      perform ranked_test_finish(m.id,case when p_player = m.host then m.guest else m.host end);
    end if;
    return ranked_test_view(p_player,m.id);
  end if;
  if p_action = 'profile' then return ranked_test_view(p_player); end if;
  target := nullif(p_data->>'matchId','')::uuid;
  if target is null or m.id is null or target <> m.id or p_player not in (m.host,m.guest) then
    raise exception 'Match is not assigned to this player';
  end if;
  if p_action = 'status' then return ranked_test_view(p_player,m.id); end if;
  if m.state not in ('reserved','running') then return ranked_test_view(p_player,m.id); end if;
  if p_action = 'lobby' then
    lobby_id := p_data->>'lobby';
    if p_player <> m.host or lobby_id is null or lobby_id !~ '^[0-9]{1,20}$' or lobby_id = '0'
       or (m.lobby <> '' and m.lobby <> lobby_id) then raise exception 'Invalid lobby owner or lobby'; end if;
    update ranked_test_matches set lobby = lobby_id where id = m.id;
  elsif p_action = 'start' then
    if m.lobby = '' then raise exception 'Lobby not published'; end if;
    if (p_player = m.host and m.host_started) or (p_player = m.guest and m.guest_started) then
      return ranked_test_view(p_player,m.id);
    end if;
    update ranked_test_matches set host_started = host_started or p_player = host,
      guest_started = guest_started or p_player = guest where id = m.id returning * into m;
    if m.host_started and m.guest_started then
      update ranked_test_matches set state = 'running', expires_at = now() + interval '30 minutes' where id = m.id;
    end if;
  elsif p_action = 'report' then
    outcome := p_data->>'winner';
    if m.state <> 'running' or outcome is null or outcome not in (m.host,m.guest,'draw') then
      raise exception 'Match has not started or winner is invalid';
    end if;
    if p_player = m.host then
      if m.host_result is not null and m.host_result <> outcome then raise exception 'Result already submitted'; end if;
      update ranked_test_matches set host_result = outcome where id = m.id;
    else
      if m.guest_result is not null and m.guest_result <> outcome then raise exception 'Result already submitted'; end if;
      update ranked_test_matches set guest_result = outcome where id = m.id;
    end if;
    select * into m from ranked_test_matches where id = m.id;
    if m.host_result is not null and m.guest_result is not null then
      if m.host_result = m.guest_result then perform ranked_test_finish(m.id,m.host_result);
      else update ranked_test_matches set state = 'disputed', finished_at = now() where id = m.id; end if;
    end if;
  else raise exception 'Unknown action';
  end if;
  return ranked_test_view(p_player,m.id);
end $$;

revoke all on function public.ranked_test_window(timestamptz), public.ranked_test_view(text,uuid),
  public.ranked_test_finish(uuid,text), public.ranked_test_action(text,text,jsonb) from public, anon, authenticated;
grant execute on function public.ranked_test_action(text,text,jsonb) to service_role;
commit;

