using Microsoft.EntityFrameworkCore;
using WarehouseApp.Models;

namespace WarehouseApp.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _ctx;
    public UserRepository(AppDbContext ctx) => _ctx = ctx;

    public User? GetByLogin(string login) =>
        _ctx.Users.AsNoTracking().FirstOrDefault(u => u.Login == login);

    public void Add(User user) => _ctx.Users.Add(user);

    public bool LoginExists(string login) =>
        _ctx.Users.Any(u => u.Login == login);

    public void Save() => _ctx.SaveChanges();
}
