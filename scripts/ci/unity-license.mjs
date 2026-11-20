import { spawnSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';

// Run activation outside the checked-out Unity project. Licensing logs can contain
// account details and must stay outside the directory uploaded as test evidence.
function manageLicense(mode) {
  const { UNITY_EDITOR, UNITY_EMAIL, UNITY_PASSWORD, RUNNER_TEMP } = process.env;
  if (!UNITY_EDITOR || !UNITY_EMAIL || !UNITY_PASSWORD || !RUNNER_TEMP) {
    throw new Error('Missing Unity editor, account or runner temp directory.');
  }
  if (process.platform === 'win32') mkdirSync(join(process.env.ProgramData, 'Unity'), { recursive: true });
  const project = join(RUNNER_TEMP, 'UnityLicenseProject');
  mkdirSync(join(project, 'Assets'), { recursive: true });
  const args = ['-batchmode', '-quit', '-nographics', '-projectPath', project,
    '-logFile', join(RUNNER_TEMP, 'unity-license-' + mode + '.log'),
    '-username', UNITY_EMAIL, '-password', UNITY_PASSWORD];
  if (mode === 'activate') {
    let serial = process.env.UNITY_SERIAL;
    if (!serial) {
      // Same Hub ULF format supported by GameCI's getSerialFromLicenseFile.
      const encoded = /<DeveloperData\s+Value="([A-Za-z0-9+/=]+)"/.exec(process.env.UNITY_LICENSE ?? '');
      if (encoded) serial = Buffer.from(encoded[1], 'base64').subarray(4).toString('latin1');
    }
    if (!serial || !/^[a-z0-9]+(?:-[a-z0-9]+)+$/i.test(serial)) {
      throw new Error('Provide a compatible Unity Hub ULF (UNITY_LICENSE) or a valid UNITY_SERIAL.');
    }
    args.push('-serial', serial);
  } else if (mode === 'return') {
    args.push('-returnlicense');
  } else {
    throw new Error('Expected activate or return.');
  }
  const result = spawnSync(UNITY_EDITOR, args, { cwd: project, stdio: 'ignore', windowsHide: true, timeout: 300_000 });
  if (result.error || result.status !== 0) {
    throw new Error('Unity license operation failed (' + (result.status ?? 'startup or timeout') + '). Check the CI account and its available seats.');
  }
}

try {
  manageLicense(process.argv[2]);
} catch (error) {
  console.error(error.message);
  process.exitCode = 1;
}
