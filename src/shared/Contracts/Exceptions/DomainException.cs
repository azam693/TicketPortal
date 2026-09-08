namespace Contracts.Exceptions;

public class DomainException(string message) : Exception(message);