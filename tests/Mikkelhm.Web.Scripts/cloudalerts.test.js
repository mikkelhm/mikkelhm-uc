// Run with: node --test tests/Mikkelhm.Web.Scripts
const test = require('node:test');
const assert = require('node:assert');
const { queryFrom } = require('../../src/Mikkelhm.Web/wwwroot/cloudalerts/cloudalerts.js');

const field = (name, value, extra = {}) => ({ name, value, type: 'text', ...extra });

test('queryFrom drops empty values and the default tests=show', () => {
  const fields = [
    field('project', 'mikkelhm'),
    field('env', ''),
    field('q', 'a b&c'),
    field('tests', 'show', { type: 'radio', checked: true }),
    field('tests', 'hide', { type: 'radio', checked: false }),
  ];

  assert.strictEqual(queryFrom(fields), '?project=mikkelhm&q=a+b%26c');
});

test('queryFrom keeps a non-default checked radio and ignores unnamed controls', () => {
  const fields = [
    field('', 'Apply', { type: 'submit' }),
    field('tests', 'show', { type: 'radio', checked: false }),
    field('tests', 'hide', { type: 'radio', checked: true }),
  ];

  assert.strictEqual(queryFrom(fields), '?tests=hide');
});

test('queryFrom with only defaults returns an empty string', () => {
  assert.strictEqual(queryFrom([field('project', ''), field('tests', 'show', { type: 'radio', checked: true })]), '');
});
