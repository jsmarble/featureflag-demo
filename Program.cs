using ConfigCat.Client;

var client = ConfigCatClient.Get("ICfcCKekZEePkMae0VIb_A/8nGfF0AlHUmmUGkXdF8YLA", // <-- This is the actual SDK Key for your 'Test Environment' environment
    options =>
    {
        options.PollingMode = PollingModes.AutoPoll(pollInterval: TimeSpan.FromSeconds(10));
    });

client.LogLevel = LogLevel.Info; // <-- Set the log level to INFO to track how your feature flags were evaluated. When moving to production, you can remove this line to avoid too detailed logging.

User user = new User("##SOME-USER-IDENTIFIER##") // Unique identifier is required. Could be UserID, Email address or SessionID.
{
    Country = "Finland",
    Email = "jane@example.com",
    Custom =
    {
        { "Instance", "50001"},
        { "Role", "PolicyAdmin"}
    }
};

var isMyFirstFeatureEnabled = client.GetValue("isMyFirstFeatureEnabled", false, user);

while (true)
{
    Console.Clear();
    Console.WriteLine("isMyFirstFeatureEnabled's value from ConfigCat: " + isMyFirstFeatureEnabled);
    await Task.Delay(500);
}
