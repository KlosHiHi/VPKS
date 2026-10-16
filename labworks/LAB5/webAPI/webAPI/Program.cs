using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<ApplicationContext>(options => options.UseSqlite("Data Source=carstore.db"));
builder.Services.AddTransient<CarService>();

builder.Services.AddStackExchangeRedisCache(
    options =>
    {
        options.Configuration = "localhost";
        options.InstanceName = "car_cash";
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Hello, Word!");

app.MapGet("/car/{id}", async (int id, CarService carService) =>
{
    Car? car = await carService.GetCar(id);

    return (car != null)
        ? JsonSerializer.Serialize(car)
        : "Cars not found!!!";
});

app.MapDelete("/car/{id}", async (int id, CarService carService) =>
{
    Car? car = await carService.DeleteUser(id);

    return (car != null) 
        ? JsonSerializer.Serialize(car) 
        : "Cars not found!!!";
});

app.MapPut("/car/{id}", async (int id, Car newCarInfo, CarService carService) =>
{
    var result = await carService.UpdateCar(id, newCarInfo);
    return result 
        ? Results.NotFound(new { message = "Cars not found" }) 
        : 
});

app.Run();

public class Car
{
    public int Id { get; set; }
    public string? Model { get; set; }
    public decimal Price { get; set; }
}

public class ApplicationContext : DbContext
{
    public DbSet<Car> Cars { get; set; }
    public ApplicationContext(DbContextOptions<ApplicationContext> options) 
        : base(options) => Database.EnsureCreated();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Car>().HasData(
             new Car { Id = 1, Model = "BMW X5", Price = 303.80M },
             new Car { Id = 2, Model = "Москвич 3", Price = 505.3M },
             new Car { Id = 3, Model = "Jett VA3 2026", Price = 1280.4M },
             new Car { Id = 4, Model = "Renault Twize", Price = 286.5M },
             new Car { Id = 5, Model = "Pegeout Sandero", Price = 1280.4M }
        );
    }
}

public class CarService(ApplicationContext db, IDistributedCache cache)
{
    private readonly ApplicationContext _db = db;
    private readonly IDistributedCache _cache = cache;


    public async Task<bool> UpdateCar(int id, Car newCarInfo)
    {
        var car = new Car();
        car = await _db.Cars.FindAsync(id);

        if(car is not null)
        {
            cache.Remove(id.ToString());

            car.Model = newCarInfo.Model;
            car.Price = newCarInfo.Price;
            await _db.SaveChangesAsync();
            return true;
        }
        else
        {
            return false;
        }
    }

    public async Task<Car?> GetCar(int id)
    {
        Car? car = null;

        var carString = await _cache.GetStringAsync(id.ToString());

        if (carString != null)
            car = JsonSerializer.Deserialize<Car>(carString);

        if (car is null)
        {
            car = await _db.Cars.FindAsync(id);

            if (car is not null)
            {
                Console.WriteLine($"{car.Model} извлечен из БД");
                carString = JsonSerializer.Serialize(car);

                await _cache.SetStringAsync(car.Id.ToString(), carString, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
                });
            }
        }
        else
        {
            Console.WriteLine($"{car.Model} извлечен из кэша");
        }

        return car;
    }

    public async Task<Car?> DeleteUser(int id)
    {
        Car? user = await _db.Cars.FindAsync(id);
        if (user != null) _db.Cars.Remove(user);
        cache.Remove(id.ToString());
        await _db.SaveChangesAsync();
        return user;
    }

}
