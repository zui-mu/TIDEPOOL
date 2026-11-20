import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { posix } from 'node:path';
import { parseArgs } from 'node:util';

function git(...args) {
  return execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 32 * 1024 * 1024 });
}

function commit(ref) {
  if (!ref) throw new Error('A base and head revision are required.');
  return git('rev-parse', '--verify', '--end-of-options', ref + '^{commit}').trim();
}

function inputs() {
  if (process.argv.length > 2) {
    const { values } = parseArgs({ options: {
      base: { type: 'string' }, head: { type: 'string' }, branch: { type: 'string' }, 'body-file': { type: 'string' },
    } });
    return { ...values, body: values['body-file'] ? readFileSync(values['body-file'], 'utf8') : undefined };
  }

  const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  if (process.env.GITHUB_EVENT_NAME === 'pull_request') {
    const pr = event.pull_request;
    return { branch: pr.head.ref, base: pr.base.sha, head: process.env.GITHUB_SHA, body: pr.body ?? '' };
  }
  if (process.env.GITHUB_EVENT_NAME !== 'push' || !event.ref.startsWith('refs/heads/')) {
    throw new Error('Expected a branch push or pull_request event.');
  }
  const branch = event.ref.slice('refs/heads/'.length);
  const head = commit(event.after);
  let base = event.before;
  if (/^0+$/.test(base)) {
    const main = commit('refs/remotes/origin/' + event.repository.default_branch);
    base = git('merge-base', main, head).trim();
  }
  return { base, head, branch };
}

function validateDescription(template, body) {
  const headings = [...template.matchAll(/^## (.+)$/gm)].map(match => match[1].trim());
  if (headings.length !== 4) throw new Error('The PR template must contain four section headings.');
  const sections = [];
  let fence = '';
  for (const line of body.replace(/<!--[\s\S]*?(?:-->|$)/g, '').split(/\r?\n/)) {
    const marker = line.match(/^ {0,3}(`{3,}|~{3,})/);
    if (marker && !fence) {
      fence = marker[1];
      continue;
    }
    if (fence && line.trimEnd().trimStart().startsWith(fence) && /^\s*(`+|~+)\s*$/.test(line)) {
      fence = '';
      continue;
    }
    const heading = !fence && /^##[ \t]+(.+?)[ \t]*$/.exec(line);
    if (heading) sections.push({ title: heading[1], content: [] });
    else if (sections.length && line.trim()) sections.at(-1).content.push(line);
  }
  if (sections.length !== headings.length || headings.some((title, i) => sections[i].title !== title || !sections[i].content.length)) {
    throw new Error('Fill all four PR template sections in order: ' + headings.join(' | '));
  }
}

function importRoot(path) {
  const match = /^(TidePool\/Assets|TidePool\/Packages\/[^/]+|sources\/JigglePhysics)\/(.+)$/.exec(path);
  if (!match) return;
  // Unity ignores hidden files and folders ending in ~, including UPM Documentation~.
  if (match[2].split('/').some(part => part.startsWith('.') || part.endsWith('~') || part === 'CVS')) return;
  if (/\.tmp$/i.test(match[2])) return;
  return match[1];
}

function validateMetaPairs(base, head) {
  const changed = git('diff', '--name-only', '--no-renames', '-z', base, head, '--').split('\0').filter(Boolean);
  const paths = new Set(git('ls-tree', '-rtz', '--name-only', head).split('\0'));
  const assets = new Set();
  for (const path of changed) {
    let asset = path.endsWith('.meta') ? path.slice(0, -5) : path;
    const root = importRoot(asset);
    if (!root) continue;
    while (asset !== root) {
      assets.add(asset);
      asset = posix.dirname(asset);
    }
  }
  const problems = [];
  for (const asset of assets) {
    const hasAsset = paths.has(asset);
    if (hasAsset !== paths.has(asset + '.meta')) {
      problems.push(hasAsset ? 'missing ' + asset + '.meta' : 'orphan ' + asset + '.meta');
    }
  }
  if (problems.length) throw new Error(problems.join('\n'));
}

function check() {
  const run = inputs();
  const base = commit(run.base);
  const head = commit(run.head);
  const policy = git('show', head + ':CONTRIBUTING.md').match(/^```regex\r?\n([^\r\n]+)\r?\n```/m);
  if (!policy) throw new Error('Missing branch expression in CONTRIBUTING.md.');
  if (run.branch !== 'main' && (!run.branch || !new RegExp(policy[1]).test(run.branch))) {
    throw new Error('Invalid branch name: ' + JSON.stringify(run.branch));
  }
  if (run.body !== undefined) validateDescription(git('show', head + ':.github/PULL_REQUEST_TEMPLATE.md'), run.body);
  git('diff', '--check', base, head, '--');
  validateMetaPairs(base, head);
  git('lfs', 'fsck', '--pointers', '--dry-run', head);
  console.log('Repository checks passed for ' + run.branch + ' (' + base.slice(0, 8) + '..' + head.slice(0, 8) + ').');
}

try {
  check();
} catch (error) {
  console.error([error.message, error.stdout, error.stderr].filter(Boolean).join('\n').trim());
  process.exitCode = 1;
}
