// Explicit manual smoke test of the deployed DEVELOPMENT endpoint only.
// Uses synthetic identities, no Steam accounts, tickets or publisher credentials.
import assert from 'node:assert/strict';
const endpoint = process.argv[2];
if (!endpoint?.startsWith('https://')) throw new Error('HTTPS ranked endpoint required');
const leftId = '00000000000000481', rightId = '00000000000000482';
const call = async (action, token, fields = {}) => {
  const response = await fetch(endpoint, {
    method:'POST', headers:{'Content-Type':'application/json',...(token ? {Authorization:`Bearer ${token}`} : {})},
    body:JSON.stringify({action,...fields}), signal:AbortSignal.timeout(20000)
  });
  const body = await response.json();
  if (!response.ok) throw new Error(`${action}: HTTP ${response.status}, ${body.error || body.message || 'request failed'}`);
  return body;
};
const left = await call('auth_test',null,{steamId:leftId,appId:480});
const right = await call('auth_test',null,{steamId:rightId,appId:480});
assert.equal(left.testMode,true); assert.equal(right.testMode,true);
const queue = {protocol:'glowglow-classic-5',region:'oceania'};
let match;
try {
  await call('queue',left.token,queue);
  match = await call('queue',right.token,queue);
  assert.equal(match.host,leftId); assert.equal(match.guest,rightId);
  const info = {matchId:match.matchId};
  assert.equal((await call('queue',left.token,queue)).matchId,match.matchId);
  await call('lobby',left.token,{...info,lobby:'1'});
  assert.equal((await call('status',right.token,info)).lobby,'1');
  await call('start',left.token,info); await call('start',right.token,info);
  await call('report',left.token,{...info,winner:leftId});
  const lost = await call('report',right.token,{...info,winner:leftId});
  const won = await call('report',left.token,{...info,winner:leftId});
  assert.equal(won.status,'complete'); assert.equal(lost.status,'complete');
  assert.equal(won.games,left.games+1); assert.equal(lost.games,right.games+1);
  assert.ok(won.rating > left.rating); assert.ok(lost.rating < right.rating);
  const repeated = await call('report',left.token,{...info,winner:leftId});
  assert.equal(repeated.rating,won.rating); assert.equal(repeated.games,won.games);
  const unauthorized = await fetch(endpoint,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action:'queue',...queue})});
  assert.equal(unauthorized.status,401);
  console.log(JSON.stringify({result:'PASS',checks:['480 auth without publisher key','two-player pairing','lobby publication','match start','result agreement','single Elo award','unauthenticated queue rejected'],winnerDelta:won.rating-left.rating,loserDelta:lost.rating-right.rating}));
} finally {
  // Test assignments only; leave no synthetic waiters for human players.
  for (const login of [left,right]) await call('cancel',login.token,{matchId:match?.matchId}).catch(() => {});
}
