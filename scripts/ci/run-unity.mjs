import { spawnSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, statSync, unlinkSync, writeFileSync } from 'node:fs';
import { basename, isAbsolute, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const project = fileURLToPath(new URL('../../TidePool/', import.meta.url));
const editor = process.env.UNITY_EDITOR;
const checks = [
  {
    name: 'import',
    method: 'GirlV06ProjectImportCheck.RunBatch',
    report: 'Development/character_validation/project-import-check.json',
    marker: 'Girl v06 project import check passed=True',
  },
  {
    name: 'math',
    method: 'TidePool.GiantHand.Editor.GiantHandMathChecks.Run',
    marker: 'GiantHand math checks passed:',
    quit: true,
  },
  {
    name: 'player',
    method: 'GirlPlayerIntegrationTest.RunBatch',
    report: 'Development/character_integration/runtime-report.json',
  },
  {
    name: 'water',
    method: 'PoolWaterCycleRegression.RunBatch',
    marker: 'TidePool water regression passed',
  },
];

function getEditorVersion() {
  if (!editor || !isAbsolute(editor)) {
    throw new Error('Set UNITY_EDITOR to the absolute path of the activated Unity Editor executable.');
  }
  const settings = readFileSync(join(project, 'ProjectSettings/ProjectVersion.txt'), 'utf8');
  const version = /^m_EditorVersion:\s*(\S+)/m.exec(settings)?.[1];
  if (!version) throw new Error('Missing m_EditorVersion in project settings.');
  const query = spawnSync(editor, ['-version'], { encoding: 'utf8', windowsHide: true, timeout: 60_000 });
  if (query.error) throw query.error;
  const output = `${query.stdout}\n${query.stderr}`.trim();
  const versions = output.split(/[^\w.]+/);
  if (query.status !== 0 || !versions.includes(version)) {
    throw new Error(`Expected Unity ${version}. Editor -version returned: ${output}`);
  }
  return version;
}

function runCheck(check, version, outputDirectory) {
  const log = join(outputDirectory, check.name + '.log');
  const report = check.report && join(project, check.report);
  // Character checks write to historical report paths that can already contain a PASS.
  const previousWrite = report && statSync(report, { bigint: true, throwIfNoEntry: false })?.mtimeNs;
  const args = ['-batchmode', '-projectPath', project, '-executeMethod', check.method, '-logFile', log];
  if (check.quit) args.push('-quit');
  console.log(`Running ${check.method} (${log})`);
  const result = spawnSync(editor, args, {
    cwd: project, stdio: 'inherit', windowsHide: true, timeout: 30 * 60_000, killSignal: 'SIGKILL',
  });

  let reportCopy;
  if (report) {
    const lastWrite = statSync(report, { bigint: true, throwIfNoEntry: false })?.mtimeNs;
    if (lastWrite !== undefined && lastWrite !== previousWrite) {
      reportCopy = join(outputDirectory, basename(report));
      copyFileSync(report, reportCopy);
    }
  }
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`Unity exited with ${result.status ?? result.signal}`);
  if (check.marker && !readFileSync(log, 'utf8').includes(check.marker)) {
    throw new Error(`Missing success message: ${check.marker}`);
  }
  if (report) {
    if (!reportCopy) throw new Error(`Unity did not write a fresh ${basename(report)}`);
    const evidence = JSON.parse(readFileSync(reportCopy, 'utf8').replace(/^\uFEFF/, ''));
    if (evidence.passed !== true || !Array.isArray(evidence.errors) || evidence.errors.length) {
      throw new Error(`Failed checks in ${reportCopy}`);
    }
    if (check.name === 'import' && (evidence.unityVersion !== version || evidence.scriptsCompiled !== true)) {
      throw new Error(`Import report does not confirm scripts compiled with Unity ${version}`);
    }
    if (check.name === 'player' && evidence.scene !== 'Assets/_Game/Scenes/PoolHall_ArmLab_01.unity') {
      throw new Error(`Unexpected player test scene: ${evidence.scene}`);
    }
  }
}

try {
  const version = getEditorVersion();
  const logs = join(project, 'Logs', 'ci');
  mkdirSync(logs, { recursive: true });
  const outputDirectory = mkdtempSync(join(logs, 'run-'));
  // Unity's interactive Search index can be empty on a fresh batch checkout.
  // Disable background indexing during Play tests and restore the original SJSON verbatim.
  const settingsDir = join(project, 'UserSettings');
  mkdirSync(settingsDir, { recursive: true });
  const searchSettings = join(settingsDir, 'Search.settings');
  const previousSettings = existsSync(searchSettings) ? readFileSync(searchSettings) : undefined;
  try {
    writeFileSync(searchSettings, '{"indexOnEditorStartup":false}\n');
    for (const check of checks) {
      try {
        runCheck(check, version, outputDirectory);
        console.log(`PASS ${check.name}`);
      } catch (error) {
        console.error(`FAIL ${check.name}: ${error.message}. See ${outputDirectory}`);
        process.exitCode = 1;
        if (check.name === 'import') break;
      }
    }
  } finally {
    if (previousSettings !== undefined) writeFileSync(searchSettings, previousSettings);
    else if (existsSync(searchSettings)) unlinkSync(searchSettings);
  }
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
