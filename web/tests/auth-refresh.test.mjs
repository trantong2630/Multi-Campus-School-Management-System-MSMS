import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { setImmediate } from 'node:timers/promises';
import axios, { AxiosError } from 'axios';
import { AUTH_REQUEST_TIMEOUT_MS, getErrorMessage, refreshSession, tokenStore } from '../src/app/apiClient.ts';

const originalAdapter = axios.defaults.adapter;
const originalLocks = Object.getOwnPropertyDescriptor(navigator, 'locks');

function setLocks(locks) {
  Object.defineProperty(navigator, 'locks', { configurable: true, value: locks });
}

function response(config, token) {
  return {
    config, status: 200, statusText: 'OK', headers: {},
    data: { token, expiresAt: '2026-10-27T01:15:00Z', user: { accountId: 1 } },
  };
}

afterEach(() => {
  axios.defaults.adapter = originalAdapter;
  if (originalLocks) Object.defineProperty(navigator, 'locks', originalLocks);
  else delete navigator.locks;
  tokenStore.clear();
});

test('concurrent refresh callers share one request and keep the access token in memory', async () => {
  setLocks(undefined);
  let finish;
  let calls = 0;
  axios.defaults.adapter = (config) => {
    calls++;
    assert.equal(config.url, '/api/auth/refresh');
    assert.equal(config.data, '{}');
    assert.equal(config.withCredentials, true);
    assert.equal(config.timeout, 10_000);
    return new Promise((resolve) => { finish = () => resolve(response(config, 'new-access')); });
  };

  const first = refreshSession();
  const second = refreshSession();
  assert.equal(first, second);
  await setImmediate();
  assert.equal(calls, 1);
  assert.equal(tokenStore.get(), null);
  finish();
  assert.equal((await first).token, 'new-access');
  assert.equal((await second).token, 'new-access');
  assert.equal(tokenStore.get(), 'new-access');
});

test('a failed refresh releases the shared request so a later attempt can succeed', async () => {
  setLocks(undefined);
  axios.defaults.adapter = () => Promise.reject(new Error('offline'));
  await assert.rejects(refreshSession(), /offline/);
  assert.equal(tokenStore.get(), null);

  axios.defaults.adapter = (config) => Promise.resolve(response(config, 'retry-access'));
  assert.equal((await refreshSession()).token, 'retry-access');
  assert.equal(tokenStore.get(), 'retry-access');
});

test('separate tabs serialize cookie rotation through the same browser lock', async () => {
  const names = [];
  let queue = Promise.resolve();
  setLocks({
    request(name, _options, callback) {
      names.push(name);
      const task = queue.then(callback);
      queue = task.catch(() => {});
      return task;
    },
  });
  let active = 0;
  let maximumActive = 0;
  let cookieVersion = 0;
  axios.defaults.adapter = async (config) => {
    maximumActive = Math.max(maximumActive, ++active);
    const readVersion = cookieVersion;
    await setImmediate();
    assert.equal(cookieVersion, readVersion, 'another tab must not rotate the cookie during this request');
    cookieVersion++;
    active--;
    return response(config, `access-${cookieVersion}`);
  };

  // Separate module instances model each tab's own in-memory token and request promise.
  const tabOne = await import('../src/app/apiClient.ts?tab=one');
  const tabTwo = await import('../src/app/apiClient.ts?tab=two');
  const sessions = await Promise.all([tabOne.refreshSession(), tabTwo.refreshSession()]);
  assert.equal(maximumActive, 1);
  assert.deepEqual(names, ['msms:refresh-session', 'msms:refresh-session']);
  assert.deepEqual(sessions.map((session) => session.token), ['access-1', 'access-2']);
  assert.equal(tabOne.tokenStore.get(), 'access-1');
  assert.equal(tabTwo.tokenStore.get(), 'access-2');
});

test('an unavailable browser lock times out shared callers and allows a later refresh', async (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });
  let requests = 0;
  let receivedSignal;
  setLocks({
    request(_name, { signal }) {
      receivedSignal = signal;
      return new Promise((_resolve, reject) => {
        signal.addEventListener('abort', () => reject(signal.reason), { once: true });
      });
    },
  });
  axios.defaults.adapter = (config) => {
    requests++;
    return Promise.resolve(response(config, 'after-lock-timeout'));
  };

  const first = refreshSession();
  assert.equal(refreshSession(), first);
  const rejection = assert.rejects(first, { name: 'AbortError' });
  context.mock.timers.tick(AUTH_REQUEST_TIMEOUT_MS - 1);
  assert.equal(receivedSignal.aborted, false);
  context.mock.timers.tick(1);
  await rejection;
  assert.equal(requests, 0);
  assert.equal(tokenStore.get(), null);

  setLocks({ request: (_name, _options, callback) => Promise.resolve().then(callback) });
  assert.equal((await refreshSession()).token, 'after-lock-timeout');
  assert.equal(requests, 1);
});

test('a rejected browser lock releases the refresh promise without starting a request', async () => {
  let requests = 0;
  setLocks({ request: () => Promise.reject(new Error('lock unavailable')) });
  axios.defaults.adapter = (config) => {
    requests++;
    return Promise.resolve(response(config, 'after-lock-error'));
  };
  await assert.rejects(refreshSession(), /lock unavailable/);
  assert.equal(requests, 0);

  setLocks(undefined);
  assert.equal((await refreshSession()).token, 'after-lock-error');
  assert.equal(requests, 1);
});

for (const code of ['ECONNABORTED', 'ETIMEDOUT']) {
  test(`${code} reports a slow server instead of a disconnected network`, () => {
    assert.equal(
      getErrorMessage(new AxiosError('request timed out', code)),
      'Máy chủ phản hồi quá lâu. Vui lòng thử lại.',
    );
  });
}

test('a network failure without a response still reports connection trouble', () => {
  assert.equal(
    getErrorMessage(new AxiosError('Network Error', 'ERR_NETWORK')),
    'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.',
  );
});

test('the server credential error takes precedence over generic client messages', () => {
  const message = 'Tên đăng nhập hoặc mật khẩu không đúng. Kiểm tra lại rồi thử đăng nhập tiếp.';
  const response = { status: 401, data: { code: 'INVALID_CREDENTIALS', message } };
  for (const code of ['ERR_BAD_REQUEST', 'ECONNABORTED', 'ETIMEDOUT']) {
    assert.equal(getErrorMessage(new AxiosError('request failed', code, undefined, undefined, response)), message);
  }
});
