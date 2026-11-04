import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'

import { describe, expect, it } from 'vitest'

const repoRoot = resolve(import.meta.dirname, '..')

type Settings = { hooks: { PreToolUse: { matcher: string; hooks: { command: string }[] }[] } }

function runHook(command: string) {
  const settings = JSON.parse(readFileSync(resolve(repoRoot, '.claude/settings.json'), 'utf8')) as Settings
  const entry = settings.hooks.PreToolUse.find((candidate) => candidate.matcher === 'Bash')
  return spawnSync('sh', ['-c', entry?.hooks[0]?.command ?? 'exit 99'], {
    input: JSON.stringify({ tool_name: 'Bash', tool_input: { command } }),
    env: { ...process.env, CLAUDE_PROJECT_DIR: repoRoot },
    encoding: 'utf8',
  })
}

describe('the agent hook that refuses Prettier', () => {
  it.each([
    'prettier --write src/a.ts',
    'prettier --check src/a.ts',
    'pnpm prettier --write src/a.ts',
    'pnpm exec prettier --write src/a.ts',
    'pnpm -C frontend exec prettier --write src/a.ts',
    'pnpm --filter=app exec prettier --write src/a.ts',
    'pnpm dlx prettier --write src/a.ts',
    'pnpx prettier --write src/a.ts',
    'npx prettier --write src/a.ts',
    'npx --yes prettier@3 --write src/a.ts',
    'npx -p prettier prettier --write src/a.ts',
    'npm exec -- prettier --write src/a.ts',
    'yarn prettier --write src/a.ts',
    'bunx prettier --write src/a.ts',
    'node_modules/.bin/prettier --write src/a.ts',
    'node node_modules/.pnpm/prettier@3.7.4/node_modules/prettier/bin/prettier.cjs --write src/a.ts',
    'cd frontend && npx prettier --write .',
    'git diff --stat; pnpm exec prettier --write src/a.ts',
    'CI=1 npx prettier --write src/a.ts',
    'git ls-files | xargs prettier --write',
    'git ls-files | xargs -n 1 prettier --write',
    'find src -name "*.ts" -exec prettier --write {} +',
    'for f in a b; do pnpm exec prettier --no-semi --write $f; done',
    'time (pnpm exec prettier --check "src/**/*.ts" 2>&1 | tail -2)',
    'npx \\\n  prettier --write src/a.ts',
    "cat <<'EOF' | npx prettier --stdin-filepath a.ts\nconst a = 1\nEOF",
  ])('refuses %s', (command) => {
    const result = runHook(command)

    expect(result.status).toBe(2)
    expect(result.stderr).toContain('does not use Prettier')
    expect(result.stderr).toContain('pnpm lint')
  })

  it.each([
    'pnpm lint',
    'pnpm test refuse-prettier-hook.test.ts',
    'grep -rn prettier frontend/package.json',
    'cat frontend/node_modules/.pnpm/prettier@3.7.4/node_modules/prettier/package.json',
    'ls .prettierrc* prettier.config.*',
    'node scripts/prettier-report.mjs',
    './prettier-report.sh',
    'pnpm why prettier',
    'npm view prettier version',
    'command -v prettier',
    'which prettier',
    'echo prettier',
    'git add .claude/hooks/refuse-prettier.mjs',
    'git log --grep=prettier',
    'gh issue list --search prettier',
    'git commit -m "do not run prettier --write; use pnpm lint"',
    "echo 'a && npx prettier --write b'",
    "git commit -F - <<'EOF'\nnpx prettier --write rewrote the file. It's gone.\nEOF",
    `gh pr create --body "$(cat <<'EOF'\n## Why\n\npnpm exec prettier --write\nnpx prettier --write\nEOF\n)"`,
  ])('allows %s', (command) => {
    const result = runHook(command)

    expect(result.status).toBe(0)
    expect(result.stderr).toBe('')
  })
})
