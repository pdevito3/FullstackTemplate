# A regular repo instead of a dotnet new template

The repo is a regular repo, not a `dotnet new` template. A user clones it, or uses the GitHub template button, and then runs `dotnet run scripts/init.cs`. The init script replaces the template name with the name of the new app and sets the default auth provider. All five auth providers stay in the code, and the `AuthProvider` setting of the AppHost selects one at runtime. As a result, the code that a user gets is the code that the repo runs and tests.

Each provider is one folder in `FullstackTemplate.AppHost/AuthProviders/`. The folder holds the factory class and the seed files. The AppHost finds the providers by reflection, and the provider name comes from the class name. The e2e selectors of each provider are one file in `frontend/e2e/idps/`. To delete a provider, delete its folder and its e2e file. If the provider is the default in `appsettings.json`, select another default. No other code names a provider.

The BFF and the server have no provider factory. They read generic `Auth:*` settings, and the BFF reads the discovery document for the optional features ([ADR 0005](0005-provider-features-by-discovery.md)). The AppHost factory is the only place that turns a provider name into these settings.

## Considered options

- **Keep the template.** We did not choose this option. The repo selected the provider at runtime with `AuthProvider` and `#if LOCAL_DEV`, and a project from the template selected it with `//#if (UseX)` blocks in five files. Thus the code of a new project was not the code that the repo ran. The only test of a provider combination was to pack the template and create a project, and a build failure in a new project showed that this test did not occur.
- **A factory for each provider in the BFF and the server too.** We did not choose this option. The provider differences are values (claim types, scopes, the logout endpoint), and the AppHost already writes them as settings. A second factory would hold the same knowledge in a second place.
- **An explicit `switch` over the provider names in `AppHost.cs`.** We did not choose this option. Each new or deleted provider would then need a change in a second file.
- **A `--prune` option in the init script that deletes the other providers.** We did not choose this option. The code of an unused provider is small, and runtime selection needs it. To delete a provider by hand is two deletions and at most one changed setting, so a script option adds code and test cost for little gain.

## Consequences

- `dotnet new install` and the NuGet package go away. `.template.config/`, `template.csproj`, and `scripts/template-smoke-test.sh` are deleted.
- `northflank.json` depended on the template conditionals, so it is deleted with its doc.
- The `.sln` file is deleted. The template never gave it to a new project, and the `.slnx` file has no project GUIDs. The init script gives the AppHost a new `UserSecretsId`, so that two apps from this repo do not share user secrets.
- A second run of the init script with the same arguments changes nothing. The script refuses to rename an app that it renamed before.
- CI runs the init script with a dotted name, makes sure that a second run changes nothing, and then builds and tests the renamed app. The unit tests of the AppHost build the resources of each provider. This job replaces the template smoke test.
- The init script changes only a clean git tree, so `git reset --hard && git clean -fd` undoes a failed run.
- The rename keeps the links to the upstream repo, and the init script deletes the init section of the README. Thus the docs of a new app do not describe its rename as a task to do.
- The rename deletes the files that only the repo uses: the CI job of the init script, and the OpenID conformance suite (`conformance.yml`, `frontend/conformance/`, the `Conformance` provider, and the userinfo check of the server). Code that the rename must cut out of a shared file is between `init:start` and `init:end` comments. CI makes sure that none of it stays in the renamed app.
