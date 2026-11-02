import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHandler } from '../supabase/functions/ranked/handler.mjs';
const id = '76561198000000001';
const env = { SUPABASE_URL:'https://example.supabase.co', SUPABASE_SERVICE_ROLE_KEY:'test-service-key',
  STEAM_APP_ID:'123456', STEAM_WEB_API_KEY:'test-steam-key', RANKED_SIGNING_SECRET:'test-only-secret-at-least-32-characters' };
const request = (body,token) => new Request('https://example/ranked',{method:'POST',
  headers:token ? {Authorization:`Bearer ${token}`} : {}, body:JSON.stringify(body)});
test('Steam identity is verified, bearer signatures checked, and client identity/rating ignored',async () => {
  const calls = []; let time = 1000;
  const handler = createHandler(env,async (url,options) => {
    if (url.startsWith('https://partner.steam-api.com/')) return Response.json({response:{params:{result:'OK',steamid:id}}});
    calls.push(JSON.parse(options.body)); return Response.json({rating:1000,games:0,status:'searching'});
  },() => time);
  const auth = await handler(request({action:'auth',ticket:'ab'.repeat(32),steamId:'76561198999999999'}));
  const login = await auth.json(); assert.equal(login.steamId,id);
  assert.equal((await handler(request({action:'queue',protocol:'glowglow-classic-5',region:'asia',rating:4000,steamId:'bad'},login.token))).status,200);
  assert.equal(calls.at(-1).p_player,id); assert.equal(calls.at(-1).p_data.rating,undefined);
  const parts = login.token.split('.'); parts[1] = btoa(JSON.stringify({sub:'76561198999999999',aud:'glowglow-ranked',exp:99999})).replace(/=/g,'');
  assert.equal((await handler(request({action:'profile'},parts.join('.')))).status,401);
  time += 7201; assert.equal((await handler(request({action:'profile'},login.token))).status,401);
});
test('Steam failures, development App ID, invalid bodies and oversized requests fail closed',async () => {
  const handler = createHandler(env,async () => Response.json({response:{error:{errorcode:101}}}));
  assert.equal((await handler(request({action:'auth',ticket:'ab'.repeat(32)}))).status,401);
  assert.equal((await handler(request({action:'auth',ticket:'not-hex'}))).status,400);
  assert.equal((await handler(request({action:'queue'}))).status,401);
  assert.equal((await handler(request({action:'admin'}))).status,400);
  assert.equal((await handler(request({payload:'x'.repeat(13000)}))).status,413);
  assert.equal((await createHandler({...env,STEAM_APP_ID:'480'})(request({action:'profile'}))).status,503);
});
