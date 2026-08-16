namespace MyApi.Modules.Shared.Services;

public interface IBaseService<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(int id);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
    Task<bool> SoftDeleteAsync(int id);
}

// Example service interface
/*
public interface IUserService : IBaseService<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<bool> ValidateCredentialsAsync(string email, string password);
}
*/
