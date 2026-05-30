using NLog;
using WarehouseApp.Data.Repositories;
using WarehouseApp.Models;

namespace WarehouseApp.Services;

public interface IAuthService
{
    OperationResult<User> Login(string login, string password);
    OperationResult Register(string login, string password, string confirmPassword);
    User? CurrentUser { get; }
    void Logout();
}

public class AuthService : IAuthService
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IUserRepository _repo;
    public AuthService(IUserRepository repo) => _repo = repo;

    public User? CurrentUser { get; private set; }

    public OperationResult<User> Login(string login, string password)
    {
        logger.Trace("Попытка входа для логина '{Login}'", login);

        if (string.IsNullOrWhiteSpace(login))
            return OperationResult<User>.Fail("Введите логин.");
        if (string.IsNullOrWhiteSpace(password))
            return OperationResult<User>.Fail("Введите пароль.");

        try
        {
            var user = _repo.GetByLogin(login.Trim());
            if (user == null)
            {
                logger.Warn("Вход не выполнен: пользователь '{Login}' не найден", login);
                return OperationResult<User>.Fail("Пользователь не найден.");
            }
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                logger.Warn("Вход не выполнен: неверный пароль для пользователя '{Login}'", login);
                return OperationResult<User>.Fail("Неверный пароль.");
            }

            CurrentUser = user;
            logger.Info("Пользователь '{Login}' (роль {Role}) вошёл в систему", user.Login, user.Role);
            return OperationResult<User>.Ok(user);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка при входе пользователя '{Login}'", login);
            throw;
        }
    }

    public OperationResult Register(string login, string password, string confirmPassword)
    {
        logger.Trace("Попытка регистрации нового пользователя '{Login}'", login);

        if (string.IsNullOrWhiteSpace(login))
            return OperationResult.Fail("Введите логин.");
        if (login.Trim().Length < 3)
            return OperationResult.Fail("Логин должен содержать минимум 3 символа.");
        if (string.IsNullOrWhiteSpace(password))
            return OperationResult.Fail("Введите пароль.");
        if (password.Length < 4)
            return OperationResult.Fail("Пароль должен содержать минимум 4 символа.");
        if (password != confirmPassword)
            return OperationResult.Fail("Пароли не совпадают.");
        if (_repo.LoginExists(login.Trim()))
        {
            logger.Warn("Регистрация отклонена: логин '{Login}' уже занят", login);
            return OperationResult.Fail("Пользователь с таким логином уже существует.");
        }

        try
        {
            _repo.Add(new User
            {
                Login = login.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Storekeeper
            });
            _repo.Save();
            logger.Info("Зарегистрирован новый пользователь '{Login}' (роль {Role})",
                login.Trim(), UserRole.Storekeeper);
            return OperationResult.Ok("Регистрация прошла успешно. Войдите в систему.");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Ошибка сохранения нового пользователя '{Login}'", login);
            throw;
        }
    }

    public void Logout()
    {
        if (CurrentUser != null)
            logger.Info("Пользователь '{Login}' вышел из системы", CurrentUser.Login);
        CurrentUser = null;
    }
}
