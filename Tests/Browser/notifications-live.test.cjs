const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/notifications-live.js'), 'utf8');

test('incoming notifications queue toasts and defer inbox refresh until they finish', async () => {
    const events = {};
    const toastEvents = {};
    const timers = new Map();
    let timerId = 0;
    let shown = 0;
    const title = {};
    const message = {};
    const toast = { addEventListener(name, callback) { toastEvents[name] = callback; } };
    const root = { dataset: { hubUrl: '/hubs/notifications', refreshPage: 'true', inboxPage: 'true' } };
    const elements = { 'notification-live': root, 'notification-toast': toast,
        'notification-toast-title': title, 'notification-toast-message': message };
    const connection = { on(name, callback) { events[name] = callback; },
        onreconnected() {}, onclose() {}, start: async () => {}, invoke: async () => 0 };
    const signalR = { HubConnectionBuilder: class {
        withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return connection; }
    } };
    const window = { signalR, addEventListener() {}, bootstrap: { Toast: { getOrCreateInstance() {
        return { show() { shown++; } };
    } } }, location: { reload() {} } };
    vm.runInNewContext(script, {
        window, signalR, document: { getElementById(id) { return elements[id]; }, addEventListener() {} },
        setTimeout(callback, ms) { const id = ++timerId; timers.set(id, { callback, ms }); return id; },
        clearTimeout(id) { timers.delete(id); }
    });
    events.NotificationReceived({ title: 'First', message: '<script>literal text</script>' });
    events.NotificationReceived({ title: 'Second', message: 'Second message' });
    assert.equal(title.textContent, 'First');
    assert.equal(message.textContent, '<script>literal text</script>');
    assert.equal(shown, 1);
    assert.equal(timers.size, 0);
    toastEvents['hidden.bs.toast']();
    assert.equal(title.textContent, 'Second');
    assert.equal(shown, 2);
    assert.equal(timers.size, 0);
    toastEvents['hidden.bs.toast']();
    assert.equal([...timers.values()][0].ms, 500);
});

function badgeFixture() {
    const events = {};
    const pending = [];
    const badge = { setAttribute() {} };
    const connection = {
        on(name, handler) { events[name] = handler; }, onreconnected() {}, onclose() {},
        start: async () => {},
        invoke() { return new Promise(resolve => pending.push(resolve)); }
    };
    const signalR = { HubConnectionBuilder: class {
        withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return connection; }
    } };
    vm.runInNewContext(script, {
        window: { signalR, addEventListener() {} }, signalR,
        document: { getElementById(id) { return id === 'notification-live'
            ? { dataset: {} } : id === 'notification-unread-count' ? badge : null; }, addEventListener() {} },
        setTimeout() {}, clearTimeout() {}
    });
    return { events, pending, badge };
}

const settle = async () => { for (let i = 0; i < 4; i++) await new Promise(setImmediate); };

test('replayed notification IDs do not trigger duplicate delivery handling', async () => {
    const state = badgeFixture(); await settle();
    const notice = { notificationId: 42, title: 'Ready', message: 'Your order is ready.' };
    state.events.NotificationReceived(notice);
    state.events.NotificationReceived(notice);
    assert.equal(state.pending.length, 2, 'initial count plus one distinct notification');
    state.events.NotificationReceived({ ...notice, notificationId: 43 });
    assert.equal(state.pending.length, 3);
});

test('an older count request cannot overwrite the latest unread count', async () => {
    const state = badgeFixture(); await settle();
    state.events.NotificationReadStateChanged();
    state.pending[1](0); await settle();
    state.pending[0](5); await settle();
    assert.equal(state.badge.textContent, '0');
    assert.equal(state.badge.hidden, true);
});

test('invalid unread counts cannot be displayed', async () => {
    const state = badgeFixture(); await settle();
    state.pending[0](-1); await settle();
    assert.equal(state.badge.textContent, undefined);
});
