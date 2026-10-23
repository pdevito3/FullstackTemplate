namespace FullstackTemplate.Server.Domain.Agents.DomainEvents;

public sealed record AgentCreated(Agent Agent) : DomainEvent;
