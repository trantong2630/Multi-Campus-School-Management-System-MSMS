import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { registerHooks } from 'node:module';
import { setImmediate } from 'node:timers/promises';
import axios, { AxiosError } from 'axios';
import { QueryClient } from '@tanstack/react-query';
import {
  AUTH_LOST_EVENT, apiClient, endMatchingSession, getErrorMessage,
  loginSession, logoutSession, refreshSession, tokenStore,
} from '../src/app/apiClient.ts';
import { clearSessionQueries, publishSessionEnd, subscribeSessionEnd } from '../src/app/authSync.ts';

const originalAdapter = axios.defaults.adapter;
const originalApiAdapter = apiClient.defaults.adapter;
const originalLocks = Object.getOwnPropertyDescriptor(navigator, 'locks');
const originalWindow = Object.getOwnPropertyDescriptor(globalThis, 'window');
const originalBroadcast = globalThis.BroadcastChannel;
let testNumber = 0;

function sid(value) { return `${testNumber}:${value}`; }

function token(sessionId, signature = 'original') {
  return `header.${Buffer.from(JSON.stringify({ sid: sid(sessionId) })).toString('base64url')}.${signature}`;
}

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

function response(config, accessToken) {
  return { config, status: 200, statusText: 'OK', headers: {}, data: { token: accessToken, user: { accountId: 1 } } };
}

function failure(status, config) {
  return new AxiosError('request failed', 'ERR_BAD_REQUEST', config, undefined, { status, data: {}, headers: {} });
}

function createCache() {
  const client = new QueryClient({ defaultOptions: { queries: { gcTime: Infinity } } });
  client.setQueryData(['students'], [{ name: 'Private student data' }]);
  window.addEventListener(AUTH_LOST_EVENT, () => clearSessionQueries(client));
  return client;
}

beforeEach(() => {
  testNumber++;
  Object.defineProperty(navigator, 'locks', { configurable: true, value: undefined });
  Object.defineProperty(globalThis, 'window', { configurable: true, value: new EventTarget() });
  globalThis.BroadcastChannel = undefined;
  tokenStore.set(token('session-a'));
});

afterEach(() => {
  axios.defaults.adapter = originalAdapter;
  apiClient.defaults.adapter = originalApiAdapter;
  if (originalLocks) Object.defineProperty(navigator, 'locks', originalLocks);
  else delete navigator.locks;
  if (originalWindow) Object.defineProperty(globalThis, 'window', originalWindow);
  else delete globalThis.window;
  globalThis.BroadcastChannel = originalBroadcast;
  tokenStore.clear();
});

test('confirmed204 deduplicates logout, blocks refresh, cancels queries and clears cached data', async () => {
  const client = createCache();
  let cancelled = false;
  const fetch = client.fetchQuery({
    queryKey: ['pending-private-data'],
    queryFn: ({ signal }) => new Promise((_resolve, reject) => {
      signal.addEventListener('abort', () => { cancelled = true; reject(new Error('aborted')); });
    }),
  }).catch(() => {});
  const transport = deferred();
  let requests = 0;
  const send = (accessToken) => {
    requests++;
    assert.equal(accessToken, token('session-a'));
    return transport.promise;
  };
  const first = logoutSession(send);
  assert.equal(logoutSession(send), first);
  assert.equal(requests, 1);
  assert.ok(tokenStore.get());
  assert.ok(client.getQueryData(['students']));
  await assert.rejects(refreshSession(), { name: 'CanceledError' });
  transport.resolve({ status: 204 });
  await first;
  await fetch;
  assert.equal(tokenStore.get(), null);
  assert.equal(cancelled, true);
  assert.equal(client.getQueryCache().getAll().length, 0);
});

for (const [name, error] of [
  ['network', new AxiosError('offline', 'ERR_NETWORK')],
  ['timeout', new AxiosError('timed out', 'ECONNABORTED')],
  ['server500', failure(500)],
]) {
  test(`${name} logout failure retains the token and cache, and permits retry`, async () => {
    const client = createCache();
    let lost = 0;
    window.addEventListener(AUTH_LOST_EVENT, () => lost++);
    await assert.rejects(logoutSession(() => Promise.reject(error)), (received) => received === error);
    assert.equal(tokenStore.get(), token('session-a'));
    assert.ok(client.getQueryData(['students']));
    assert.equal(lost, 0);
    await logoutSession(() => Promise.resolve({ status: 204 }));
    assert.equal(tokenStore.get(), null);
    assert.equal(lost, 1);
  });
}

test('expired access401 refreshes once and retries logout with the renewed access token', async () => {
  const client = createCache();
  let refreshes = 0;
  axios.defaults.adapter = (config) => {
    refreshes++;
    assert.equal(config.url, '/api/auth/refresh');
    assert.equal(config.timeout, 10_000);
    return Promise.resolve(response(config, token('session-a', 'renewed')));
  };
  const attempts = [];
  await logoutSession(async (accessToken) => {
    attempts.push(accessToken);
    assert.ok(client.getQueryData(['students']), 'the cache remains available until logout is confirmed');
    if (attempts.length === 1) throw failure(401);
    return { status: 204 };
  });
  assert.deepEqual(attempts, [token('session-a'), token('session-a', 'renewed')]);
  assert.equal(refreshes, 1);
  assert.equal(tokenStore.get(), null);
  assert.equal(client.getQueryData(['students']), undefined);
});

