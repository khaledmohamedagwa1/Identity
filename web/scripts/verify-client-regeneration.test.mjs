// TC-F10-02: Regenerate API client after OpenAPI change -> types updated; build passes.
// Runs the real generator on a modified copy of the AdminApi document and type-checks code that uses the new type.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { join, resolve } from 'node:path';
import yaml from 'js-yaml';
import { apis, inputFor, webRoot } from './api-sources.mjs';

test('TC_F10_02_RegenerateClientAfterOpenApiChange', () => {
  const work = resolve(webRoot, 'tmp', `regen-${process.pid}`);
  rmSync(work, { recursive: true, force: true });
  mkdirSync(work, { recursive: true });
  try {
    // 1. Change the contract: add a schema and an operation that returns it.
    const admin = apis.find((a) => a.name === 'admin-api');
    const doc = yaml.load(readFileSync(inputFor(admin).path, 'utf8'));
    doc.components ??= {};
    doc.components.schemas ??= {};
    doc.components.schemas.TcF10Widget = {
      type: 'object',
      required: ['id', 'label'],
      properties: { id: { type: 'string' }, label: { type: 'string' } },
    };
    doc.paths['/api/v1/tc-f10-widgets'] = {
      get: {
        operationId: 'listTcF10Widgets',
        tags: ['TcF10'],
        responses: {
          200: {
            description: 'Widgets',
            content: {
              'application/json': {
                schema: { type: 'array', items: { $ref: '#/components/schemas/TcF10Widget' } },
              },
            },
          },
        },
      },
    };
    const changedPath = join(work, 'admin-api.changed.json');
    writeFileSync(changedPath, JSON.stringify(doc, null, 2));

    // 2. Regenerate.
    execFileSync(
      process.execPath,
      [
        resolve(webRoot, 'scripts/generate-api.mjs'),
        '--input-override',
        `admin-api=${changedPath}`,
        '--output-root',
        join(work, 'generated'),
      ],
      { cwd: webRoot, stdio: 'pipe' },
    );
    const generated = join(work, 'generated', 'admin-api');
    assert.ok(existsSync(join(generated, 'models', 'tc-f-10-widget.ts')), 'new model generated');
    assert.match(
      readFileSync(join(generated, 'functions.ts'), 'utf8'),
      /listTcF10Widgets/,
      'new operation generated',
    );

    // 3. Code using the new types compiles under the workspace's strict settings.
    writeFileSync(
      join(work, 'probe.ts'),
      [
        "import type { TcF10Widget } from './generated/admin-api/models';",
        "import { listTcF10Widgets } from './generated/admin-api/functions';",
        "const widget: TcF10Widget = { id: 'w1', label: 'Widget' };",
        'export const probe = [widget.label.toUpperCase(), listTcF10Widgets.PATH];',
        '',
      ].join('\n'),
    );
    writeFileSync(
      join(work, 'tsconfig.json'),
      JSON.stringify({
        extends: '../../tsconfig.json',
        compilerOptions: { noEmit: true, types: [] },
        include: ['**/*.ts'],
      }),
    );
    execFileSync(
      process.execPath,
      [resolve(webRoot, 'node_modules/typescript/bin/tsc'), '-p', join(work, 'tsconfig.json')],
      {
        cwd: webRoot,
        stdio: 'pipe',
      },
    );
  } finally {
    rmSync(work, { recursive: true, force: true });
  }
});
