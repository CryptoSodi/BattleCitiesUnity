const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

function bridge(clipboard) {
  const library = {};
  let sent;
  const response = new Promise(resolve => { sent = resolve; });
  const context = { LibraryManager: { library }, mergeInto: Object.assign,
    navigator: { clipboard }, UTF8ToString: value => value,
    SendMessage: (target, method, body) => sent({ target, method, body: JSON.parse(body) }) };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../Assets/Plugins/WebGL/BattleCitiesReferral.jslib'), 'utf8'), context);
  return { library, response };
}

test('clipboard is read only on Paste, and returned to the originating request', async () => {
  let reads = 0;
  const { library, response } = bridge({ readText() { reads++; return Promise.resolve('ABCDEF012345'); } });
  assert.equal(reads, 0);
  library.BattleCitiesPasteReferral('Main menu', 'request-1');
  assert.equal(reads, 1);
  assert.deepEqual(await response, { target: 'Main menu', method: 'OnReferralPasteResult', body: { id: 'request-1', ok: true, text: 'ABCDEF012345' } });
});

test('denied, unsupported, and synchronously failing clipboard reads report an error', async () => {
  for (const clipboard of [undefined, { readText: () => Promise.reject(new Error('denied')) }, { readText: () => { throw new Error('blocked'); } }]) {
    const { library, response } = bridge(clipboard);
    library.BattleCitiesPasteReferral('Main menu', 'request-2');
    const result = await response;
    assert.equal(result.body.id, 'request-2');
    assert.equal(result.body.ok, false);
  }
});

test('oversized clipboard data is bounded before passing to Unity', async () => {
  const { library, response } = bridge({ readText: () => Promise.resolve('x'.repeat(50000)) });
  library.BattleCitiesPasteReferral('Main menu', 'request-3');
  assert.equal((await response).body.text.length, 2049);
});
