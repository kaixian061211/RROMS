const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const script = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/web-push.js'), 'utf8');
const settle = async () => { for (let i = 0; i < 10; i++) await new Promise(setImmediate); };

async function fixture({ permission = 'granted', optedIn = false, token = null, alias = null } = {}) {
    const listeners = {};
    const state = { identity: 'customer_a', logins: [], logouts: 0, prompts: 0, optIns: 0,
        failOptIn: false, failIdentity: false, warnings: [] };
    const sdk = {
        async init() {},
        User: { externalId: alias, addEventListener() {},
            PushSubscription: { optedIn, token, addEventListener() {}, async optIn() {
                state.optIns++;
                if (state.failOptIn) throw new Error('temporary subscription failure');
                this.optedIn = true; this.token = 'browser-token';
            } } },
        Notifications: { permission: permission === 'granted', isPushSupported: () => true, addEventListener() {} },
        Slidedown: { async promptPush() { state.prompts++; } },
        async login(value) { state.logins.push(value); this.User.externalId = value; },
        async logout() { state.logouts++; this.User.externalId = null; }
    };
    const window = { Notification: { permission }, addEventListener(name, callback) { listeners[name] = callback; } };
    const document = { visibilityState: 'visible',
        getElementById: () => ({ dataset: { tokenUrl: '/token', identityUrl: '/identity' } }),
        addEventListener(name, callback) { listeners[name] = callback; } };
    vm.runInNewContext(script, { window, document, navigator: {}, URLSearchParams,
        console: { warn(...args) { state.warnings.push(args); } },
        async fetch(url) {
            if (url === '/token') return { ok: true, json: async () => ({ formFieldName: 'csrf', requestToken: 'token' }) };
            if (state.failIdentity) throw new Error('offline');
            return { ok: state.identity !== null, status: state.identity === null ? 403 : 200,
                json: async () => ({ externalId: state.identity }) };
        }
    });
    state.start = async () => { await window.OneSignalDeferred[0](sdk); await settle(); };
    Object.assign(state, { sdk, window, document, listeners });
    return state;
}

test('push binds the server-provided customer identity before opting in', async () => {
    const state = await fixture(); await state.start();
    assert.deepEqual(state.logins, ['customer_a']);
    assert.equal(state.optIns, 1);
    assert.equal(state.prompts, 0);
});

test('push preserves denied permission and an existing subscription opt-out', async () => {
    for (const options of [{ permission: 'denied' }, { token: 'existing-token', optedIn: false }]) {
        const state = await fixture(options); await state.start();
        assert.equal(state.optIns, 0); assert.equal(state.prompts, 0);
    }
});

test('push offers permission only once while the same page is open', async () => {
    const state = await fixture({ permission: 'default' }); await state.start();
    state.listeners.focus(); await settle();
    assert.equal(state.prompts, 1);
});

test('push detaches the previous customer when signed out or identity cannot be checked', async () => {
    for (const offline of [false, true]) {
        const state = await fixture({ alias: 'customer_a', optedIn: true }); await state.start();
        state.identity = null; state.failIdentity = offline;
        state.listeners.focus(); await settle();
        assert.equal(state.sdk.User.externalId, null);
        assert.equal(state.logouts, 1);
    }
});

test('a transient subscription failure can retry when the page regains focus', async () => {
    const state = await fixture(); state.failOptIn = true; await state.start();
    assert.equal(state.optIns, 1);
    state.failOptIn = false; state.listeners.focus(); await settle();
    assert.equal(state.optIns, 2);
    assert.equal(state.sdk.User.PushSubscription.optedIn, true);
});

test('a restored page checks the current customer before retaining its push identity', async () => {
    const state = await fixture({ optedIn: true }); await state.start();
    state.identity = 'customer_b';
    state.listeners.pageshow?.({ persisted: true }); await settle();
    assert.equal(state.sdk.User.externalId, 'customer_b');
    assert.deepEqual(state.logins, ['customer_a', 'customer_b']);
    assert.equal(state.logouts, 1);
});
