using System.Linq;
using System.Diagnostics;
using ConfigCat.Client;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using Flagsmith;
using DevCycle.SDK.Server.Cloud.Api;
using DevCycle.SDK.Server.Common.Model;

internal class Program
{
    private static Lazy<Dictionary<string, string>> keysLazy = new Lazy<Dictionary<string, string>>(ReadKeys);

    private static async Task Main(string[] args)
    {
        const int refreshSeconds = 10;

        User user = new User
        {
            Email = "harry@example.com",
            TenantId = 47223,
            Country = "Italy",
            Role = "PolicyAdmin"
        };

        // START OpenFeature

        // END OpenFeature

        // START DevCycle
        string devcycKey = ReadKey("DEV_CYCLE_KEY");

        // END DevCyle

        // START FlagSmith
        FlagsmithClient fsClient = new(ReadKey("FLAGSMITH_KEY"),
            environmentRefreshIntervalSeconds: refreshSeconds);

        var fsFlags = await fsClient.GetIdentityFlags(user.Email,
                        new List<ITrait>{
                            new Trait("Email", user.Email),
                            new Trait("TenantId", user.TenantId),
                            new Trait("Country", user.Country),
                            new Trait("Role", user.Role),
                        });
        // END FlagSmith

        // START ConfigCat
        var ccclient = ConfigCatClient.Get(ReadKey("CONFIG_CAT_KEY"), // <-- This is the actual SDK Key for your 'Test Environment' environment
            options =>
            {
                options.PollingMode = PollingModes.AutoPoll(pollInterval: TimeSpan.FromSeconds(refreshSeconds));
                options.Logger = new ConsoleLogger(LogLevel.Info);
            });

        var ccuser = new ConfigCat.Client.User(user.Email)
        {
            Country = user.Country,
            Email = user.Email,
            Custom =
            {
                { "TenantId", user.TenantId},
                { "Role", user.Role}
            }
        };
        // END ConfigCat

        // START LaunchDarkly
        var ldConfig = Configuration.Default(ReadKey("LAUNCH_DARKLY_KEY"));
        var ldclient = new LdClient(ldConfig);

        var ldContext = Context.Builder(user.Email)
            .Name(user.Email)
            .Set("Country", user.Country)
            .Set("TenantId", user.TenantId)
            .Set("Role", user.Role)
            .Build();
        // END LaunchDarkly

        while (true)
        {
            Console.Clear();
            const string ff_key = "demo-feature";
            Stopwatch sw;

            // ConfigCat
            sw = Stopwatch.StartNew();
            var configcat_feat = ccclient.GetValue(ff_key, false, ccuser);
            sw.Stop();
            Console.WriteLine($"retrieved {ff_key} value from ConfigCat in {sw.ElapsedMilliseconds} ms: " + configcat_feat);

            // FlagSmith
            sw = Stopwatch.StartNew();
            var fs_feat = await fsFlags.IsFeatureEnabled(ff_key);
            sw.Stop();
            Console.WriteLine($"retrieved {ff_key} value from FlagSmith in {sw.ElapsedMilliseconds} ms: " + fs_feat);

            // LaunchDarkly
            sw = Stopwatch.StartNew();
            var ld_feat = ldclient.BoolVariation(ff_key, ldContext, false);
            sw.Stop();
            Console.WriteLine($"retrieved {ff_key} value from LaunchDarkly in {sw.ElapsedMilliseconds} ms: " + ld_feat);

            await Task.Delay(2000);
        }
    }

    private static string ReadKey(string name)
    {
        if (keysLazy.Value.TryGetValue(name, out string key))
            return key;
        else
            throw new ArgumentException($"Key not found for '{name}'.");
    }

    private static Dictionary<string, string> ReadKeys()
    {
        var lines = File.ReadAllLines("secrets.env").ToList();
        var pairs = lines.Select(ParseKeyPair);
        var keys = new Dictionary<string, string>(pairs);
        return keys;
    }

    private static KeyValuePair<string, string> ParseKeyPair(string line)
    {
        int i = line.IndexOf('=');
        string k = line.Substring(0, i);
        string key = line.Substring(i + 1);
        return new KeyValuePair<string, string>(k, key);
    }
}

internal class User
{
    public string Country { get; set; }
    public string Email { get; set; }
    public int TenantId { get; set; }
    public string Role { get; set; }
}
