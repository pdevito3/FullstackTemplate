import { RuleTester } from "../rule-tester.ts";

import { noAccumulatingSpreadRule } from "./no-accumulating-spread.ts";

const tester = new RuleTester();
const error = { messageId: "accumulatingSpread" };

// The cases marked oxc come from the tests of oxc/no-accumulating-spread
// (crates/oxc_linter/src/rules/oxc/no_accumulating_spread.rs).
const oxcValid = [
  "let x = { ...a }",
  "let x = [ ...a ]",
  "[1,2,3].map(n => ({ ...obj, n }))",
  "arr.map(function (x) { return { ...obj, [x]: x } }, {})",
  "[...a, ...b]",
  "[...a, ...b].reduce((acc, x) => { acc['foo'] = x; return acc }, {})",
  "arr.reduce((acc, x) => { acc['foo'] = x; return acc }, { ...a, ...b })",
  "let x = { ...a, ...b }; arr.reduce((x, y) => x + y, 0)",
  "arr.reduce((acc, x) => ({ ...x }), {})",
  "arr.reduce((acc, x) => ({ ...obj }), {})",
  "(arr?.map)((acc, x) => [...acc, x], [])",
  "(arr?.[method])((acc, x) => [...acc, x], [])",
  "(arr?.reduce)((acc, x) => [...acc, x], [], 123)",
  "(arr?.reduce)((acc, x) => [...x], [])",
  "(arr?.reduce)((acc) => [...acc], [])",
  "arr.reduce((acc, x, index, array, extra) => [...acc, x], [])",
  "(arr?.reduce())((acc, x) => [...acc, x], [])",
  "arr.reduce((acc, x) => { acc[x] = { ...x }; return acc }, {})",
  "foo.reduce((acc, bar) => {acc.push(...bar); return acc;}, [])",
  "foo.reduce((acc, bar) => {acc[bar.key] = { ...bar.value }; return acc;}, {})",
  "let foo = []; for (let i = 0; i < 10; i++) { foo.push(i); }",
  "let foo = []; while (foo.length < 10) { foo.push(foo.length); }",
  "let foo = {}; for (const i of [1,2,3]) { foo[i] = i; }",
  "function doSomething(list) { return list.reduce((acc, each) => { return each.subList.flatMap((subEach) => { return [...acc, subEach.subList] }) }, []) }",
];

const oxcInvalid = [
  "(arr?.reduce)((acc, x) => [...acc, x], []);",
  "arr.reduce((acc, x, index) => [...acc, x], []);",
  "arr.reduceRight((acc, x, index, array) => ({ ...acc, [x]: index }), {});",
  "(arr?.['reduce'])((acc, x) => [...acc, x])",
  "((arr?.reduce)!)((acc, x) => [...acc, x], [])",
  "(arr?.reduce as any)((acc, x) => [...acc, x], [])",
  "arr?.reduce((acc, x) => [...acc, x], [])",
  "(arr['reduceRight'])((acc, x) => [...acc, x], [])",
  "Object.keys(obj).reduce((acc, key) => ({ ...acc, [key]: obj[key] }), ({} as foo))",
  "arr.reduce((differentName, x) => ({ ...differentName, [x]: x }), {})",
  "a.b.c.d.reduce((acc,x) => ([...acc, x]), (([]) as foo))",
  "get_array().reduce((acc, x) => ({ ...acc, [x]: x }), {})",
  "arr.reduce(function (acc, x) { return { ...acc, [x]: x } }, {})",
  "arr.reduce((acc, x) => { let temp = { ...acc, x }; return temp }, {})",
  "foo.reduce((acc, bar) => {return [...acc, ...bar];}, [])",
  "let foo = []; for (let i in [1,2,3]) { foo = [...foo, i]; }",
  "let foo = []; for (const i = 0; i < 10; i++) { foo = [...foo, i]; }",
  "let foo = {}; for (let i of [1,2,3]) { foo = { ...foo, [i]: i }; }",
  "let foo = {}; while (Object.keys(foo).length < 10) { foo = { ...foo, [Object.keys(foo).length]: Object.keys(foo).length }; }",
];

tester.run("anti-slop/no-accumulating-spread", noAccumulatingSpreadRule, {
  valid: [
    "items.reduce((sum, item) => sum + item.size, 0);",
    "items.reduce((acc, item) => { acc.push(item); return acc; }, []);",
    "items.reduce((acc, item) => [...item.children], []);",
    "items.map((acc) => [...acc]);",
    "let all = []; all = [...all, 1];",
    "let list = []; for (const item of items) { const copy = [...list, item]; use(copy); }",
    "for (const item of items) { let all = []; all = [...all, item]; }",
    "items.reduce((acc, item) => { const acc2 = item; return [...acc2]; }, []);",
    "let cur = start; while (cur) { const n = { ...cur }; use(n); cur = cur.next; }",
    "async function f() { let page = first; while (page) { render({ ...page }); page = await next(page); } }",
    "let state = init; for (const e of events) { state = reduce(state, e); log({ ...state }); }",
    "let row = {}; for (const c of cols) { const r = { ...row }; r[c] = 1; out.push(r); row = {}; }",
    ...oxcValid,
  ],
  invalid: [
    {
      name: "spread in a reducer with a this parameter",
      code: "arr.reduce(function (this: unknown, acc: number[], x: number) { return [...acc, x]; }, []);",
      errors: [error],
    },
    { name: "array spread in an arrow reducer", code: "items.reduce((acc, item) => [...acc, item], []);", errors: [error] },
    { name: "object spread in a reducer", code: "items.reduce((acc, item) => ({ ...acc, [item.id]: item }), {});", errors: [error] },
    { name: "spread in a returned value", code: "items.reduceRight(function (acc, item) { return [...acc, item]; }, []);", errors: [error] },
    { name: "array spread in a for-of loop", code: "let all = []; for (const item of items) { all = [...all, item]; }", errors: [error] },
    { name: "object spread in a while loop", code: "let map = {}; while (next()) { map = { ...map, key: 1 }; }", errors: [error] },
    {
      name: "spread in a branch of a conditional",
      code: "items.reduce((acc: number[], n) => (n > 0 ? [...acc, n] : acc), []);",
      errors: [error],
    },
    {
      name: "spread under satisfies",
      code: "items.reduce((acc: number[], n) => [...acc, n] satisfies number[], []);",
      errors: [error],
    },
    {
      name: "spread into a local that the reducer returns",
      code: "items.reduce<number[]>((acc, n) => { const next = [...acc, n]; return next }, []);",
      errors: [error],
    },
    {
      name: "spread into a local that the loop assigns to the accumulator",
      code: "let all: number[] = []; for (const n of items) { const next = [...all, n]; all = next; }",
      errors: [error],
    },
    {
      name: "spread in a callback in a loop",
      code: "let list = []; for (const item of items) { run(() => { list = [...list, item]; }); }",
      errors: [error],
    },
    { name: "accumulator with a default", code: "items.reduce((acc = [], n) => [...acc, n]);", errors: [error] },
    ...oxcInvalid.map((code) => ({ name: `oxc: ${code}`, code, errors: [error] })),
  ],
});
