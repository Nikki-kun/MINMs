using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;

namespace MINMs.Server.Services;

public interface IContactService{
    Task<AddContactResult> AddContactAsync(int ownerId, string contactLogin, string contactName, CancellationToken cancellationToken = default);
}

public class СontactService : IContactService
{
    public Task<AddContactResult> AddContactAsync(int ownerId, string contactLogin, string contactName, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}

public enum AddContactResult
{
    Success,
    AlreadyExists,
    ContactNotFound,
    CannotAddSelf,
    DatabaseError,
    InvalidInput
}