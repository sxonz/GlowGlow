import { test, before, after, beforeEach } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';

const db = new PGlite();
const a = '76561198000000001', b = '76561198000000002', c = '76561198000000003';
const queue = { protocol: 'glowglow-classic-5', region: 'asia' };
const call = async (id, action, data = {}) => (await db.query(
  'select public.ranked_action($1,$2,$3::jsonb) as result', [id, action, JSON.stringify(data)])).rows[0].result;
const match = async () => { await call(a,'queue',queue); return await call(b,'queue',queue); };
const start = async m => {
  await call(a,'lobby',{matchId:m.matchId,lobby:'109775240000000001'});
  await call(a,'start',{matchId:m.matchId}); await call(b,'start',{matchId:m.matchId});
};
before(async () => {
  await db.exec('create role anon; create role authenticated; create role service_role;');
  await db.exec(await readFile(new URL('../supabase/migrations/202609210001_ranked.sql',import.meta.url),'utf8'));
});
beforeEach(async () => { await db.exec('truncate ranked_queue, ranked_matches, ranked_players cascade'); });
after(async () => { await db.close(); });

test('server pairs compatible players once and chooses the waiting host', async () => {
  const m = await match(); assert.equal(m.host,a); assert.equal(m.guest,b);
  assert.equal((await call(a,'queue',queue)).matchId,m.matchId);
  assert.equal((await call(c,'queue',queue)).status,'searching');
  assert.equal((await db.query('select count(*)::int as n from ranked_matches')).rows[0].n,1);
});
test('region and wire version isolate queues',async () => {
  await call(a,'queue',queue);
  assert.equal((await call(b,'queue',{...queue,region:'europe'})).status,'searching');
  assert.equal((await call(c,'queue',{...queue,protocol:'other'})).status,'searching');
});
test('ratings are server owned; both waiting windows must admit the opponent', async () => {
  await call(a,'profile'); await call(b,'profile');
  await db.query('update ranked_players set rating = 1350 where steam_id = $1',[b]);
  await call(a,'queue',{...queue,rating:1350});
  assert.equal((await call(b,'queue',queue)).status,'searching');
  await db.query("update ranked_queue set joined_at = now() - interval '90 seconds' where steam_id = $1",[a]);
  assert.equal((await call(a,'queue',queue)).status,'searching');
  await db.exec("update ranked_queue set joined_at = now() - interval '90 seconds'");
  assert.equal((await call(a,'queue',queue)).status,'reserved');
});
test('nearest eligible player wins selection; stale waiters are ignored', async () => {
  await call(a,'queue',queue);
  await db.exec("update ranked_queue set heartbeat = now() - interval '31 seconds'");
  assert.equal((await call(b,'queue',queue)).status,'searching');
  assert.equal((await call(c,'queue',queue)).host,b);
});
test('nearest rating is selected before a more distant older waiter', async () => {
  await call(a,'profile'); await call(b,'profile'); await call(c,'profile');
  await db.query('update ranked_players set rating = 1030 where steam_id = $1',[a]);
  await db.query('update ranked_players set rating = 1010 where steam_id = $1',[b]);
  await db.query('insert into ranked_queue(steam_id,protocol,region) values ($1,$3,$4),($2,$3,$4)',[a,b,queue.protocol,queue.region]);
  assert.equal((await call(c,'queue',queue)).host,b);
});
test('100 queued requests produce 50 disjoint assignments', async () => {
  const ids = Array.from({length:100},(_,i) => String(76561198000000100n + BigInt(i)));
  await Promise.all(ids.map(id => call(id,'queue',queue)));
  const matches = (await db.query('select host,guest from ranked_matches')).rows;
  assert.equal(matches.length,50);
  assert.equal(new Set(matches.flatMap(m => [m.host,m.guest])).size,100);
});
test('replayed start cannot extend the match expiry',async () => {
  const m = await match(); await start(m);
  const before = (await db.query('select expires_at from ranked_matches')).rows[0].expires_at;
  await call(a,'start',{matchId:m.matchId});
  assert.deepEqual((await db.query('select expires_at from ranked_matches')).rows[0].expires_at,before);
});
test('only assigned host can publish an immutable lobby; outsiders cannot operate matches',async () => {
  const m = await match();
  await assert.rejects(call(b,'lobby',{matchId:m.matchId,lobby:'109775240000000001'}));
  await assert.rejects(call(c,'start',{matchId:m.matchId}));
  await call(a,'lobby',{matchId:m.matchId,lobby:'109775240000000001'});
  await assert.rejects(call(a,'lobby',{matchId:m.matchId,lobby:'109775240000000002'}));
});
test('both players must start; result consensus updates Elo exactly once',async () => {
  const m = await match(); const data = {matchId:m.matchId,winner:a};
  await assert.rejects(call(a,'report',data)); await start(m);
  assert.equal((await call(a,'report',data)).rating,1000);
  const result = await call(b,'report',data); assert.equal(result.status,'complete'); assert.equal(result.rating,984);
  assert.equal((await call(a,'report',data)).rating,1016);
  assert.equal((await call(a,'profile')).games,1);
});
test('conflicting reports are disputed without rating changes',async () => {
  const m = await match(); await start(m);
  await call(a,'report',{matchId:m.matchId,winner:a});
  assert.equal((await call(b,'report',{matchId:m.matchId,winner:b})).status,'disputed');
  assert.equal((await call(a,'profile')).rating,1000);
  assert.equal((await call(b,'profile')).games,0);
});
test('queue cancellation and connection expiry do not award ratings',async () => {
  await call(a,'queue',queue); await call(a,'cancel');
  assert.equal((await call(b,'queue',queue)).status,'searching');
  const m = await call(a,'queue',queue);
  await db.exec("update ranked_matches set expires_at = now() - interval '1 second'");
  assert.equal((await call(a,'status',{matchId:m.matchId})).status,'expired');
  assert.equal((await call(a,'profile')).games,0);
});
test('explicit leave after combat start concedes; duplicate leave cannot award twice',async () => {
  const m = await match(); await start(m);
  assert.equal((await call(a,'cancel',{matchId:m.matchId})).winner,b);
  assert.equal((await call(a,'cancel',{matchId:m.matchId})).rating,984);
  assert.equal((await call(b,'profile')).games,1);
});
test('a stale cancellation cannot concede the current match',async () => {
  const m = await match(); await start(m);
  assert.equal((await call(a,'cancel',{matchId:'00000000-0000-0000-0000-000000000001'})).status,'running');
  assert.equal((await call(a,'profile')).games,0);
});
test('public clients cannot invoke privileged RPC or read ratings directly',async () => {
  await db.exec('set role anon');
  try {
    await assert.rejects(call(a,'profile'));
    await assert.rejects(db.query('select * from ranked_players'));
    await assert.rejects(db.query("select ranked_finish(gen_random_uuid(),'draw')"));
  } finally { await db.exec('reset role'); }
});
test('the Edge Function service role can call the protected RPC',async () => {
  await db.exec('set role service_role');
  try { assert.equal((await call(a,'profile')).rating,1000); }
  finally { await db.exec('reset role'); }
});
