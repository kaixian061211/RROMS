const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/tracking-live.js'), 'utf8');
const settle = async () => { for (let i = 0; i < 10; i++) await new Promise(setImmediate); };

function fixture(url = 'https://restaurant.test/Tracking?orderId=7') {
    const initial = { orderId: 7, orderStatus: 'pending', reservationId: null,
        reservationStatus: null, paymentId: 3, paymentStatus: 'pending', paymentAmount: 10 };
    const content = { dataset: { orderId: '7', reservationId: '', snapshot: JSON.stringify(initial) },
        renders: 0, replaceChildren() { this.renders++; } };
    const banner = { hidden: true };
    const listeners = {};
    const timers = new Map();
    let timerId = 0;
    const state = { content, banner, listeners, timers, requests: [], snapshot: initial,
        renderedSnapshot: null, failStatus: false, wrongHtml: false };
    const document = { hidden: false,
        getElementById(id) { return ({ 'tracking-content': content, 'tracking-updates-banner': banner })[id]; },
        addEventListener(name, callback) { listeners[name] = callback; },
        importNode(incoming) { return incoming; } };
    state.document = document;
    const window = { location: { href: url, reload() {} },
        addEventListener(name, callback) { listeners[name] = callback; } };
    state.window = window;
    const context = { window, document, URL, AbortController, console,
        setTimeout(callback, ms) { const id = ++timerId; timers.set(id, { callback, ms }); return id; },
        clearTimeout(id) { timers.delete(id); },
        fetch: async (href) => {
            state.requests.push(href);
            const json = new URL(href).searchParams.get('handler') === 'Status';
            return { ok: !(json && state.failStatus), redirected: false, url: href,
                headers: { get: () => json ? 'application/json' : 'text/html' },
                json: async () => state.snapshot, text: async () => 'authorized HTML' };
        },
        DOMParser: class { parseFromString() { return { getElementById() {
            return { dataset: { ...content.dataset, orderId: state.wrongHtml ? '999' : '7',
                snapshot: JSON.stringify(state.renderedSnapshot ?? state.snapshot) }, childNodes: [] };
        } }; } }
    };
    vm.runInNewContext(script, context);
    state.poll = async () => {
        const entry = [...timers.entries()].find(([, timer]) => timer.ms === 5000);
        assert.ok(entry, 'a visible-page polling timer must exist');
        timers.delete(entry[0]); entry[1].callback(); await settle();
    };
    return state;
}

test('tracking starts and refreshes without SignalR', async () => {
    const state = fixture(); await settle();
    assert.equal(state.requests.length, 2);
    assert.equal(state.content.renders, 1);
    assert.equal(typeof state.window.RromsTracking.refresh, 'function');
});

test('payment selector is preserved in status and HTML requests', async () => {
    const state = fixture('https://restaurant.test/Tracking?paymentId=3'); await settle();
    for (const request of state.requests) {
        assert.equal(new URL(request).searchParams.get('paymentId'), '3');
        assert.equal(new URL(request).searchParams.has('orderId'), false);
    }
    assert.equal(new URL(state.requests[1]).searchParams.has('handler'), false);
});

test('same-status detail changes refresh the cards', async () => {
    const state = fixture(); await settle();
    state.snapshot = { ...state.snapshot, paymentAmount: 25 };
    await state.poll();
    assert.equal(state.content.renders, 2);
});

test('rendered HTML establishes the baseline when it is newer than JSON', async () => {
    const state = fixture();
    state.renderedSnapshot = { ...state.snapshot, paymentAmount: 50 };
    await settle();
    state.snapshot = state.renderedSnapshot;
    await state.poll();
    assert.equal(state.content.renders, 1);
});

test('failed requests retain cards, show the banner and retry', async () => {
    const state = fixture(); await settle();
    state.failStatus = true; await state.poll();
    assert.equal(state.content.renders, 1);
    assert.equal(state.banner.hidden, false);
    state.failStatus = false; await state.poll();
    assert.equal(state.banner.hidden, true);
});

test('wrong-record HTML cannot replace current authorized cards', async () => {
    const state = fixture(); await settle();
    state.wrongHtml = true;
    state.window.RromsTracking.refresh(); await settle();
    assert.equal(state.content.renders, 1);
    assert.equal(state.banner.hidden, false);
});

test('hidden pages pause polling and resume when visible', async () => {
    const state = fixture(); await settle();
    const before = state.requests.length;
    state.document.hidden = true; state.listeners.visibilitychange();
    state.window.RromsTracking.refresh(); await settle();
    assert.equal(state.requests.length, before);
    state.document.hidden = false; state.listeners.visibilitychange(); await settle();
    assert.ok(state.requests.length > before);
});
