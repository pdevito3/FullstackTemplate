import { defineRule } from "@oxlint/plugins";
import type { ESTree } from "@oxlint/plugins";

const REDUCERS = new Set(["reduce", "reduceRight"]);

const LOOPS = new Set(["ForStatement", "ForInStatement", "ForOfStatement", "WhileStatement", "DoWhileStatement"]);

function spreadsName(collection: ESTree.Node | null | undefined, name: string): boolean {
  if (collection?.type !== "ArrayExpression" && collection?.type !== "ObjectExpression") return false;

  const items: readonly (ESTree.Node | null)[] =
    collection.type === "ArrayExpression" ? collection.elements : collection.properties;

  return items.some(
    (item) => item?.type === "SpreadElement" && item.argument.type === "Identifier" && item.argument.name === name,
  );
}

function reducerAccumulator(fn: ESTree.Node): string | null {
  const call = fn.parent;
  if (call?.type !== "CallExpression" || call.arguments[0] !== fn) return null;
  if (call.callee.type !== "MemberExpression" || call.callee.property.type !== "Identifier") return null;
  if (!REDUCERS.has(call.callee.property.name)) return null;

  const params = "params" in fn ? fn.params : [];
  const first = params[0];

  return first?.type === "Identifier" ? first.name : null;
}

const FUNCTIONS = new Set(["FunctionDeclaration", "FunctionExpression", "ArrowFunctionExpression"]);

function enclosingFunction(node: ESTree.Node): ESTree.Node | null {
  for (let at = node.parent; at; at = at.parent) {
    if (FUNCTIONS.has(at.type)) return at;
  }

  return null;
}

function insideLoop(node: ESTree.Node): boolean {
  for (let at = node.parent; at; at = at.parent) {
    if (LOOPS.has(at.type)) return true;
    if (FUNCTIONS.has(at.type)) return false;
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
    const report = (node: ESTree.Node, name: string) =>
      context.report({ node, messageId: "accumulatingSpread", data: { name } });

    return {
      ArrowFunctionExpression(fn) {
        const name = reducerAccumulator(fn);
        if (name !== null && fn.body.type !== "BlockStatement" && spreadsName(fn.body, name)) report(fn.body, name);
      },
      ReturnStatement(node) {
        const fn = enclosingFunction(node);
        const name = fn && reducerAccumulator(fn);
        if (name && spreadsName(node.argument, name) && node.argument) report(node.argument, name);
      },
      AssignmentExpression(node) {
        if (node.operator !== "=" || node.left.type !== "Identifier") return;
        if (spreadsName(node.right, node.left.name) && insideLoop(node)) report(node.right, node.left.name);
      },
    };
  },
});
