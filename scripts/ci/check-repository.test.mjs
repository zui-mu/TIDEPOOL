import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const script = fileURLToPath(new URL('./check-repository.mjs', import.meta.url));
const template = ['做了什么', '为什么这么做', '可观测的结果是什么', '有什么收益/风险']
  .map(title => '## ' + title + '\n\n<!-- 填写本段 -->\n').join('\n');

function fixture(t) {
  const temp = realpathSync(tmpdir());
  const cwd = mkdtempSync(join(temp, 'tidepool-ci-'));
  t.after(() => {
    assert.equal(dirname(realpathSync(cwd)), temp);
    rmSync(cwd, { recursive: true, force: true });
  });
  const git = (...args) => execFileSync('git', ['-c', 'core.hooksPath=.test-no-hooks', ...args], { cwd, encoding: 'utf8' }).trim();
  const write = (name, content) => {
    mkdirSync(dirname(join(cwd, name)), { recursive: true });
    writeFileSync(join(cwd, name), content);
  };
  const commit = () => {
    git('add', '-A');
    git('commit', '--quiet', '-m', 'fixture');
    return git('rev-parse', 'HEAD');
  };
  git('init', '-q', '-b', 'main');
  git('config', 'user.name', 'Test');
  git('config', 'user.email', 'test@example.invalid');
  git('config', 'commit.gpgsign', 'false');
  git('config', 'core.autocrlf', 'false');
  write('CONTRIBUTING.md', '```regex\n^(ci|fix)/[a-z]+$\n```\n');
  write('.github/PULL_REQUEST_TEMPLATE.md', template);
  write('TidePool/Assets/old.cs', '// old\n');
  write('TidePool/Assets/old.cs.meta', 'guid: 1\n');
  const base = commit();
  const run = (args = ['--base', base, '--head', 'HEAD', '--branch', 'ci/check'], env = {}) => {
    const result = spawnSync(process.execPath, [script, ...args], { cwd, encoding: 'utf8', env: { ...process.env, ...env } });
    assert.ifError(result.error);
    return { status: result.status, output: result.stdout + result.stderr };
  };
  return { base, cwd, git, write, commit, run };
}

test('first branch push compares against its fork point and accepts paired asset renames', t => {
  const f = fixture(t);
  f.git('update-ref', 'refs/remotes/origin/main', f.base);
  f.git('mv', 'TidePool/Assets/old.cs', 'TidePool/Assets/new.cs');
  f.git('mv', 'TidePool/Assets/old.cs.meta', 'TidePool/Assets/new.cs.meta');
  f.write('TidePool/Packages/test/Documentation~/example.md', 'example\n');
  const head = f.commit();
  f.write('event.json', JSON.stringify({ ref: 'refs/heads/ci/check', before: '0'.repeat(40), after: head, repository: { default_branch: 'main' } }));
  const result = f.run([], { GITHUB_EVENT_NAME: 'push', GITHUB_EVENT_PATH: join(f.cwd, 'event.json') });
  assert.equal(result.status, 0, result.output);
  assert.ok(result.output.includes(f.base.slice(0, 8) + '..' + head.slice(0, 8)));
});

test('deleted assets, embedded package folders and local package files require their counterparts', t => {
  const f = fixture(t);
  f.git('rm', 'TidePool/Assets/old.cs');
  f.write('TidePool/Packages/test/Runtime/A.cs', '// demo\n');
  f.write('TidePool/Packages/test/Runtime/A.cs.meta', 'guid: 2\n');
  f.write('sources/JigglePhysics/Demo.md', 'demo\n');
  f.commit();
  const result = f.run();
  assert.equal(result.status, 1);
  assert.match(result.output, /orphan TidePool\/Assets\/old.cs.meta/);
  assert.match(result.output, /missing TidePool\/Packages\/test\/Runtime.meta/);
  assert.match(result.output, /missing sources\/JigglePhysics\/Demo.md.meta/);
});

test('PR body uses four filled template sections and treats code blocks as content', t => {
  const f = fixture(t);
  const body = template.replaceAll('<!-- 填写本段 -->', '记录结果。\n```md\n## example\n```');
  const check = (description, branch = 'fix/demo') => {
    f.write('event.json', JSON.stringify({ pull_request: { base: { sha: f.base }, head: { ref: branch }, body: description } }));
    return f.run([], { GITHUB_EVENT_NAME: 'pull_request', GITHUB_EVENT_PATH: join(f.cwd, 'event.json'), GITHUB_SHA: f.base });
  };
  const valid = check(body);
  assert.equal(valid.status, 0, valid.output);
  for (const invalid of [template, body.replace('为什么这么做', 'Summary'), body + '\n## Extra\nExtra content.']) {
    assert.match(check(invalid).output, /Fill all four/);
  }
  assert.match(check(body, 'agent/demo').output, /Invalid branch name/);
});

test('raw Git blobs with an LFS attribute and whitespace regressions both fail', t => {
  const f = fixture(t);
  f.write('bad.bin', 'raw data\n');
  f.commit();
  f.write('.gitattributes', '*.bin filter=lfs diff=lfs merge=lfs -text\n');
  f.git('add', '.gitattributes');
  f.git('commit', '--quiet', '-m', 'apply LFS without migrating blob');
  const lfs = f.run();
  assert.equal(lfs.status, 1, lfs.output);
  assert.match(lfs.output, /bad\.bin/);
  f.write('text.md', 'trailing space \n');
  f.commit();
  const whitespace = f.run();
  assert.equal(whitespace.status, 1);
  assert.match(whitespace.output, /trailing whitespace/);
});
