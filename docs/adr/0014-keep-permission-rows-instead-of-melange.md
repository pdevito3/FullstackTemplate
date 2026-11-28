# Keep the permission rows, and try melange only for sharing

The permissions stay as ADR 0009 describes them. `IPermissionGuard` loads the rows of the caller in `user_permissions` and `agent_permissions`, and `Caller.Has` decides. We do not move these checks to [melange](https://github.com/pthm/melange), which compiles an OpenFGA model into PostgreSQL functions that read a `melange_tuples` view over the app tables.

If a feature needs sharing, such as a workshop that is shared with users, teams, and teams in teams, melange and SQL by hand are the two options to try for that feature. In the spike below, melange was faster for one check, and its rules were one `.fga` model that walks teams in teams and stops on cycles. SQL by hand was more than 100 times faster for a list with melange 0.11.0, and about 25 times faster with PR #149 and the expression indexes.

A spike compared both forms on the schema of the EF migrations, on PostgreSQL 17, with melange 0.11.0, 2,000 tenants, 100,000 users, and 40,000 workshops. Each time below is a mean or a median inside the database, with a warm cache and no network round trip.

## Today's permissions in melange

Each permission was a relation of `tenant` for users and agents. `workshop.can_change` was `(organizer or manage_tenant from tenant) and manage_workshops from tenant`.

- Melange gave the same answer as the C# rules in each of 22 cases by hand, 40,000 tenant checks, and 49,999 workshop checks.
- One check took about 23 µs. The query of the guard today takes about 19 µs.
- `Caller.Permissions`, which `AllowedActions` and the MCP filter read, needs each permission. With `check_permission_bulk` that took about 270 µs, about 14 times the query of today.
- A list of the workshops that a user can change took about 19 ms. The same list in SQL took 0.14 ms.
- The model compiles to 85 functions and about 292 KB of SQL. A new permission is a migration of about 157 KB.

Some rules stay in C# in each case:

- A delegated agent holds the permissions that both its user and its grant have. A check of OpenFGA has one subject, so this needs two checks and C# code, or a view branch that joins the rows of the user and of the grant. The spike did not model it.
- A caller that changes the role of a user, takes a permission away from them, or deletes them must hold each permission of that user (ADR 0009). This compares two sets, which OpenFGA cannot express.
- `AllTenantsManager` and `TenantJob` are not rows, so the view cannot give their permissions.

## Sharing in melange

The spike added teams that hold users and other teams, workshop collaborators who are editors or viewers, and venue managers. A venue manager can change each workshop at the venue. Two product rules were assumed: a workshop is visible only to the people that it is shared with and to the holders of `manage_workshops`, and a shared editor or a venue manager needs no tenant permission to change it.

- Melange and SQL by hand gave the same answer in 21 cases by hand, 20,000 checks, and 300 lists. The cases cover three levels of teams, a cycle of two teams, and grants that point to another tenant.
- A check of `can_change` took about 216 µs. The same check in SQL by hand took about 1.4 ms. That SQL was not tuned.
- A list of the workshops that a user can view took 130 to 316 ms. The same list in SQL took about 0.7 to 1.0 ms.
- A page of 20 workshops, with the tenant filter and one check for each row, took about 9 ms. Each tenant had about 20 workshops, so this checked each workshop of the tenant. The cost grows with the workshops of the tenant, not with the size of the page.

The lists are slow because the walk through teams in teams reads each nested membership in the database, in each tenant. Melange PR [#149](https://github.com/pthm/melange/pull/149) fixes the walk. With it, and an index on each `team_id || '#member'` expression, the list took about 20 ms. Without the indexes it took about 100 ms. Issue [#148](https://github.com/pthm/melange/issues/148) describes the rest of the cost, the `from tenant` lookups inside an `and`. A second fix brought the list to about 1.4 ms, but each shape of it that issue #148 describes has a cost elsewhere, such as a SaaS list benchmark of melange about 9% slower, so it is not in the PR.

## Considered options

- **Replace the permission rows with melange.** We did not choose this option. A check costs the same, the set of permissions costs about 14 times more, and the rules of delegated agents, of the set that a caller can take away, and of system callers stay in C#. The app gets 85 generated functions to migrate and no new rule that it needs.
- **Use melange for the lists of a sharing feature.** We did not choose this option yet. With #149 and the expression indexes a list takes about 20 ms. Without the indexes, or without #149, it takes more than 100 ms.
- **Write the sharing rules in SQL by hand.** This stays an option. For one flat collaborators table, with no teams in teams, an `EXISTS` in the EF query is probably enough, and melange does not pay off. The spike did not build this form.
- **A separate OpenFGA server.** We did not choose this option. The tuples must then be copied from the app tables, and a check is a network call. Melange reads the tables in the same transaction.

## Consequences

- ADR 0009 does not change for the permissions of a tenant. A new permission is still a name in `Permission`, a check in the handler, and a migration that grants it.
- A sharing check is a second decision beside `Caller.Has`. `AllowedActions` of a shared entity must take the result of `check_permission`, so a list pays one check for each row and each relation.
- A feature that adopts melange for sharing must:
  - write the `melange_tuples` view so that it repeats the soft delete filter and the tenant of each row. The view does not run the EF query filters.
  - add an index on each subject expression of the view, such as `team_id || '#member'`. `melange doctor` does not report it.
  - add a test that each relation that the code checks is in the `.fga` file. `check_permission` returns a deny for an unknown relation, with no error.
  - wait for #149 or carry its fix, add the expression indexes, and measure the list against SQL by hand before a list calls `list_accessible_objects`.
- A check for each row is the fallback for a list only in a tenant with few rows. It must run in the query, so that the count of the pager stays right.
