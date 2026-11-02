import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { createHandler } from '../supabase/functions/ranked/handler.mjs';
const db = new PGlite();
const a = '76561198000000001', b = '76561198000000002';
const env = { SUPABASE_URL:'https://example.supabase.co', SUPABASE_SERVICE_ROLE_KEY:'service-test',
  RANKED_SIGNING_SECRET:'shared-test-secret-at-least-32-characters', RANKED_TEST_MODE:'true' };
const request = (body, token) => new Request('https://example/ranked', { method:'POST',
  headers: token ? { Authorization:`Bearer ${token}` } : {}, body:JSON.stringify(body) });
const fetchDb = async (url, options) => {
  assert.ok(url.endsWith('/rpc/ranked_test_action'), '480 sessions must only touch test data');
  const p = JSON.parse(options.body);
  try {
    const result = await db.query('select public.ranked_test_action($1,$2,$3::jsonb) as result',
      [p.p_player,p.p_action,JSON.stringify(p.p_data)]);
    return Response.json(result.rows[0].result);
  } catch { return Response.json({error:'database'}, {status:400}); }
};
const handler = createHandler(env,fetchDb);
const call = async (action, token, data = {}) => {
  const response = await handler(request({action,...data},token));
  assert.equal(response.status,200,await response.clone().text());
  return response.json();
};
before(async () => {
  await db.exec('create role anon; create role authenticated; create role service_role;');
  for (const name of ['202609210001_ranked.sql','202609210002_ranked_test.sql'])
    await db.exec(await readFile(new URL(`../supabase/migrations/${name}`,import.meta.url),'utf8'));
});
after(async () => db.close());

test('480: two accounts authenticate without publisher keys, auto-pair, publish lobby and award isolated Elo once', async () => {
  const left = await call('auth_test',null,{steamId:a,appId:480});
  const right = await call('auth_test',null,{steamId:b,appId:480});
  assert.equal(left.testMode,true);
  const queue = {protocol:'glowglow-classic-5',region:'asia'};
  assert.equal((await call('queue',left.token,queue)).status,'searching');
  const match = await call('queue',right.token,queue);
  assert.equal(match.host,a); assert.equal(match.guest,b);
  const data = {matchId:match.matchId};
  assert.equal((await call('queue',left.token,queue)).matchId,match.matchId);
  await call('lobby',left.token,{...data,lobby:'109775240000000001'});
  assert.equal((await call('status',right.token,data)).lobby,'109775240000000001');
  await call('start',left.token,data); await call('start',right.token,data);
  await call('report',left.token,{...data,winner:a});
  assert.equal((await call('report',right.token,{...data,winner:a})).rating,984);
  assert.equal((await call('report',left.token,{...data,winner:a})).rating,1016);
  assert.equal((await call('report',left.token,{...data,winner:a})).games,1);
  for (const table of ['players','queue','matches'])
    assert.equal((await db.query(`select count(*)::int as n from ranked_${table}`)).rows[0].n,0);
});

test('480: test and production tokens cannot cross modes even with the same signing key', async () => {
  const testLogin = await call('auth_test',null,{steamId:a,appId:480});
  const prod = createHandler({...env,RANKED_TEST_MODE:'false',STEAM_APP_ID:'123456',STEAM_WEB_API_KEY:'publisher'},async url =>
    url.includes('partner.steam-api.com') ? Response.json({response:{params:{result:'OK',steamid:a}}}) : Response.json({rating:1000}));
  assert.equal((await prod(request({action:'auth_test',steamId:a,appId:480}))).status,403);
  assert.equal((await prod(request({action:'profile'},testLogin.token))).status,401);
  const prodLogin = await (await prod(request({action:'auth',ticket:'ab'.repeat(32)}))).json();
  assert.equal((await handler(request({action:'profile'},prodLogin.token))).status,401);
});

test('480: invalid identities, wrong App IDs and mixed server configuration are rejected', async () => {
  assert.equal((await handler(request({action:'auth_test',steamId:a,appId:123456}))).status,400);
  assert.equal((await handler(request({action:'auth_test',steamId:'bad',appId:480}))).status,400);
  assert.equal((await handler(request({action:'auth',ticket:'ab'.repeat(32)}))).status,400);
  assert.equal((await handler(request({action:'queue'}))).status,401);
  const mixed = createHandler({...env,STEAM_APP_ID:'123456'},fetchDb);
  assert.equal((await mixed(request({action:'auth_test',steamId:a,appId:480}))).status,503);
});

test('480: unauthenticated database roles cannot call test RPC or read test ratings', async () => {
  await db.exec('set role anon');
  await assert.rejects(db.query('select * from ranked_test_players'), /permission denied/);
  await assert.rejects(db.query("select ranked_test_action($1,'profile','{}')",[a]), /permission denied/);
  await db.exec('reset role');
});
