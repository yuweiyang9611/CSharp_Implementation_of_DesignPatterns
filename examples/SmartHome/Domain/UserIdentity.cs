namespace DesignPatterns.TeachingProjects.SmartHome.Domain;

public enum HomeRole
{
    Owner,
    Resident,
    Guest
}

public sealed record UserIdentity(string Name, HomeRole Role);
