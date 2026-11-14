namespace FullstackTemplate.Server.Resources.Jobs;

/// <summary>
/// A background job of the whole system, which must see the rows of all tenants. Its handler derives from
/// <see cref="SystemJobHandler{TJob}"/>. A job of one tenant is an <see cref="ITenantJob"/>.
/// </summary>
public interface ISystemJob;
