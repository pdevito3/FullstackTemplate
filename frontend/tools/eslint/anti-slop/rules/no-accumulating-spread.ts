import { defineRule } from "@oxlint/plugins";
import type { ESTree, Variable } from "@oxlint/plugins";

import { arrayMethodTarget, resolveArrayBinding, unwrapArrayExpression } from "../shared/array-method.ts";

const REDUCERS = new Set(["reduce", "reduceRight"]);

const LOOPS = new Set(["ForStatement", "ForInStatement", "ForOfStatement", "WhileStatement", "DoWhileStatement"]);

const FUNCTIONS = new Set(["FunctionDeclaration", "FunctionExpression", "ArrowFunctionExpression"]);

function enclosingFunction(node: ESTree.Node): ESTree.Node | null {
  for (let at = node.parent; at; at = at.parent) {
    if (FUNCTIONS.has(at.type)) return at;
  }

  return null;
}

function contains(outer: ESTree.Node, inner: ESTree.Node): boolean {
  return outer.range[0] <= inner.range[0] && inner.range[1] <= outer.range[1];
}

// As oxc: the first parameter of a callback with 2 to 4 parameters, which is the first of 1 or 2 arguments of a
// reduce or reduceRight call, and no function between the spread and that callback.
function isReducerAccumulator(variable: Variable, spread: ESTree.SpreadElement): boolean {
  const definition = variable.defs[0];
  if (definition?.type !== "Parameter") return false;

  const fn = definition.node;
  if (fn.type !== "ArrowFunctionExpression" && fn.type !== "FunctionExpression") return false;
  if (fn.params.length < 2 || fn.params.length > 4) return false;

  const first = fn.params[0];
  const binding = first?.type === "AssignmentPattern" ? first.left : first;
  if (binding !== definition.name || enclosingFunction(spread) !== fn) return false;

  let call: ESTree.Node | null = fn.parent;
  while (call !== null && unwrapArrayExpression(call) === fn) call = call.parent;
  if (call?.type !== "CallExpression" || call.arguments.length > 2) return false;

  const callback = call.arguments[0];
  const method = arrayMethodTarget(call.callee);

  return callback !== undefined && unwrapArrayExpression(callback) === fn && method !== null && REDUCERS.has(method.name);
}

// As oxc: a loop around the spread that does not hold the declaration of the variable. Unlike oxc, the loop may
// assign the variable anywhere, not only with the spread on the right side, as in `const next = [...all, n]; all = next`.
function isLoopAccumulator(variable: Variable, spread: ESTree.SpreadElement): boolean {
  const declaration = variable.identifiers[0];
  if (declaration === undefined) return false;

  const writes = variable.references.filter((reference) => reference.isWrite() && !reference.init);

  for (let at: ESTree.Node | null = spread.parent; at; at = at.parent) {
    if (!LOOPS.has(at.type) || contains(at, declaration)) continue;

    const loop = at;
    if (writes.some((reference) => contains(loop, reference.identifier))) return true;
  }

  return false;
}

export const noAccumulatingSpreadRule = defineRule({
  meta: {
    type: "problem",
    docs: {
      description: "Disallow a spread of the accumulator in a reducer or a loop, which copies it on each step.",
    },
    messages: {
      accumulatingSpread:
        "This spread copies `{{name}}` on each step, so the work grows with the square of the items. Push into or assign to `{{name}}` in place, or build the result with one `map`, `flatMap`, or `Object.fromEntries`.",
    },
  },
  createOnce(context) {
    return {
      SpreadElement(spread) {
        if (spread.argument.type !== "Identifier") return;

        const variable = resolveArrayBinding(context.sourceCode, spread.argument);
        if (variable === null) return;

        if (isReducerAccumulator(variable, spread) || isLoopAccumulator(variable, spread))
          context.report({ node: spread, messageId: "accumulatingSpread", data: { name: spread.argument.name } });
      },
    };
  },
});
