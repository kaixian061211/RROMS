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
        document: { querySelector() { return {
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
