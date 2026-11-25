import { RuleTester } from "../rule-tester.ts";

import { noAccumulatingSpreadRule } from "./no-accumulating-spread.ts";

const tester = new RuleTester();
const error = { messageId: "accumulatingSpread" };

tester.run("anti-slop/no-accumulating-spread", noAccumulatingSpreadRule, {
  valid: [
    "items.reduce((sum, item) => sum + item.size, 0);",
    "items.reduce((acc, item) => { acc.push(item); return acc; }, []);",
    "items.reduce((acc, item) => [...item.children], []);",
    "items.map((acc) => [...acc]);",
    "let all = []; all = [...all, 1];",
    "for (const item of items) { const copy = [...list, item]; use(copy); }",
    "for (const item of items) { run(() => { list = [...list, item]; }); }",
  ],
  invalid: [
    { name: "array spread in an arrow reducer", code: "items.reduce((acc, item) => [...acc, item], []);", errors: [error] },
    { name: "object spread in a reducer", code: "items.reduce((acc, item) => ({ ...acc, [item.id]: item }), {});", errors: [error] },
    { name: "spread in a returned value", code: "items.reduceRight(function (acc, item) { return [...acc, item]; }, []);", errors: [error] },
    { name: "array spread in a for-of loop", code: "let all = []; for (const item of items) { all = [...all, item]; }", errors: [error] },
    { name: "object spread in a while loop", code: "let map = {}; while (next()) { map = { ...map, key: 1 }; }", errors: [error] },
  ],
});
