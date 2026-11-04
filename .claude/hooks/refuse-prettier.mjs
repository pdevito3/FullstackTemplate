// A PreToolUse hook. Exit code 2 refuses the Bash command and gives stderr to the agent.
// This is a guard against a habit, not a security boundary. It knows the common forms. `sh -c '...'`, `$(...)` in double
// quotes, and rarer wrappers such as `timeout` and `nohup` go through.
import { readFileSync } from 'node:fs'

const refusal = `This repo does not use Prettier and has no Prettier config. Prettier rewrites a whole file to another style.
Keep the style of the file that you edit. To check the frontend, run \`pnpm lint\` in frontend/.`

const runners = new Set([
  'npx', 'pnpx', 'bunx', 'pnpm', 'yarn', 'npm', 'bun', 'node', 'exec', 'dlx', 'x', 'env', 'time', 'xargs',
  'do', 'then', 'else', 'if', 'while', '!', '{',
])
const flagsWithValue = new Set(['-C', '--dir', '--prefix', '-F', '--filter', '-p', '--package', '-n', '-I'])
const prettierFile = /^prettier(@.+|\.c?js)?$/

const heredocBody = /<<-?\s*\\?(['"]?)([\w.-]+)\1(.*)\n[\s\S]*?\n[ \t]*\2[ \t]*$/gm
const quotedTextOrComment = /'[^']*'|"(?:\\[\s\S]|[^"\\])*"|(?<!\S)#.*/g
const commandBoundary = /(?<!\\)[;&|()`\n]|\s-exec(?:dir)?\s/

function commandWord(segment) {
  const words = segment.trim().split(/\s+/)
  for (let i = 0; i < words.length; i++) {
    const name = words[i].split('/').at(-1)
    if (flagsWithValue.has(name)) i++
    else if (!name.startsWith('-') && !/^\w+=/.test(name) && !runners.has(name)) return name
  }
}

function runsPrettier(command) {
  return command
    .replace(heredocBody, '$3')
    .replace(quotedTextOrComment, (text) => (text.startsWith('#') ? '' : '""'))
    .replaceAll('\\\n', ' ')
    .split(commandBoundary)
    .some((segment) => prettierFile.test(commandWord(segment)))
}

const { tool_input: toolInput } = JSON.parse(readFileSync(0, 'utf8'))
if (runsPrettier(toolInput.command)) {
  console.error(refusal)
  process.exit(2)
}
