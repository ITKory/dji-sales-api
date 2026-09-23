using System.Globalization;
using sales_performance_api.Domain.Entities;
using sales_performance_api.Domain.Enums;

namespace sales_performance_api.Infrastructure.Persistence.Seed;

public static class DemoDataGenerator
{
	public const string Version = "sales-demo-v1";
	private const int RandomSeed = 20260923;
	private const int SalesCount = 3600;
	public static readonly DateOnly StartDate = new(2025, 9, 24);
	public static readonly DateOnly EndDate = new(2026, 9, 23);
	public static readonly DateOnly QuietFrom = new(2026, 2, 10);
	public static readonly DateOnly QuietTo = new(2026, 2, 16);

	public static DemoData Generate()
	{
		// No wall clock, current culture, GUID randomness or external inputs affect this dataset.
		var random = new Random(RandomSeed);
		string[] names =
		[
			"Sarah Chen", "Marcus Reid", "Priya Patel", "James Okafor", "Elena Vasquez",
			"David Kim", "Alex Turner", "Nina Costa", "Oliver Smith", "Mia Novak",
			"Liam Brown", "Emma Wilson", "Noah Martin", "Ava Garcia", "Leo Fischer",
			"Sofia Rossi", "Lucas Silva", "Isabella Lee", "Ethan Clark", "Amelia Davis"
		];
		var managers = names.Select((name, i) => new Manager
		{
			Id = Id(1, i + 1), Name = name,
			Initials = string.Concat(name.Split(' ').Select(part => part[0]))
		}).ToArray();
		string[] firstNames = ["John", "Lisa", "Robert", "Emily", "Michael", "Jessica", "Tom", "Anna"];
		string[] lastNames =
			["Anderson", "Wang", "Chen", "Davis", "Scott", "Park", "Holland", "Smith", "Mendez", "Turner"];
		var customers = Enumerable.Range(0, 80).Select(i => new Customer
		{
			Id = Id(2, i + 1), Name = $"{firstNames[i % 8]} {lastNames[i / 8]}",
			Company = FormattableString.Invariant($"Demo Company {i / 2 + 1:00}")
		}).ToArray();
		string[] categoryNames = ["Drones", "Cameras", "Stabilizers", "Batteries", "Accessories", "Services"];
		var categories = categoryNames.Select((name, i) => new Category
		{
			Id = Id(3, i + 1), Name = name, SortOrder = i
		}).ToArray();
		var products = Enumerable.Range(0, 48).Select(i => new Product
		{
			Id = Id(4, i + 1), Name = FormattableString.Invariant($"{categoryNames[i / 8]} Model {i % 8 + 1:00}"),
			CategoryId = categories[i / 8].Id
		}).ToArray();

		// Weighted calendar creates seasonal peaks, quiet summer, and quieter weekends.
		int[] monthWeights = [6, 6, 10, 11, 12, 8, 5, 6, 12, 15, 22, 24];
		var calendar = new List<DateOnly>();
		for (var date = StartDate; date <= EndDate; date = date.AddDays(1))
		{
			if (date >= QuietFrom && date <= QuietTo) continue;
			var weekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
			var weight = monthWeights[date.Month - 1] * (weekend ? 1 : 3);
			weight += date.Day % 5; // Repeatable variation between adjacent days.
			for (var j = 0; j < weight; j++) calendar.Add(date);
		}

		var sales = new List<Sale>(SalesCount);
		var itemNumber = 0;
		decimal[] basePrices = [650m, 380m, 180m, 65m, 20m, 140m];
		for (var i = 0; i < SalesCount; i++)
		{
			var date = i == 0 ? StartDate : i == SalesCount - 1 ? EndDate : calendar[random.Next(calendar.Count)];
			int managerIndex;
			do
			{
				var draw = random.Next(75);
				managerIndex = draw < 40 ? draw / 8 : draw < 70 ? 5 + (draw - 40) / 3 : 15 + draw - 70;
				// Weak managers have a full summer without transactions.
			} while (managerIndex >= 15 && date.Month is 7 or 8);

			var tier = managerIndex < 5 ? 0 : managerIndex < 15 ? 1 : 2;
			var statusDraw = random.Next(100);
			var cancellationRate = 7 + tier * 5;
			var status = statusDraw < cancellationRate ? SaleStatus.Cancelled
				: statusDraw < cancellationRate + 5 + tier ? SaleStatus.Refunded : SaleStatus.Paid;
			var sale = new Sale
			{
				Id = Id(5, i + 1), ManagerId = managers[managerIndex].Id,
				CustomerId = customers[random.Next(customers.Length)].Id, Status = status,
				SaleDate = date.ToDateTime(new TimeOnly(random.Next(8, 18), random.Next(60), random.Next(60)),
					DateTimeKind.Utc)
			};
			var lines = random.Next(1, tier == 0 ? 5 : tier == 1 ? 4 : 3);
			var productIndexes = new HashSet<int>();
			for (var line = 1; line <= lines; line++)
			{
				int productIndex;
				do
				{
					productIndex = random.Next(products.Length);
				} while (!productIndexes.Add(productIndex));

				var product = products[productIndex];
				var categoryIndex = productIndex / 8;
				var price = Money(basePrices[categoryIndex] * (1m + (productIndex % 8) * 0.18m)
				                                            * (0.85m + random.Next(31) / 100m));
				var margin = (tier == 0 ? 0.38m : tier == 1 ? 0.27m : 0.13m)
				             + random.Next(13) / 100m + categoryIndex * 0.01m;
				// A few valid paid deals exercise zero revenue and negative profit without using refunds.
				var free = i % 311 == 0 && line == 1 && status == SaleStatus.Paid;
				var loss = i % 173 == 0 && line == 1 && status == SaleStatus.Paid;
				sale.Items.Add(new SaleItem
				{
					Id = Id(6, ++itemNumber), SaleId = sale.Id, LineNumber = line,
					ProductId = product.Id, CategoryIdAtSale = product.CategoryId, ProductNameAtSale = product.Name,
					Quantity = random.Next(1, tier == 0 ? 6 : tier == 1 ? 4 : 3),
					UnitSalePrice = free ? 0 : price,
					UnitCost = Money(price * (loss ? 1.12m : 1m - margin))
				});
			}

			sales.Add(sale);
		}

		return new DemoData(managers, customers, categories, products, sales);
	}

	private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

	private static Guid Id(int kind, int number) => Guid.Parse(
		string.Create(CultureInfo.InvariantCulture, $"00000000-0000-0000-{kind:0000}-{number:000000000000}"));
}
