namespace FullstackTemplate.SharedTestHelpers.Fakes.Agent;

using AutoBogus;
using FullstackTemplate.Server.Domain.Agents.Dtos;

public sealed class FakeAgentForCreationDto : AutoFaker<AgentForCreationDto>
{
    public FakeAgentForCreationDto()
    {
        RuleFor(x => x.TenantId, _ => null);
        RuleFor(x => x.Name, AgentRules.Name);
        RuleFor(x => x.ClientId, AgentRules.ClientId);
        RuleFor(x => x.Mode, AgentRules.Mode);
        RuleFor(x => x.Permissions, _ => []);
    }
}
