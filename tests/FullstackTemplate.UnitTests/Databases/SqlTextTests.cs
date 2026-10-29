namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using Shouldly;

public class SqlTextTests
{
    private static readonly HashSet<string> Tables = ["users", "audit_entries"];

    [Test]
    [Arguments("UPDATE users AS u SET first_name = @p WHERE u.id = @id", "users")]
    [Arguments("update USERS set first_name = 'x'", "users")]
    [Arguments("DELETE FROM public.users WHERE id = $1", "users")]
    [Arguments("DELETE FROM \"users\" WHERE id = $1", "users")]
    [Arguments("INSERT INTO \"public\".\"audit_entries\" (kind) VALUES ('x')", "audit_entries")]
    [Arguments("WITH changed AS (UPDATE users SET first_name = 'x' RETURNING tenant_id) SELECT * FROM tenants", "users")]
    [Arguments("-- users.update\nUPDATE audit_entries SET kind = kind", "audit_entries")]
    public void names_the_first_table_of_the_set_that_a_word_names(string sql, string table)
    {
        // Act & Assert
        SqlText.FirstNamed(sql, Tables).ShouldBe(table);
    }

    [Test]
    [Arguments("DELETE FROM used_dpop_proofs WHERE expires <= now()")]
    [Arguments("UPDATE \"Users\" SET first_name = 'x'")]
    [Arguments("UPDATE users_archive SET first_name = 'x'")]
    [Arguments("-- users\nSELECT 1")]
    [Arguments("/* users /* nested users */ users */ SELECT 1")]
    [Arguments("SELECT 'users', 'it''s users'")]
    [Arguments("SELECT E'it\\'s users'")]
    [Arguments("SELECT $$users$$, $body$ users $body$")]
    public void finds_no_table_in_a_comment_a_value_or_another_name(string sql)
    {
        // Act & Assert
        SqlText.FirstNamed(sql, Tables).ShouldBeNull();
    }

    [Test]
    public void parameter_with_a_dollar_is_not_the_start_of_dollar_quoted_text()
    {
        // Act & Assert
        SqlText.FirstNamed("SELECT $1 FROM users WHERE id = $2", Tables).ShouldBe("users");
    }

    [Test]
    [Arguments("INSERT INTO t VALUES (1)", true)]
    [Arguments("WITH c AS (DELETE FROM t RETURNING id) SELECT id FROM c", true)]
    [Arguments("merge into t using s on t.id = s.id when matched then do nothing", true)]
    [Arguments("TRUNCATE t", true)]
    [Arguments("COPY t FROM STDIN", true)]
    [Arguments("SELECT id FROM t FOR UPDATE", true)]
    [Arguments("SELECT id FROM t", false)]
    [Arguments("SELECT 'UPDATE' FROM t -- DELETE", false)]
    [Arguments("SELECT \"update\" FROM t", false)]
    [Arguments("SELECT updated_on FROM t", false)]
    public void a_write_has_a_bare_write_keyword(string sql, bool isWrite)
    {
        // Act & Assert
        SqlText.IsWrite(sql).ShouldBe(isWrite);
    }

    [Test]
    [Arguments("DO $$ BEGIN UPDATE users SET first_name = 'x'; END $$", true)]
    [Arguments("do 'BEGIN UPDATE users SET first_name = ''x''; END'", true)]
    [Arguments("CALL rename_users()", true)]
    [Arguments("SELECT 1; call rename_users()", true)]
    [Arguments("-- note\n/* note */ DO 'BEGIN NULL; END'", true)]
    [Arguments("SELECT $body$ x $body$", true)]
    [Arguments("INSERT INTO t (id) VALUES (1) ON CONFLICT (id) DO NOTHING", false)]
    [Arguments("SELECT \"do\", 'CALL x', $1 FROM t -- DO", false)]
    [Arguments("SELECT rename_users()", false)]
    public void hidden_code_is_a_do_block_a_call_or_dollar_quoted_text(string sql, bool runsHiddenCode)
    {
        // Act & Assert
        SqlText.RunsHiddenCode(sql).ShouldBe(runsHiddenCode);
    }
}
