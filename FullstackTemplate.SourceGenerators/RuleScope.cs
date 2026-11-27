namespace FullstackTemplate.SourceGenerators;

/// <summary>
/// The custom tags that say which projects a rule checks. A rule with no tag checks only the Server, so
/// ServerRulesOff.globalconfig turns it off in each other project. A rule with <see cref="AppCode"/> checks the Server,
/// the BFF, and the AppHost, so TestRulesOff.globalconfig turns it off in each project under tests/. A rule with
/// <see cref="AllCode"/> checks each project. ServerRulesOffTests reads these tags.
/// </summary>
internal static class RuleScope
{
	public const string AllCode = "AllCode";
	public const string AppCode = "AppCode";
}
