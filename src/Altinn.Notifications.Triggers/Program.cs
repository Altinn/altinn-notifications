IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

string notificationsEndpoint = configuration["PlatformSettings:ApiNotificationsEndpoint"]!;

Console.WriteLine($"Hello, World! \r\n {notificationsEndpoint}");