test('refresh401 confirms that the session is gone without a second logout request', async () => {
  const client = createCache();
  let attempts = 0;
  axios.defaults.adapter = () => Promise.reject(failure(401));
  await logoutSession(() => { attempts++; return Promise.reject(failure(401)); });
  assert.equal(attempts, 1);
  assert.equal(tokenStore.get(), null);
  assert.equal(client.getQueryData(['students']), undefined);
});

test('a second logout401 after refresh is terminal, and does not refresh repeatedly', async () => {
  let refreshes = 0;
  let attempts = 0;
  axios.defaults.adapter = (config) => {
    refreshes++;
    return Promise.resolve(response(config, token('session-a', 'renewed')));
  };
  await logoutSession(() => { attempts++; return Promise.reject(failure(401)); });
  assert.equal(attempts, 2);
  assert.equal(refreshes, 1);
  assert.equal(tokenStore.get(), null);
});

test('refresh network failure after expired access does not pretend logout succeeded', async () => {
  const client = createCache();
  axios.defaults.adapter = () => Promise.reject(new AxiosError('offline', 'ERR_NETWORK'));
  await assert.rejects(logoutSession(() => Promise.reject(failure(401))), { code: 'ERR_NETWORK' });
  assert.equal(tokenStore.get(), token('session-a'));
  assert.ok(client.getQueryData(['students']));
  client.clear();
});

test('refreshing a different cookie session never logs that newer session out', async () => {
  let attempts = 0;
  axios.defaults.adapter = (config) => Promise.resolve(response(config, token('new-session-b')));
  await assert.rejects(logoutSession(() => {
    attempts++;
    return Promise.reject(failure(401));
  }), (error) => getErrorMessage(error) === 'Phiên đăng nhập đã thay đổi. Tải lại trang để tiếp tục.');
  assert.equal(attempts, 1);
  assert.equal(tokenStore.get(), token('session-a'));
});

test('a refresh response arriving after successful logout cannot restore the token', async () => {
  const transport = deferred();
  let config;
  axios.defaults.adapter = (value) => { config = value; return transport.promise; };
  const refresh = refreshSession();
  const rejected = assert.rejects(refresh, { name: 'CanceledError' });
  await logoutSession(() => Promise.resolve({ status: 204 }));
  transport.resolve(response(config, token('session-a', 'late-refresh')));
  await rejected;
  assert.equal(tokenStore.get(), null);
});

test('queued refresh is discarded before transport when logout supersedes it', async () => {
  const heldLock = deferred();
  let queue = heldLock.promise;
  Object.defineProperty(navigator, 'locks', {
    configurable: true,
    value: {
      request(_name, _options, action) {
        const task = queue.then(action);
        queue = task.catch(() => {});
        return task;
      },
    },
  });
  let refreshes = 0;
  axios.defaults.adapter = () => { refreshes++; throw new Error('must not refresh'); };
  const refresh = refreshSession();
  const rejected = assert.rejects(refresh, { name: 'CanceledError' });
  const logout = logoutSession(() => Promise.resolve({ status: 204 }));
  heldLock.resolve();
  await Promise.all([rejected, logout]);
  assert.equal(refreshes, 0);
  assert.equal(tokenStore.get(), null);
});

test('an earlier login response cannot restore a session after logout', async () => {
  const transport = deferred();
  const login = loginSession(() => transport.promise);
  const rejected = assert.rejects(login, { name: 'CanceledError' });
  await logoutSession(() => Promise.resolve({ status: 204 }));
  transport.resolve({ token: token('session-a'), user: { accountId: 1 } });
  await rejected;
  assert.equal(tokenStore.get(), null);
});

test('an old logout response cannot clear a newer login', async () => {
  const transport = deferred();
  const logout = logoutSession(() => transport.promise);
  await loginSession(() => Promise.resolve({ token: token('session-b'), user: { accountId: 2 } }));
  transport.resolve({ status: 204 });
  await logout;
  assert.equal(tokenStore.get(), token('session-b'));
});

for (const newerLogin of [false, true]) {
  test(`a delayed protected401 cannot ${newerLogin ? 'clear a newer login' : 'refresh after logout'}`, async () => {
    const transport = deferred();
    let config;
    let refreshes = 0;
    apiClient.defaults.adapter = (value) => { config = value; return transport.promise; };
    axios.defaults.adapter = () => { refreshes++; throw new Error('must not refresh'); };
    const request = apiClient.get('/protected');
    const rejected = assert.rejects(request, (error) => error.response?.status === 401);
    await setImmediate();
    if (newerLogin) await loginSession(() => Promise.resolve({ token: token('session-b'), user: { accountId: 2 } }));
    else await logoutSession(() => Promise.resolve({ status: 204 }));
    transport.reject(failure(401, config));
    await rejected;
    assert.equal(refreshes, 0);
    assert.equal(tokenStore.get(), newerLogin ? token('session-b') : null);
  });
}

