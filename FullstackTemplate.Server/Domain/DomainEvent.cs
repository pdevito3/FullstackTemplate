namespace FullstackTemplate.Server.Domain;

using MediatR;

public abstract record DomainEvent : INotification;
