/**
 * Reads the stack of this checkout from the result of `dotnet aspire describe --format Json`. e2e/run-against-stack.ts
 * runs the command and gives the result to checkStack.
 */
import type { SpawnSyncReturns } from "node:child_process";
import * as z from "zod";

const resourceSchema = z.looseObject({
  displayName: z.string(),
  state: z.string().nullish(),
  healthStatus: z.string().nullish(),
  urls: z
    .array(
      z.looseObject({ name: z.string().nullish(), url: z.string().nullish() }),
    )
    .nullish(),
});

const descriptionSchema = z.object({ resources: z.array(resourceSchema) });

export type StackResource = z.infer<typeof resourceSchema>;

export type DescribeRun = Pick<
  SpawnSyncReturns<string>,
  "status" | "stdout" | "error"
>;

export type StackCheck =
  | { kind: "ready"; resources: StackResource[] }
  | { kind: "not-ready"; message: string };

// The resources that the stack tests use. CI waits until each one is healthy before it runs the tests.
const neededResources = ["server", "bff", "webfrontend"];
const neededList = `${neededResources.slice(0, -1).join(", ")}, and ${neededResources.at(-1)}`;

export function checkStack({
  run,
  appHost,
}: {
  run: DescribeRun;
  appHost: string;
}): StackCheck {
  const startHint = [
    `Start the stack, and wait until ${neededList} are healthy:`,
    `  dotnet aspire start --isolated --non-interactive --apphost ${appHost}`,
    `  ${waitCommand(neededResources, appHost)}`,
    "Then run pnpm test:e2e:stack again. To select the identity provider, see docs/authentication.md.",
  ].join("\n");
  const readHint = [
    `To see the output, run dotnet aspire describe --format Json --apphost ${appHost}.`,
    "If the Aspire CLI version does not match Aspire.AppHost.Sdk, run dotnet tool restore.",
  ].join("\n");
  const notReady = (...lines: string[]): StackCheck => ({
    kind: "not-ready",
    message: lines.join("\n"),
  });

  if (run.error) {
    return notReady(
      `The script cannot run dotnet aspire describe: ${run.error.message}`,
      "If dotnet is missing, install the .NET SDK. Then run dotnet tool restore.",
    );
  }
  if (run.status !== 0) {
    const stdout = run.stdout.trim();
    return notReady(
      `dotnet aspire describe failed with exit code ${run.status}. Read its output ${stdout === "" ? "above" : "above and below"}.`,
      ...(stdout === "" ? [] : [stdout]),
      "If the Aspire CLI is missing, run dotnet tool restore.",
    );
  }
  if (run.stdout.trim() === "") {
    return notReady("No Aspire stack runs for this checkout.", startHint);
  }

  let json: unknown;
  try {
    json = JSON.parse(run.stdout);
  } catch (error) {
    return notReady(
      `dotnet aspire describe gave output that the script cannot read. It is not JSON: ${String(error)}`,
      readHint,
    );
  }
  const parsed = descriptionSchema.safeParse(json);
  if (!parsed.success) {
    return notReady(
      "dotnet aspire describe gave output that the script cannot read:",
      z.prettifyError(parsed.error),
      readHint,
    );
  }
  const resources = parsed.data.resources;

  const missing = neededResources.filter(
    (name) => !resources.some((r) => r.displayName === name),
  );
  if (missing.length > 0) {
    return notReady(
      `The stack has no ${missing.join(" or ")} resource. The stack tests need ${neededList}.`,
      startHint,
    );
  }

  const unhealthy = resources.filter(
    (r) =>
      neededResources.includes(r.displayName) &&
      (r.state !== "Running" || r.healthStatus !== "Healthy"),
  );
  if (unhealthy.length > 0) {
    return notReady(
      "These resources of the stack are not healthy:",
      ...unhealthy.map(
        (r) =>
          `  ${r.displayName} (state ${r.state ?? "unknown"}, health ${r.healthStatus ?? "unknown"})`,
      ),
      "Wait until they are healthy, then run pnpm test:e2e:stack again:",
      `  ${waitCommand(
        unhealthy.map((r) => r.displayName),
        appHost,
      )}`,
      `If a resource stays unhealthy, read its logs: dotnet aspire logs <resource> --apphost ${appHost}`,
    );
  }

  return { kind: "ready", resources };
}

function waitCommand(resources: string[], appHost: string): string {
  return `for resource in ${resources.join(" ")}; do dotnet aspire wait "$resource" --status healthy --timeout 900 --apphost ${appHost}; done`;
}
