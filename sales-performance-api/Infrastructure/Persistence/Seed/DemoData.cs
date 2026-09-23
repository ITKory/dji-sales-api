using sales_performance_api.Domain.Entities;

namespace sales_performance_api.Infrastructure.Persistence.Seed;

public sealed record DemoData(
    IReadOnlyList<Manager> Managers,
    IReadOnlyList<Customer> Customers,
    IReadOnlyList<Category> Categories,
    IReadOnlyList<Product> Products,
    IReadOnlyList<Sale> Sales);
