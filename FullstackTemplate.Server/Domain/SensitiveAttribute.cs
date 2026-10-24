namespace FullstackTemplate.Server.Domain;

/// <summary>
/// The value is personal or secret, for example a name or an email address. The audit log stores it in full and
/// shows it only to a reader with <c>read_audit_log_sensitive</c>. Put it on an entity property, on a complex property
/// (it then applies to each value of the complex type), or on a property of an audit action. Each text or structured
/// value of an entity or an action needs this mark or <see cref="NotSensitiveAttribute"/>. A unit test fails without one.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SensitiveAttribute : Attribute;

/// <summary>
/// The value is safe to show to each reader of the audit log, for example an id or a name from a fixed set.
/// See <see cref="SensitiveAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NotSensitiveAttribute : Attribute;
