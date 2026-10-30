const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const launcher = path.resolve(__dirname, '../../scripts/Start-Local.ps1');
const shell = process.platform === 'win32' ? 'powershell.exe' : 'pwsh';
const secret = 'test-only-private-value';
const connection = 'Server=test-server;Database=test-database;Password=test-only-db-password';

function fixture(t, shared = { Stripe__SecretKey: secret }, local = { ConnectionStrings__DefaultConnection: connection }) {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'rroms-secrets-'));
    t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
    const sharedPath = path.join(directory, 'shared.json');
    const localPath = path.join(directory, 'local.json');
    fs.writeFileSync(sharedPath, JSON.stringify(shared));
    fs.writeFileSync(localPath, JSON.stringify(local));
    return { directory, sharedPath, localPath };
}

function check(files) {
    const result = spawnSync(shell, [
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', launcher,
        '-SharedSecretsPath', files.sharedPath, '-LocalSecretsPath', files.localPath, '-Check'
    ], { encoding: 'utf8', timeout: 20000 });
    assert.ifError(result.error);
    const output = result.stdout + result.stderr;
    assert.ok(!output.includes(secret), 'A service credential was printed.');
    assert.ok(!output.includes(connection), 'A database connection was printed.');
    return { ...result, output };
}

test('private files can be checked without starting the application or printing values', t => {
    const result = check(fixture(t));
    assert.equal(result.status, 0, result.output);
    assert.match(result.output, /No app was started/);
});

test('local values reach the app and inherited values are restored afterward', t => {
    const files = fixture(t, { Stripe__SecretKey: secret }, {
        ConnectionStrings__DefaultConnection: connection,
        Stripe__SecretKey: 'test-only-local-override'
    });
    const harness = path.join(files.directory, 'harness.ps1');
    fs.writeFileSync(harness, `
$ErrorActionPreference = 'Stop'
$env:Stripe__SecretKey = 'inherited-test-value'
function global:dotnet {
    if ($env:Stripe__SecretKey -cne 'test-only-local-override') { throw 'Local override was not applied.' }
    if ($env:ConnectionStrings__DefaultConnection -cne $env:RROMS_TEST_EXPECTED_CONNECTION) { throw 'Wrong database setting.' }
    $global:LASTEXITCODE = 0
}
& $env:RROMS_TEST_LAUNCHER -SharedSecretsPath $env:RROMS_TEST_SHARED -LocalSecretsPath $env:RROMS_TEST_LOCAL
if ($env:Stripe__SecretKey -cne 'inherited-test-value') { throw 'Inherited environment was not restored.' }
`);
    const result = spawnSync(shell, [
        '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', harness
    ], {
        encoding: 'utf8', timeout: 20000,
        env: {
            ...process.env, RROMS_TEST_LAUNCHER: launcher,
            RROMS_TEST_SHARED: files.sharedPath, RROMS_TEST_LOCAL: files.localPath,
            RROMS_TEST_EXPECTED_CONNECTION: connection
        }
    });
    assert.ifError(result.error);
    assert.equal(result.status, 0, result.stdout + result.stderr);
});

test('database settings are rejected in the shared file', t => {
    const result = check(fixture(t, { ConnectionStrings__DefaultConnection: connection }));
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Keep database and administrator settings in secrets.local.json/);
});

test('a missing personal file cannot silently use another database', t => {
    const files = fixture(t);
    fs.unlinkSync(files.localPath);
    const result = check(files);
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Missing/);
});

test('the database template must be replaced before launch', t => {
    const result = check(fixture(t, {}, {
        ConnectionStrings__DefaultConnection: 'Server=YOUR_SQL_SERVER;Database=YOUR_DATABASE'
    }));
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Set your own/);
});

test('malformed JSON reports the file without printing its secret content', t => {
    const files = fixture(t);
    fs.writeFileSync(files.sharedPath, `{"Stripe__SecretKey":"${secret}" BROKEN}`);
    const result = check(files);
    assert.notEqual(result.status, 0);
    assert.match(result.output, /Ensure it contains valid JSON/);
});

test('non-string setting values are rejected without printing their content', t => {
    const result = check(fixture(t, { Stripe__SecretKey: [secret] }));
    assert.notEqual(result.status, 0);
    assert.match(result.output, /must be strings/);
});
