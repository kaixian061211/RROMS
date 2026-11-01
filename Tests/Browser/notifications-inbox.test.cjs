const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../../wwwroot/js/notifications-inbox.js'), 'utf8');

test('read actions block repeated submission and recover after back/forward restoration', () => {
    const listeners = {};
    const button = { textContent: 'Mark as read', dataset: {}, disabled: false };
    class Form {
        method = 'post'; dataset = {};
        setAttribute() {} removeAttribute() {}
        querySelector() { return button; }
    }
    const form = new Form();
    vm.runInNewContext(script, {
        HTMLFormElement: Form,
        document: { addEventListener() {}, querySelector() { return {
            addEventListener(name, callback) { listeners[name] = callback; },
            querySelectorAll() { return [form]; }
        }; } },
        window: { addEventListener(name, callback) { listeners[name] = callback; } }
    });
    let prevented = 0;
    const event = { target: form, defaultPrevented: false, preventDefault() { prevented++; } };
    listeners.submit(event);
    assert.equal(button.disabled, true);
    assert.equal(button.textContent, 'Saving…');
    listeners.submit(event);
    assert.equal(prevented, 1);
    listeners.pageshow();
    assert.equal(button.disabled, false);
    assert.equal(button.textContent, 'Mark as read');
    assert.equal(form.dataset.submitting, undefined);
    listeners.submit(event);
    assert.equal(prevented, 1);
});

function liveFixture() {
    const error = { hidden: true };
    const state = { requests: [], renders: 0, fail: false, wrongOwner: false };
    const page = {
        dataset: { notificationOwner: '2', beforeId: '25' },
        addEventListener() {}, contains() { return false; },
        querySelector(selector) { return selector === '[data-inbox-error]' ? error : null; },
        querySelectorAll() { return []; },
        replaceChildren() { state.renders++; error.hidden = true; }
    };
    state.window = { location: { href: 'https://restaurant.test/Notifications?beforeId=25' }, addEventListener() {} };
    vm.runInNewContext(script, {
        window: state.window, AbortController, URL, setTimeout, clearTimeout,
        document: { hidden: false, activeElement: null, addEventListener() {},
            querySelector() { return page; }, importNode(node) { return node; } },
        fetch: async url => {
            state.requests.push(url);
            return { ok: !state.fail, redirected: false, url,
                headers: { get: () => 'text/html' }, text: async () => '<authorized inbox>' };
        },
        DOMParser: class { parseFromString() { return { querySelector() { return {
            dataset: { notificationOwner: state.wrongOwner ? '999' : '2', beforeId: '25' }, childNodes: []
        }; } }; } }
    });
    state.error = error;
    return state;
}

const settle = async () => { for (let i = 0; i < 8; i++) await new Promise(setImmediate); };

test('live inbox refresh preserves the current pagination cursor', async () => {
    const state = liveFixture();
    state.window.RromsNotificationInbox.refresh(); await settle();
    assert.equal(state.renders, 1);
    assert.equal(state.requests[0], 'https://restaurant.test/Notifications?beforeId=25');
});

test('a different recipient cannot replace the current inbox', async () => {
    const state = liveFixture(); state.wrongOwner = true;
    state.window.RromsNotificationInbox.refresh(); await settle();
    assert.equal(state.renders, 0);
    assert.equal(state.error.hidden, false);
});

test('failed inbox refresh retains content and can recover', async () => {
    const state = liveFixture(); state.fail = true;
    state.window.RromsNotificationInbox.refresh(); await settle();
    assert.equal(state.renders, 0);
    assert.equal(state.error.hidden, false);
    state.fail = false;
    state.window.RromsNotificationInbox.refresh(); await settle();
    assert.equal(state.renders, 1);
    assert.equal(state.error.hidden, true);
});