test('confirmed AUTH_LOST from a protected request cancels and clears private cache', async () => {
  const client = createCache();
  apiClient.defaults.adapter = (config) => Promise.reject(failure(401, config));
  axios.defaults.adapter = () => Promise.reject(failure(401));
  await assert.rejects(apiClient.get('/protected'));
  assert.equal(tokenStore.get(), null);
  assert.equal(client.getQueryData(['students']), undefined);
});

test('tab messages clear only matching sessions and contain no auth tokens', async () => {
  const messages = [];
  const channels = new Set();
  globalThis.BroadcastChannel = class {
    constructor() { channels.add(this); }
    postMessage(message) {
      messages.push(message);
      for (const channel of channels) if (channel !== this) queueMicrotask(() => channel.onmessage?.({ data: message }));
    }
    close() { channels.delete(this); }
  };
  const sibling = await import('../src/app/apiClient.ts?logout-tab=sibling');
  const newer = await import('../src/app/apiClient.ts?logout-tab=newer');
  sibling.tokenStore.set(token('session-a'));
  newer.tokenStore.set(token('session-b'));
  const cleanups = [subscribeSessionEnd(endMatchingSession), subscribeSessionEnd(sibling.endMatchingSession), subscribeSessionEnd(newer.endMatchingSession)];
  publishSessionEnd(sid('session-a'));
  await setImmediate();
  assert.equal(tokenStore.get(), null);
  assert.equal(sibling.tokenStore.get(), null);
  assert.equal(newer.tokenStore.get(), token('session-b'));
  assert.deepEqual(Object.keys(messages[0]).sort(), ['eventId', 'sessionId']);
  assert.equal(messages[0].sessionId, sid('session-a'));
  cleanups.forEach((cleanup) => cleanup());
});

test('unavailable randomUUID cannot turn confirmed logout into a failure', async (context) => {
  context.mock.method(crypto, 'randomUUID', () => { throw new Error('unavailable'); });
  const messages = [];
  globalThis.BroadcastChannel = class {
    postMessage(message) { messages.push(message); }
    close() {}
  };
  await logoutSession(() => Promise.resolve({ status: 204 }));
  assert.equal(tokenStore.get(), null);
  assert.equal(messages[0].sessionId, sid('session-a'));
  assert.equal(typeof messages[0].eventId, 'string');
});

test('the feature logout request has a timeout and uses its current-session bearer snapshot', async () => {
  const hook = registerHooks({
    resolve(specifier, context, nextResolve) {
      if (specifier === '@/app/apiClient') return nextResolve(new URL('../src/app/apiClient.ts', import.meta.url).href, context);
      return nextResolve(specifier, context);
    },
  });
  const { iamApi } = await import('../src/features/iam/api.ts');
  hook.deregister();
  let attempts = 0;
  apiClient.defaults.adapter = (config) => {
    attempts++;
    assert.equal(config.url, '/auth/logout');
    assert.equal(config.timeout, 10_000);
    assert.equal(config.headers.Authorization, `Bearer ${token('session-a')}`);
    return Promise.resolve({ config, status: 204, data: undefined, headers: {}, statusText: 'No Content' });
  };
  await iamApi.logout();
  assert.equal(attempts, 1);
  assert.equal(tokenStore.get(), null);
});

test('a logout event during startup refresh prevents that revoked session being adopted without Web Locks', async () => {
  const startup = await import('../src/app/apiClient.ts?logout-tab=startup');
  startup.tokenStore.clear();
  const transport = deferred();
  let config;
  axios.defaults.adapter = (value) => { config = value; return transport.promise; };
  const refresh = startup.refreshSession();
  const rejected = assert.rejects(refresh, { name: 'CanceledError' });
  assert.equal(startup.tokenStore.get(), null);
  startup.endMatchingSession(sid('session-a'));
  transport.resolve(response(config, token('session-a', 'delayed-startup')));
  await rejected;
  assert.equal(startup.tokenStore.get(), null);
});

test('storage fallback transmits only session metadata and ignores an older session logout', () => {
  const writes = [];
  window.localStorage = {
    setItem(key, newValue) {
      writes.push(JSON.parse(newValue));
      window.dispatchEvent(Object.assign(new Event('storage'), { key, newValue }));
    },
    removeItem() {},
  };
  const cleanup = subscribeSessionEnd(endMatchingSession);
  publishSessionEnd(sid('session-a'));
  assert.equal(tokenStore.get(), null);
  assert.deepEqual(Object.keys(writes[0]).sort(), ['eventId', 'sessionId']);
  tokenStore.set(token('session-b'));
  publishSessionEnd(sid('session-a'));
  assert.equal(tokenStore.get(), token('session-b'));
  cleanup();
});
