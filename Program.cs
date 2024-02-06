using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using ConfigCat.Client;
using LaunchDarkly.Sdk;
using LaunchDarkly.Sdk.Server;
using Flagsmith;
using DevCycle.SDK.Server.Cloud.Api;
using DevCycle.SDK.Server.Common.Model;
using OpenFeature;
using OpenFeature.Model;
using FeatBit.Sdk.Server;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Options;


internal class Program
{
    private static Lazy<Dictionary<string, string>> keysLazy = new Lazy<Dictionary<string, string>>(ReadKeys);

    private static async Task Main(string[] args)
    {
        const int refreshSeconds = 10;

        //create a local user object to convert to context in SDKs
        User user = new User 
        {
            Email = "jane@example.com",
            TenantId = 47223,
            Country = "Italy",
            Role = "PolicyAdmin"
        };

        // START DevCycle
        using DevCycleCloudClient dcclient = new DevCycleCloudClientBuilder()
                .SetSDKKey(ReadKey("DEV_CYCLE_KEY"))
                .SetLogger(new NullLoggerFactory())
                .Build();
        DevCycleUser dcuser = new DevCycleUser(user.Email);
        // END DevCyle

        // START FeatBit
        var options = new FbOptionsBuilder(ReadKey("FEAT_BIT_KEY"))
            .Event(new Uri("https://featbit-tio-eu-eval.azurewebsites.net"))
            .Streaming(new Uri("wss://featbit-tio-eu-eval.azurewebsites.net"))
            .Build();
        var fbclient = new FbClient(options);
        var fbuser = FbUser.Builder(user.Email).Build();
        // END FeatBit

        // START OpenFeature
        OpenFeature.Api.Instance.SetProvider(dcclient.GetOpenFeatureProvider());
        FeatureClient oFeatClient = OpenFeature.Api.Instance.GetClient();
        var ofctx = EvaluationContext.Builder()
            .Set("user_id", user.Email)
            .Set("Email", user.Email)
            .Set("Country", user.Country)
            .Set("Role", user.Role)
            .Build();
        // END OpenFeature

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

            // DevCycle
            sw = Stopwatch.StartNew();
            bool dc_feat = await dcclient.VariableValue(dcuser, ff_key, false);
            sw.Stop();
            Console.WriteLine($"retrieved {ff_key} value from DevCycle in {sw.ElapsedMilliseconds} ms: " + dc_feat);

            // FeatBit
            sw = Stopwatch.StartNew();
            var fb_feat = fbclient.BoolVariation(ff_key, fbuser, defaultValue: false);
            sw.Stop();
            Console.WriteLine($"retrieved {ff_key} value from FeatBit in {sw.ElapsedMilliseconds} ms: " + fb_feat);

            // OpenFeature - DevCyle
             sw = Stopwatch.StartNew();
            var of_dc_feat = await oFeatClient.GetBooleanValue(ff_key, false, ofctx);
            sw.Stop();
            Console.WriteLine($"retrieved {ff_key} value from OpenFeature-{"DevCycle"} in {sw.ElapsedMilliseconds} ms: " + of_dc_feat);

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
