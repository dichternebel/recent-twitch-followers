using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

using CliWrap;
using CliWrap.Buffered;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.Linq;
using System.Net.Http;

namespace RecentFollowers
{
    class Program
    {
        private static readonly string currentTwitchCliVersion = "1.1.20";

        private static readonly HttpClient client = new HttpClient();

        private static int currentFollower = -1;

        private static bool? followerListChanged;

        private static Font font = SystemFonts.CreateFont("Impact", 24);

        private static string clientID { get; set; }
        private static string clientSecret { get; set; }

        private static string displayHeartbeat { get; set; }
        private static string displayFollower { get; set; }
        private static string displayTotal { get; set; }
        private static string displayViewerCount { get; set; }

        private static string outputFolder { get; set; }
        private static string heartbeatPath { get; set; }
        private static string followerPath { get; set; }
        private static string totalPath { get; set; }
        private static string viewerPath { get; set; }

        private static TwitchUser twitchStreamer { get; set; }

        private static FollowerListObject RecentFollowers { get; set; }

#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unused private members
        // Warning disabled since fields are needed to prevent garbage collection!
        private static Timer heartbeatTimer;
        private static Timer viewerTimer;
        private static Timer followerTimer;
        private static Timer displayTimer;
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0052 // Remove unused private members

        static int Main(string[] args)
        {
            // Initialize serilog logger
            Log.Logger = new LoggerConfiguration()
                 .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
                 .WriteTo.File(Path.Combine(Directory.GetCurrentDirectory(), "logs", $"log-.txt"), Serilog.Events.LogEventLevel.Information, rollingInterval: RollingInterval.Day)
                 .MinimumLevel.Debug()
                 .Enrich.FromLogContext()
                 .CreateLogger();

            try
            {
                Native.SetQuickEditMode(false);
                MainAsync(args).Wait();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[Main]: {ex.Message}");
                return 1;
            }
        }

