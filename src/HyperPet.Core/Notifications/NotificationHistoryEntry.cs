using HyperPet.Core.Pet;

namespace HyperPet.Core.Notifications;

public sealed record NotificationHistoryEntry(Guid Id, PetAlert Alert);
