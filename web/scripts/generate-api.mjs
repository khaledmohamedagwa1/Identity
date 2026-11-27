// Generates the typed Angular clients from the OpenAPI documents (never hand-written, spec 7.8).
// Usage: node scripts/generate-api.mjs [--input-override name=path] [--output-root dir]
import { execFileSync } from 'node:child_process';
import { relative, resolve } from 'node:path';
import { apis, generatedRoot, inputFor, webRoot } from './api-sources.mjs';

const args = process.argv.slice(2);
const overrides = new Map();
let outputRoot = generatedRoot;
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--input-override') {
    const [name, path] = args[++i].split('=');
    overrides.set(name, resolve(path));
  } else if (args[i] === '--output-root') {
    outputRoot = resolve(args[++i]);
  }
}

const bin = resolve(webRoot, 'node_modules/ng-openapi-gen/lib/index.js');
for (const api of apis) {
  const input = overrides.has(api.name)
    ? { path: overrides.get(api.name), isStub: false }
    : inputFor(api);
  const output = resolve(outputRoot, api.name);
  execFileSync(
    process.execPath,
    [
      bin,
      '--input',
      input.path,
      '--output',
      output,
      '--silent',
      'true',
      '--ignoreUnusedModels',
      'false',
      '--enumStyle',
      'alias',
    ],
    { stdio: 'inherit', cwd: webRoot },
  );
  const note = input.isStub ? ' (STUB: real contract not published yet)' : '';
  console.log(`api:generate ${api.name} <- ${relative(webRoot, input.path)}${note}`);
}
