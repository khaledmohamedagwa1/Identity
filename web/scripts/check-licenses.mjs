// Fails when any npm dependency (prod or dev) has a license outside the allow-list (spec 7.1: no commercial libraries).
// The .NET side has its own check in the backend CI.
import { execFileSync } from 'node:child_process';
import { resolve } from 'node:path';
import { webRoot } from './api-sources.mjs';

export const ALLOWED = [
  'MIT',
  'ISC',
  'Apache-2.0',
  'BSD-2-Clause',
  'BSD-3-Clause',
  '0BSD',
  'BlueOak-1.0.0',
  'CC0-1.0',
  'Unlicense',
  'Python-2.0', // argparse (via js-yaml): PSF license, permissive
  'CC-BY-3.0', // spdx-exceptions data
  'CC-BY-4.0', // caniuse-lite data
  'MPL-2.0', // weak copyleft, used unmodified (lightningcss)
];

const bin = resolve(
  webRoot,
  'node_modules/license-checker-rseidelsohn/bin/license-checker-rseidelsohn.js',
);
try {
  execFileSync(
    process.execPath,
    [bin, '--excludePrivatePackages', '--summary', '--onlyAllow', ALLOWED.join(';')],
    {
      cwd: webRoot,
      stdio: 'inherit',
    },
  );
  console.log('licenses:check passed');
} catch {
  console.error(
    'licenses:check failed: a dependency uses a license that is not on the allow-list (scripts/check-licenses.mjs).',
  );
  process.exit(1);
}
