namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

public class QueryInputAnalyzerTests
{
    [Test]
    public async Task a_request_text_in_a_query_of_the_past_mistake_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return await dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name == request.Dto.Name, cancellationToken);
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTQRY001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            "The query compares the request value 'request.Dto.Name', which can hold a NUL that Postgres cannot take. Compare the entity value, or check the input with EntityId.IsValid or PostgresText.CanStore first. For a list, check each item. See docs/backend/features-and-cqrs.md.");
    }

    [Test]
    public async Task the_value_of_the_entity_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var tenant = Create(request.Dto.Name);
            return await dbContext.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name == tenant.Name, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_request_id_in_a_query_of_the_past_mistake_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var users = await dbContext.Users
                .AsNoTracking()
                .Where(e => e.TenantId == request.TenantId)
                .ToListAsync(cancellationToken);
            return users.Count > 0;
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTQRY001");
    }

    [Test]
    public async Task a_request_id_that_entity_id_is_valid_checks_first_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            if (!EntityId.IsValid(request.TenantId, Tenant.IdPrefix))
                return false;

            var users = await dbContext.Users
                .AsNoTracking()
                .Where(e => e.TenantId == request.TenantId)
                .ToListAsync(cancellationToken);
            return users.Count > 0;
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_request_text_that_postgres_text_can_store_checks_first_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return PostgresText.CanStore(request.Identifier)
                && await dbContext.Users.AnyAsync(u => u.Identifier == request.Identifier, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_check_after_the_query_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var found = await dbContext.Users.AnyAsync(u => u.Identifier == request.Identifier, cancellationToken);
            return found && PostgresText.CanStore(request.Identifier);
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTQRY001");
    }

    [Test]
    public async Task a_check_of_another_value_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return PostgresText.CanStore(request.Dto.Name)
                && await dbContext.Users.AnyAsync(u => u.Identifier == request.Identifier, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTQRY001");
    }

    [Test]
    [Arguments("u => u.Identifier != request.Identifier")]
    [Arguments("u => request.Identifier == u.Identifier")]
    [Arguments("u => u.Identifier.Equals(request.Identifier)")]
    [Arguments("u => string.Equals(u.Identifier, request.Identifier)")]
    [Arguments("u => u.UserPermissions.Any(p => u.Identifier == request.Identifier)")]
    public async Task each_compare_of_a_request_text_in_a_query_gets_fstqry001(string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Identifier");
    }

    [Test]
    [Arguments("u => u.Identifier == request.Identifier.Trim()", "request.Identifier.Trim()")]
    [Arguments("u => u.Identifier == request.Identifier.TrimEnd().ToLowerInvariant()", "request.Identifier.TrimEnd().ToLowerInvariant()")]
    [Arguments("u => u.Identifier.ToUpper() == request.Identifier.ToUpper()", "request.Identifier.ToUpper()")]
    [Arguments("u => u.Identifier.StartsWith(request.Identifier)", "request.Identifier")]
    [Arguments("u => u.Identifier.EndsWith(request.Identifier)", "request.Identifier")]
    [Arguments("u => u.Identifier.Contains(request.Identifier)", "request.Identifier")]
    [Arguments("u => EF.Functions.Like(u.Identifier, request.Identifier)", "request.Identifier")]
    [Arguments("u => EF.Functions.ILike(u.Identifier, request.Identifier)", "request.Identifier")]
    [Arguments("u => request.Ids.Contains(u.Id)", "request.Ids")]
    [Arguments("""u => EF.Functions.ILike(u.Identifier, $"%{request.Identifier}%")""", "request.Identifier")]
    [Arguments("u => EF.Functions.ILike(u.Identifier, LikePattern.Containing(request.Identifier), LikePattern.EscapeCharacter)", "request.Identifier")]
    [Arguments("u => EF.Functions.Like(u.Identifier, LikePattern.StartingWith(request.Identifier.Trim()), LikePattern.EscapeCharacter)", "request.Identifier.Trim()")]
    [Arguments("""
        u => u.Identifier == request.Identifier + "-1"
        """, "request.Identifier")]
    [Arguments("""u => u.Identifier.StartsWith("a" + request.Identifier + "b")""", "request.Identifier")]
    public async Task each_query_operator_that_sends_a_request_text_gets_fstqry001(string predicate, string text)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe(text);
    }

    [Test]
    [Arguments("u => u.Identifier == request.Identifier.Trim()")]
    [Arguments("u => u.Identifier.StartsWith(request.Identifier)")]
    [Arguments("u => EF.Functions.ILike(u.Identifier, request.Identifier)")]
    [Arguments("""u => EF.Functions.ILike(u.Identifier, $"%{request.Identifier}%")""")]
    [Arguments("u => EF.Functions.ILike(u.Identifier, LikePattern.Containing(request.Identifier), LikePattern.EscapeCharacter)")]
    [Arguments("""
        u => u.Identifier == request.Identifier + "-1"
        """)]
    public async Task a_request_text_that_postgres_text_can_store_checks_first_has_no_diagnostics_in_each_operator(string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            return PostgresText.CanStore(request.Identifier)
                && await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_like_pattern_of_the_entity_value_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var tenant = Create(request.Dto.Name);
            return await dbContext.Users.AnyAsync(u => EF.Functions.ILike(u.Identifier, LikePattern.Containing(tenant.Name) + $"{tenant.Id}%", LikePattern.EscapeCharacter), cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_check_of_a_text_after_a_trim_of_given_characters_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return PostgresText.CanStore(request.Identifier.Trim('\0'))
                && await dbContext.Users.AnyAsync(u => u.Identifier == request.Identifier, cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Identifier");
    }

    [Test]
    [Arguments("if (!request.Ids.All(id => EntityId.IsValid(id, Tenant.IdPrefix))) return false;")]
    [Arguments("if (!request.Ids.All(PostgresText.CanStore)) return false;")]
    [Arguments("foreach (var id in request.Ids) { if (!EntityId.IsValid(id, Tenant.IdPrefix)) return false; }")]
    [Arguments("foreach (var id in request.Ids ?? []) { if (!EntityId.IsValid(id, Tenant.IdPrefix)) return false; }")]
    [Arguments("foreach (var raw in request.Ids) { var id = raw.Trim(); if (!EntityId.IsValid(id, Tenant.IdPrefix)) return false; }")]
    [Arguments("for (var i = 0; i < request.Ids.Length; i++) { if (!EntityId.IsValid(request.Ids[i], Tenant.IdPrefix)) return false; }")]
    [Arguments("if (request.Ids?.All(id => EntityId.IsValid(id, Tenant.IdPrefix)) == false) return false;")]
    [Arguments("if (request.Ids?.All(PostgresText.CanStore) == false) return false;")]
    public async Task a_request_list_that_a_check_of_each_item_checks_first_has_no_diagnostics(string check)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            {check}
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("if (!EntityId.IsValid(request.Ids[0], Tenant.IdPrefix)) return false;")]
    [Arguments("var first = request.Ids[0]; if (!EntityId.IsValid(first, Tenant.IdPrefix)) return false;")]
    public async Task a_check_of_one_item_of_a_request_list_gets_fstqry001(string check)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            {check}
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Ids");
    }

    [Test]
    [Arguments("""var t = id; t = "x";""")]
    [Arguments("""var t = id; t += "x";""")]
    [Arguments("""var t = id; (t, _) = ("x", 1);""")]
    public async Task a_check_of_a_copy_of_each_item_that_the_code_assigns_again_gets_fstqry001(string copy)
    {
        // Act
        var diagnostics = await RunInHandler($$"""
            foreach (var id in request.Ids) { {{copy}} if (!PostgresText.CanStore(t)) return false; }
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Ids");
    }

    [Test]
    [Arguments("""
        u => request.Filter == null || u.Identifier == "a"
        """)]
    [Arguments("""
        u => request.Filter != null && u.Identifier == "a"
        """)]
    [Arguments("""
        u => null == request.Filter || u.Identifier == "a"
        """)]
    public async Task a_null_check_of_a_request_text_in_a_query_has_no_diagnostics(string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_compare_of_a_request_text_next_to_a_null_check_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return await dbContext.Users.AnyAsync(u => request.Filter == null || u.Identifier == request.Filter, cancellationToken);
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Location.SourceSpan.Start.ShouldBeGreaterThan(diagnostic.Location.SourceTree!.GetText().ToString().IndexOf("u.Identifier ==", StringComparison.Ordinal));
        Text(diagnostic).ShouldBe("request.Filter");
    }

    [Test]
    [Arguments("""u => u.Identifier == (request.Filter ?? "")""", "request.Filter")]
    [Arguments("""u => (request.Filter ?? "") == u.Identifier""", "request.Filter")]
    [Arguments("""u => u.Identifier == (request.Filter ?? "").Trim()""", "request.Filter")]
    [Arguments("""u => u.Identifier.StartsWith(request.Filter ?? "")""", "request.Filter")]
    [Arguments("""u => EF.Functions.ILike(u.Identifier, $"%{request.Filter ?? ""}%")""", "request.Filter")]
    [Arguments("u => (request.Ids ?? new string[0]).Contains(u.Id)", "request.Ids")]
    [Arguments("""u => u.Identifier == (request.Filter == null ? "" : request.Filter)""", "request.Filter")]
    [Arguments("""u => u.Identifier == (u.Id == "a" ? request.Identifier : "b")""", "request.Identifier")]
    [Arguments("""u => u.Identifier == (u.Id == "a" ? "b" : request.Identifier)""", "request.Identifier")]
    public async Task a_request_text_through_a_coalesce_or_a_conditional_gets_fstqry001(string predicate, string text)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe(text);
    }

    [Test]
    public async Task each_request_text_in_a_coalesce_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return await dbContext.Users.AnyAsync(u => u.Identifier == (request.Filter ?? request.Identifier), cancellationToken);
            """);

        // Assert
        diagnostics.Select(Text).ShouldBe(["request.Filter", "request.Identifier"]);
    }

    [Test]
    [Arguments("""if (!PostgresText.CanStore(request.Filter ?? "")) return false;""", """u => u.Identifier == (request.Filter ?? "")""")]
    [Arguments("""if (!PostgresText.CanStore((request.Filter ?? "").Trim())) return false;""", """u => u.Identifier == (request.Filter ?? "").Trim()""")]
    [Arguments("if (request.Filter != null && !PostgresText.CanStore(request.Filter)) return false;", """u => u.Identifier == (request.Filter ?? "")""")]
    [Arguments("if (!PostgresText.CanStore(request.Identifier)) return false;", """u => u.Identifier == (u.Id == "a" ? request.Identifier : "b")""")]
    [Arguments("", """u => u.Identifier == (PostgresText.CanStore(request.Identifier) ? request.Identifier : "")""")]
    [Arguments("", """u => u.Identifier == (request.Filter == null ? "a" : "b")""")]
    [Arguments("var tenant = Create(request.Dto.Name);", """u => u.Identifier == (tenant.Name ?? "")""")]
    [Arguments("var tenant = Create(request.Dto.Name);", """u => u.Identifier == (u.Id == "a" ? tenant.Name : u.Id)""")]
    [Arguments("""if (!PostgresText.CanStore(request.Filter?.Trim() ?? "")) return false;""", """u => u.Identifier == (request.Filter ?? "").Trim()""")]
    [Arguments("""if (!PostgresText.CanStore(request.Filter?.Trim() ?? "")) return false;""", """u => u.Identifier == (request.Filter ?? "")""")]
    [Arguments("""if (!PostgresText.CanStore(request.Filter?.Trim().ToLower() ?? "")) return false;""", """u => u.Identifier == (request.Filter ?? "")""")]
    [Arguments("""if (!PostgresText.CanStore(request.Filter?.Trim()?.ToLower() ?? "")) return false;""", """u => u.Identifier == (request.Filter ?? "")""")]
    [Arguments("""if (!PostgresText.CanStore((request.Filter?.Trim() ?? "").ToLower())) return false;""", """u => u.Identifier == (request.Filter ?? "")""")]
    [Arguments("""if (!PostgresText.CanStore(request.Dto?.Name?.Trim() ?? "")) return false;""", """u => u.Identifier == request.Dto.Name""")]
    public async Task a_coalesce_or_a_conditional_with_no_unchecked_request_text_has_no_diagnostics(string arrange, string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            {arrange}
            return await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_check_of_the_default_of_a_coalesce_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            if (!PostgresText.CanStore(request.Filter ?? request.Identifier))
                return false;
            return await dbContext.Users.AnyAsync(u => u.Identifier == request.Identifier, cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Identifier");
    }

    [Test]
    [Arguments("""PostgresText.CanStore(request.Filter ?? request.Identifier)""", """u => u.Identifier == (request.Filter ?? request.Identifier)""")]
    [Arguments("""PostgresText.CanStore(request.Filter ?? request.Identifier)""", """u => u.Identifier == (request.Filter ?? request.Identifier).Trim()""")]
    [Arguments("""PostgresText.CanStore((request.Filter ?? request.Identifier).Trim())""", """u => u.Identifier == (request.Filter ?? request.Identifier)""")]
    [Arguments("""PostgresText.CanStore(request.Filter ?? request.Identifier)""", """u => u.Identifier == (string)(request.Filter ?? request.Identifier)""")]
    [Arguments("""PostgresText.CanStore(request.Filter == null ? "" : request.Filter)""", """u => u.Identifier == (request.Filter == null ? "" : request.Filter)""")]
    [Arguments("""PostgresText.CanStore(string.Concat(request.Identifier, "-1"))""", """u => u.Identifier == string.Concat(request.Identifier, "-1")""")]
    [Arguments("""PostgresText.CanStore(string.Format("{0}", request.Identifier))""", """u => u.Identifier == string.Format("{0}", request.Identifier)""")]
    [Arguments("""new[] { request.Identifier, request.TenantId }.All(PostgresText.CanStore)""", """u => new[] { request.Identifier, request.TenantId }.Contains(u.Id)""")]
    [Arguments("""new[] { request.Identifier, request.TenantId }.All(id => PostgresText.CanStore(id))""", """u => new[] { request.Identifier, request.TenantId }.Contains(u.Id)""")]
    public async Task a_check_of_the_expression_that_the_query_compares_has_no_diagnostics(string check, string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            if (!{check})
                return false;
            return await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""PostgresText.CanStore(request.Filter ?? request.Identifier)""", """u => u.Identifier == (request.Filter ?? request.TenantId)""", "request.TenantId")]
    [Arguments("""PostgresText.CanStore(request.Filter ?? request.Identifier)""", """u => u.Identifier == (request.Filter ?? request.Identifier) + request.TenantId""", "request.TenantId")]
    [Arguments("""PostgresText.CanStore(request.Filter == null ? "" : request.Filter)""", """u => u.Identifier == (request.Filter == null ? "" : request.Identifier)""", "request.Identifier")]
    [Arguments("""PostgresText.CanStore(string.Concat(request.Identifier, "-1"))""", """u => u.Identifier == string.Concat(request.TenantId, "-1")""", "request.TenantId")]
    [Arguments("""new[] { request.Identifier, "a" }.All(PostgresText.CanStore)""", """u => new[] { request.TenantId, "a" }.Contains(u.Id)""", "request.TenantId")]
    [Arguments("""new[] { new { Dto = new NameDto("a") } }.All(request => PostgresText.CanStore(request.Dto.Name ?? ""))""", """u => u.Identifier == (request.Dto.Name ?? "")""", "request.Dto.Name")]
    public async Task a_check_of_another_expression_gets_fstqry001(string check, string predicate, string text)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            if (!{check})
                return false;
            return await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe(text);
    }

    [Test]
    public async Task a_check_of_the_same_expression_after_the_query_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var found = await dbContext.Users.AnyAsync(u => u.Identifier == (request.Filter ?? request.Identifier), cancellationToken);
            return found && PostgresText.CanStore(request.Filter ?? request.Identifier);
            """);

        // Assert
        diagnostics.Select(Text).ShouldBe(["request.Filter", "request.Identifier"]);
    }

    [Test]
    [Arguments("""PostgresText.CanStore(request.Filter?.Trim('\0') ?? "")""", """u => u.Identifier == (request.Filter ?? "")""", "request.Filter")]
    [Arguments("""PostgresText.CanStore(request.Filter?.Substring(1) ?? "")""", """u => u.Identifier == (request.Filter ?? "").Trim()""", "request.Filter")]
    [Arguments("""PostgresText.CanStore(request.Dto?.Name.Substring(1) ?? "")""", "u => u.Identifier == request.Dto.Name", "request.Dto.Name")]
    [Arguments("""PostgresText.CanStore(request.Filter?.Trim() ?? "")""", "u => u.Identifier == request.Identifier", "request.Identifier")]
    [Arguments("""PostgresText.CanStore(request.Filter?.Trim() ?? request.Identifier)""", "u => u.Identifier == request.Identifier", "request.Identifier")]
    public async Task a_check_of_a_conditional_access_that_is_not_the_request_text_gets_fstqry001(string check, string predicate, string text)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            if (!{check})
                return false;
            return await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe(text);
    }

    [Test]
    [Arguments("u => u.Identifier == (string)request.Identifier", "(string)request.Identifier")]
    [Arguments("""u => u.Identifier == string.Format("{0}", (object)request.Identifier)""", "(object)request.Identifier")]
    [Arguments("""u => u.Identifier == string.Concat((object)request.Identifier, "a")""", "(object)request.Identifier")]
    [Arguments("""u => u.Identifier == (string)(request.Filter ?? "")""", "request.Filter")]
    [Arguments("""u => u.Identifier == (u.Id == "a" ? (string)request.Identifier : "b")""", "(string)request.Identifier")]
    public async Task a_request_text_through_an_explicit_cast_gets_fstqry001(string predicate, string text)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe(text);
    }

    [Test]
    [Arguments("PostgresText.CanStore(request.Identifier)", """u => u.Identifier == string.Format("{0}", (object)request.Identifier)""")]
    [Arguments("PostgresText.CanStore((string)request.Identifier)", "u => u.Identifier == request.Identifier")]
    [Arguments("PostgresText.CanStore(request.Identifier)", "u => u.Identifier == (string)request.Identifier")]
    public async Task a_checked_request_text_through_an_explicit_cast_has_no_diagnostics(string check, string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            if (!{check})
                return false;
            return await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""u => u.Identifier == string.Concat(request.Identifier, "-1")""", "request.Identifier")]
    [Arguments("""u => u.Identifier == string.Concat(new[] { "a", request.Identifier })""", "request.Identifier")]
    [Arguments("u => u.Identifier == string.Concat(request.Ids)", "request.Ids")]
    [Arguments("""u => new[] { "a", request.Identifier }.Contains(u.Id)""", "request.Identifier")]
    [Arguments("""u => u.Identifier == string.Concat(request.Identifier, "-1").Trim()""", "request.Identifier")]
    [Arguments("""u => u.Identifier == string.Format("{0}", request.Identifier)""", "request.Identifier")]
    [Arguments("""u => u.Identifier == string.Format("{0}{1}", new object[] { 1, request.Identifier })""", "request.Identifier")]
    [Arguments("""u => u.Identifier == string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", request.Identifier)""", "request.Identifier")]
    [Arguments("u => u.Identifier == string.Format(request.Identifier, 1)", "request.Identifier")]
    [Arguments("""u => u.Identifier.StartsWith(string.Concat(request.Identifier, "%"))""", "request.Identifier")]
    [Arguments("""u => EF.Functions.ILike(u.Identifier, string.Format("%{0}%", request.Identifier))""", "request.Identifier")]
    public async Task a_request_text_in_string_concat_string_format_or_an_array_gets_fstqry001(string predicate, string text)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe(text);
    }

    [Test]
    [Arguments("""u => u.Identifier == string.Concat(tenant.Name, "-1")""")]
    [Arguments("""u => u.Identifier == string.Format("{0}", tenant.Name)""")]
    [Arguments("""u => u.Identifier == string.Format("{0}", u.Id)""")]
    [Arguments("""u => u.Identifier == string.Format("{0}", request.Dto)""")]
    [Arguments("""u => u.Identifier == string.Format("{0}", request.Identifier)""")]
    [Arguments("""u => u.Identifier == string.Concat(new[] { "a", request.Identifier })""")]
    public async Task string_concat_or_string_format_with_no_unchecked_request_text_has_no_diagnostics(string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            var tenant = Create(request.Dto.Name);
            if (!PostgresText.CanStore(request.Identifier))
                return false;
            return await dbContext.Users.AnyAsync({predicate}, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""id = "x";""")]
    [Arguments("""id += "x";""")]
    [Arguments("""(id, _) = ("x", 1);""")]
    [Arguments("""ref var alias = ref id; alias = "x";""")]
    [Arguments("""var other = "y"; ref var alias = ref other; alias = ref id; alias = "x";""")]
    public async Task a_check_of_a_lambda_parameter_that_the_code_assigns_again_gets_fstqry001(string assignment)
    {
        // Act
        var diagnostics = await RunInHandler($$"""
            if (!request.Ids.All(id => { {{assignment}} return EntityId.IsValid(id, Tenant.IdPrefix); }))
                return false;
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Ids");
    }

    [Test]
    [Arguments("id => { return EntityId.IsValid(id, Tenant.IdPrefix); }")]
    [Arguments("""id => { var other = "x"; other += "y"; return EntityId.IsValid(id, other); }""")]
    [Arguments("id => { var copy = id.Trim(); return EntityId.IsValid(copy, Tenant.IdPrefix); }")]
    [Arguments("id => { ref readonly var alias = ref id; return EntityId.IsValid(alias, Tenant.IdPrefix) && EntityId.IsValid(id, Tenant.IdPrefix); }")]
    [Arguments("""id => { var other = "y"; ref var alias = ref other; alias = "x"; return EntityId.IsValid(id, alias); }""")]
    public async Task a_check_of_a_lambda_parameter_that_the_code_does_not_assign_again_has_no_diagnostics(string check)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            if (!request.Ids.All({check}))
                return false;
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("for (var i = 1; i < request.Ids.Length; i++)")]
    [Arguments("for (var i = 0; i < request.Ids.Length - 1; i++)")]
    [Arguments("for (var i = 0; i <= request.Ids.Length - 2; i++)")]
    [Arguments("for (var i = 0; i < 1; i++)")]
    [Arguments("for (var i = 0; i < other.Length; i++)")]
    [Arguments("for (var i = 0; i < request.Ids.Length; i += 2)")]
    [Arguments("for (var i = 0; i < request.Ids.Length; i = i + 2)")]
    [Arguments("for (var i = 0; ; i++)")]
    [Arguments("for (var i = request.Ids.Length - 2; i >= 0; i--)")]
    [Arguments("for (var i = request.Ids.Length - 1; i > 0; i--)")]
    [Arguments("for (var i = other.Length - 1; i >= 0; i--)")]
    [Arguments("for (var i = request.Ids.Length - 1; i >= 0; i -= 2)")]
    [Arguments("for (var i = 0; i <= request.Ids.Length; i++)")]
    [Arguments("for (var i = 0; i > request.Ids.Length; i++)")]
    [Arguments("for (var i = 0; 0 < request.Ids.Length; i++)")]
    [Arguments("for (var i = request.Ids.Length - 1; 0 >= 0; i--)")]
    [Arguments("for (var i = request.Ids.Length - 1; i >= 1; i--)")]
    [Arguments("for (var i = request.Ids.Length + 1; i >= 0; i--)")]
    [Arguments("for (int i = 0, j = 0; i < request.Ids.Length; j++)")]
    [Arguments("for (int i = 0, j = 0; i < request.Ids.Length; j += 1)")]
    [Arguments("for (int i = request.Ids.Length - 1, j = 0; i >= 0; j--)")]
    [Arguments("for (int i = request.Ids.Length - 1, j = 0; i >= 0; j -= 1)")]
    public async Task a_for_loop_that_skips_an_item_gets_fstqry001(string loop)
    {
        // Act
        var diagnostics = await RunInHandler($$"""
            string[] other = ["a"];
            {{loop}} { if (!EntityId.IsValid(request.Ids[i], Tenant.IdPrefix)) return false; }
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Ids");
    }

    [Test]
    [Arguments("for (var i = 0; i < request.Names.Capacity; i++)")]
    [Arguments("for (var i = 0; i < request.Ids.Length; i++)")]
    public async Task a_for_loop_that_does_not_read_the_size_of_the_same_list_gets_fstqry001(string loop)
    {
        // Act
        var diagnostics = await RunInHandler($$"""
            {{loop}} { if (!EntityId.IsValid(request.Names[i], Tenant.IdPrefix)) return false; }
            return await dbContext.Users.AnyAsync(u => request.Names.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Names");
    }

    [Test]
    [Arguments("for (var i = 0; i < request.Ids.Length; i++) { i++; if (!EntityId.IsValid(request.Ids[i], Tenant.IdPrefix)) return false; }")]
    [Arguments("for (var i = 0; i < request.Ids.Length; ) { if (!EntityId.IsValid(request.Ids[i], Tenant.IdPrefix)) return false; i += 2; }")]
    [Arguments("for (var i = 0; i < request.Ids.Length; i++) { ref var alias = ref i; alias += 1; if (!EntityId.IsValid(request.Ids[i], Tenant.IdPrefix)) return false; }")]
    public async Task a_for_loop_that_moves_its_index_in_the_body_gets_fstqry001(string loop)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            {loop}
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Ids");
    }

    [Test]
    [Arguments("for (var i = 0; i < request.Ids.Length; ++i)")]
    [Arguments("for (var i = 0; i < request.Ids.Length; i += 1)")]
    [Arguments("for (int i = 0; i < request.Ids.Length; i++)")]
    [Arguments("for (var i = request.Ids.Length - 1; i >= 0; i--)")]
    [Arguments("for (var i = request.Ids.Length - 1; i >= 0; --i)")]
    [Arguments("for (var i = request.Ids.Length - 1; i >= 0; i -= 1)")]
    public async Task a_for_loop_that_reaches_each_item_has_no_diagnostics(string loop)
    {
        // Act
        var diagnostics = await RunInHandler($$"""
            {{loop}} { if (!EntityId.IsValid(request.Ids[i], Tenant.IdPrefix)) return false; }
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_for_loop_over_the_count_of_a_list_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            for (var i = 0; i < request.Names.Count; i++)
            {
                var name = request.Names[i].Trim();
                if (!PostgresText.CanStore(name))
                    return false;
            }

            return await dbContext.Users.AnyAsync(u => request.Names.Contains(u.Id), cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""
        u => string.Equals(request.Filter, null) || u.Identifier == "a"
        """)]
    [Arguments("""
        u => string.Equals(null, request.Filter) || u.Identifier == "a"
        """)]
    [Arguments("""
        u => string.Equals(request.Filter, null, StringComparison.Ordinal) || u.Identifier == "a"
        """)]
    [Arguments("""
        u => !string.Equals(request.Filter, null) && u.Identifier == "a"
        """)]
    [Arguments("""
        u => request.Filter!.Equals(null) || u.Identifier == "a"
        """)]
    [Arguments("""
        u => object.Equals(request.Filter, null) || u.Identifier == "a"
        """)]
    [Arguments("""
        u => object.Equals(null, request.Filter) || u.Identifier == "a"
        """)]
    public async Task a_null_check_of_a_request_text_by_equals_has_no_diagnostics(string predicate)
    {
        // Act
        var diagnostics = await RunInHandler($"return await dbContext.Users.AnyAsync({predicate}, cancellationToken);");

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_compare_of_a_request_text_next_to_a_null_check_by_equals_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            return await dbContext.Users.AnyAsync(u => string.Equals(request.Filter, null) || string.Equals(u.Identifier, request.Filter), cancellationToken);
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Location.SourceSpan.Start.ShouldBeGreaterThan(diagnostic.Location.SourceTree!.GetText().ToString().IndexOf("u.Identifier,", StringComparison.Ordinal));
        Text(diagnostic).ShouldBe("request.Filter");
    }

    [Test]
    public async Task a_check_of_each_item_of_another_list_gets_fstqry001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            string[] other = ["a"];
            if (!other.All(id => EntityId.IsValid(id, Tenant.IdPrefix)))
                return false;
            return await dbContext.Users.AnyAsync(u => request.Ids.Contains(u.Id), cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Ids");
    }

    [Test]
    public async Task a_request_text_in_an_explicit_handle_gets_fstqry001()
    {
        // Act
        var diagnostics = await Run("""
            public sealed record Command(string Identifier) : IRequest<bool>;

            public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, bool>
            {
                Task<bool> IRequestHandler<Command, bool>.Handle(Command request, CancellationToken cancellationToken)
                    => dbContext.Users.AnyAsync(u => u.Identifier == request.Identifier, cancellationToken);
            }
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("request.Identifier");
    }

    [Test]
    public async Task a_compare_outside_a_query_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            string[] names = ["a", "b"];
            return request.Identifier == "a" && names.Where(n => n == request.Identifier).Any();
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_method_that_is_not_a_request_handler_has_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            public sealed class Lookup(AppDbContext dbContext)
            {
                public Task<bool> Handle(string identifier, CancellationToken cancellationToken)
                    => dbContext.Users.AnyAsync(u => u.Identifier == identifier, cancellationToken);
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    private static string Text(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static Task<ImmutableArray<Diagnostic>> RunInHandler(string body)
        => Run($$"""
            public sealed record NameDto(string Name);

            public sealed record Command(string Identifier, string TenantId, NameDto Dto, string[] Ids, string? Filter, System.Collections.Generic.List<string> Names) : IRequest<bool>;

            public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command, bool>
            {
                public async Task<bool> Handle(Command request, CancellationToken cancellationToken)
                {
                    {{body}}
                }

                private static Tenant Create(string name) => throw new NotSupportedException(name);
            }
            """);

    private static async Task<ImmutableArray<Diagnostic>> Run(string source)
    {
        var references = PlatformReferences.All;
        var compilation = CSharpCompilation.Create(
            "Features",
            [CSharpSyntaxTree.ParseText(
                $$"""
                namespace App;

                using System;
                using System.Linq;
                using System.Threading;
                using System.Threading.Tasks;
                using FullstackTemplate.Server.Databases;
                using FullstackTemplate.Server.Domain;
                using FullstackTemplate.Server.Domain.Tenants;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                {{source}}
                """,
                path: "Source.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var exceptions = new List<Exception>();
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, _, _) => exceptions.Add(exception),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);
        var diagnostics = await compilation
            .WithAnalyzers([new QueryInputAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }
}