        static async Task MainAsync(string[] args)
        {
            // Name this thing
            Console.Title = "Recent Twitch Followers";

            // Physical path to executable
            var baseDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            // We need the twitch-cli libary for this
            var libPath = Path.Combine(baseDir, "lib");

            // Get the twitch-cli if not present
            if (!File.Exists(Path.Combine(libPath, "twitch.exe")))
            {
                getTwitchCliFromGitHub(libPath);
            }
            else
            {
                var cliVersion = await Cli.Wrap("lib/twitch.exe").WithArguments("version").WithWorkingDirectory(Directory.GetCurrentDirectory()).ExecuteBufferedAsync();
                if (cliVersion.StandardOutput != $"twitch-cli/{currentTwitchCliVersion}\n")
                {
                    getTwitchCliFromGitHub(libPath);
                }
            }

            // initialize outputFolder
            outputFolder = Path.Combine(baseDir, "output");

            // first argument: custom output path
            if (args.Length > 0)
            {
                outputFolder = Path.GetFullPath(args[0]);
            }
            if (!Directory.Exists(outputFolder))
            {
                var di = Directory.CreateDirectory(outputFolder);

                if (di.Exists)
                {
                    Log.Logger.Information("Output folder created successfully.");
                }
            }

            clientID = ConfigurationManager.AppSettings["ClientID"];
            clientSecret = ConfigurationManager.AppSettings["ClientSecret"];

            // Get permission and token
            var authProcess = Process.Start("lib/twitch.exe", new string[] { "configure", "-i", clientID, "-s", clientSecret });
            authProcess.WaitForExit();

            var tokenProcess = Process.Start("lib/twitch.exe", new string[] { "token" });
            tokenProcess.WaitForExit();

            var userFile = "user.json";

            if (File.Exists(userFile))
            {
                var jsonString = File.ReadAllText(userFile);
                twitchStreamer = JsonSerializer.Deserialize<TwitchUser>(jsonString);

                // New output folder passed as argument
                if (args.Length > 0)
                {
                    twitchStreamer.OutputFolder = outputFolder;
                }
                else if (!string.IsNullOrWhiteSpace(twitchStreamer.OutputFolder))
                {
                    outputFolder = twitchStreamer.OutputFolder;
                }
            }
            else
            {
                Console.Write("Enter your Twitch name: ");
                var userName = Console.ReadLine();

                // Get Twitch user
                twitchStreamer = await GetTwitchUserByName(userName);
                // Set the given output folder
                twitchStreamer.OutputFolder = outputFolder;
            }

            // Hide cursor
            Console.CursorVisible = false;

            // Always update user information on application start
            using FileStream createStream = File.Create(userFile);
            await JsonSerializer.SerializeAsync(createStream, twitchStreamer);
            await createStream.DisposeAsync();

            // Set file paths
            heartbeatPath = Path.Combine(outputFolder, "currentHeartBeat.txt");
            followerPath = Path.Combine(outputFolder, "currentFollower.txt");
            totalPath = Path.Combine(outputFolder, "totalFollowerCount.txt");
            viewerPath = Path.Combine(outputFolder, "currentViewerCount.txt");

            Log.Logger.Information($"Gathering information for {twitchStreamer.DisplayName}...");

#if DEBUG
            runHeartbeatTask(null, null);
#else
            heartbeatTimer = new Timer(5000);
            heartbeatTimer.Elapsed += runHeartbeatTask;
            heartbeatTimer.Start();
            viewerTimer = new Timer(5000);
            viewerTimer.Elapsed += runViewerTask;
            viewerTimer.Start();
            followerTimer = new Timer(8000);
            followerTimer.Elapsed += runFollowerTask;
            followerTimer.Start();
            displayTimer = new Timer(500);
            displayTimer.Elapsed += OutputToConsole;
#endif

            Console.Title = $"Recent Twitch Followers - output to {outputFolder}";

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static void getTwitchCliFromGitHub(string libPath)
        {
            if (!Directory.Exists(libPath))
            {
                Directory.CreateDirectory(libPath);
            }
            else
            {
                new DirectoryInfo(libPath).GetFileSystemInfos().ToList().ForEach(x =>
                {
                    if (x is DirectoryInfo di)
                        di.Delete(true);
                    else
                        x.Delete();
                });
            }

            
            var fileName = $"twitch-cli_{currentTwitchCliVersion}_Windows_x86_64";
            var zipPath = Path.Combine(libPath, $"{fileName}.zip");

            Log.Logger.Information($"Downloading Twitch-CLI to {zipPath}...");
            var byteArray = client.GetByteArrayAsync($@"https://github.com/twitchdev/twitch-cli/releases/download/v{currentTwitchCliVersion}/{fileName}.zip").Result;
            File.WriteAllBytes(zipPath, byteArray);
            ZipFile.ExtractToDirectory(zipPath, libPath, true);
            File.Delete(zipPath);

            var extractedPath = Path.Combine(libPath, fileName);
            if (Directory.Exists(extractedPath))
            {
                string[] files = Directory.GetFiles(extractedPath);
                // Move the files to the lib
                foreach (string s in files)
                {
                    var sourceFileName = Path.GetFileName(s);
                    var destFile = Path.Combine(libPath, sourceFileName);
                    File.Move(s, destFile);
                }
                // Remove empty folder
                Directory.Delete(extractedPath, true);
            }
        }

        private static void runHeartbeatTask(object sender, System.Timers.ElapsedEventArgs e)
        {
            var rnd = new Random();
            displayHeartbeat = rnd.Next(147, 222).ToString();
            WriteToTextFile(heartbeatPath, displayHeartbeat);
#if DEBUG
            runViewerTask(null, null);
#endif
        }

        private static async void runViewerTask(object sender, System.Timers.ElapsedEventArgs e)
        {
            displayViewerCount = await GetViewerCountFromTwitch();
            WriteToTextFile(viewerPath, displayViewerCount);
#if DEBUG
            runFollowerTask(null, null);
#endif
        }

        private static async Task<string> GetViewerCountFromTwitch()
        {
            int currentViewerCount = 0;

            try
            {
                var result = await Cli.Wrap("lib/twitch.exe").WithArguments($"api get /streams -q user_id={twitchStreamer.Id}").WithWorkingDirectory(Directory.GetCurrentDirectory()).ExecuteBufferedAsync();
                var streamObject = JsonSerializer.Deserialize<StreamObject>(result.StandardOutput);

                if (streamObject.Streams.Count > 0)
                {
                    currentViewerCount = streamObject.Streams[0].ViewerCount;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

            return currentViewerCount.ToString();
        }

        private static async void runFollowerTask(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Ugly guard
            if (followerListChanged.HasValue) return;
            followerListChanged = false;

            var currentFollowers = await GetFollowersFromTwitch();
            if (currentFollowers == null)
            {
                followerListChanged = null;
                return;
            }

            if (!IsObjectEqual(RecentFollowers,currentFollowers))
            {
                Log.Logger.Debug("Follower list is getting updated...");
                RecentFollowers = currentFollowers;
                followerListChanged = true;
            }

            currentFollower++;
            
            if (RecentFollowers.Data.Count == 0)
            {
                displayFollower = "waiting 4 u!";
                currentFollower = -1;
            }
            else if (RecentFollowers.Data.Count >= currentFollower + 1)
            {
                displayFollower = RecentFollowers.Data[currentFollower].FollowerName;
            }
            else
            {
                currentFollower = 0;
                displayFollower = RecentFollowers.Data[currentFollower].FollowerName;
            }

            WriteToTextFile(followerPath, displayFollower);

            displayTotal = RecentFollowers.Total.ToString();
            WriteToTextFile(totalPath, displayTotal);

            // List didn't change so stop processing
            if (!followerListChanged.Value)
            {
                followerListChanged = null;
                return;
            }

#if !DEBUG
            displayTimer.Stop();
#endif

            for (int i = 0; i < RecentFollowers.Data.Count; i++)
            {
                var follower = RecentFollowers.Data[i];
                var twitchUser = await GetTwitchUserByName(follower.FollowerName);
                if (twitchUser == null) continue;

                var fileId = i + 1;
                var followerFileName = $"follower{fileId}.txt";
                WriteToTextFile(Path.Combine(outputFolder, followerFileName), twitchUser.DisplayName);
                
                var byteArray = GetAvatarFromTwitch(twitchUser);
                if (byteArray == null) continue;

                var fileName = $"avatar{fileId}.png";

                EditAndSaveAvatar(byteArray, twitchUser.DisplayName, Path.Combine(outputFolder, fileName));
            }

            followerListChanged = null;
            Console.Clear();
#if DEBUG
            OutputToConsole(null, null);
#endif
#if !DEBUG
            displayTimer.Start();
#endif
        }

        private static async Task<FollowerListObject> GetFollowersFromTwitch()
        {
            FollowerListObject followerListObject = null;

            try
            {
                // Get the five most recent followers
                var result = await Cli.Wrap("lib/twitch.exe").WithArguments($"api get /users/follows -q to_id={twitchStreamer.Id} -q first=5").WithWorkingDirectory(Directory.GetCurrentDirectory()).ExecuteBufferedAsync();
                followerListObject = JsonSerializer.Deserialize<FollowerListObject>(result.StandardOutput);
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[GetAvatarFromTwitch]: {ex.Message}");
                followerListChanged = null;
            }

            return followerListObject;
        }

        private static async Task<TwitchUser> GetTwitchUserByName(string userName)
        {
            TwitchUser twitchUser = null;
            if (string.IsNullOrEmpty(userName)) return twitchUser;

            try
            {
                Log.Logger.Information($"Getting Twitch user information from {userName}...");
                var userResult = await Cli.Wrap("lib/twitch.exe").WithArguments($"api get /users -q login={userName}").WithWorkingDirectory(Directory.GetCurrentDirectory()).ExecuteBufferedAsync();
                twitchUser = JsonSerializer.Deserialize<TwitchUserObject>(userResult.StandardOutput).Data[0];
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[GetTwitchUserByName]: {ex.Message}");
                followerListChanged = null;
            }

            return twitchUser;
        }

        private static byte[] GetAvatarFromTwitch(TwitchUser twitchUser)
        {
            byte[] byteArray = null;
            if (twitchUser == null) return byteArray;

            try
            {
                Log.Logger.Information($"Loading Twitch avatar for user {twitchUser.DisplayName}...");
                byteArray = client.GetByteArrayAsync(twitchUser.ProfileImageUrl).Result;
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[GetAvatarFromTwitch]: {ex.Message}");
                followerListChanged = null;
            }

            return byteArray;
        }

        private static void EditAndSaveAvatar(byte[] imageByteArray, string nameToBeAdded, string pathtoImage)
        {
            if (imageByteArray == null) return;

            try
            {
                Log.Logger.Information($"Processing Twitch avatar for user {nameToBeAdded}...");

                using (var image = Image.Load(imageByteArray))
                {
                    image.Mutate(x => x.Resize(180, 180));
                    image.Mutate(x => x.DrawText(nameToBeAdded, font, Brushes.Solid(Color.White), Pens.Solid(Color.Black, 1), new PointF(3, 150)));
                    image.Save(pathtoImage); // Automatic encoder selected based on extension.
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[EditAndSaveAvatar]: {ex.Message}");
                followerListChanged = null;
            }
        }

        private static void WriteToTextFile(string filePath, string line)
        {
            try
            {
                using var sw = new StreamWriter(filePath);
                sw.WriteLine(line);
                sw.Close();
            }
            catch (Exception ex)
            {
                Log.Logger.Error($"[WriteToTextFile]: {ex.Message}");
            }
        }

        private static void OutputToConsole(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Random BPM      :    ");
            Console.WriteLine("Current follower:                                ");
            Console.WriteLine("Total followers :             ");
            Console.WriteLine("Current viewers :             ");
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Random BPM      : " + displayHeartbeat);
            Console.WriteLine("Current follower: " + displayFollower);
            Console.WriteLine("Total followers : " + displayTotal);
            Console.WriteLine("Current viewers : " + displayViewerCount);
        }

        public static bool IsObjectEqual(object x, object y)
        {
            return JsonSerializer.Serialize(x) == JsonSerializer.Serialize(y);
        }
    }
}
