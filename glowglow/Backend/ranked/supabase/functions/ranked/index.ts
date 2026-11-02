import { createHandler } from './handler.mjs';

Deno.serve(createHandler({
  SUPABASE_URL: Deno.env.get('SUPABASE_URL'),
  SUPABASE_SERVICE_ROLE_KEY: Deno.env.get('SUPABASE_SERVICE_ROLE_KEY'),
  STEAM_APP_ID: Deno.env.get('STEAM_APP_ID'),
  STEAM_WEB_API_KEY: Deno.env.get('STEAM_WEB_API_KEY'),
  RANKED_SIGNING_SECRET: Deno.env.get('RANKED_SIGNING_SECRET'),
  RANKED_PROTOCOL: Deno.env.get('RANKED_PROTOCOL'),
  RANKED_TEST_MODE: Deno.env.get('RANKED_TEST_MODE'),
}));
