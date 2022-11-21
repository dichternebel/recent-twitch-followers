using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using CliWrap;
using CliWrap.Buffered;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.Linq;

namespace RecentFollowers
{
    class Program
    {
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
        //private static Timer displayTimer;
        private static System.Timers.Timer displayTimer;
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
                MainAsync(args).Wait();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                return 1;
            }
        }

        static async Task MainAsync(string[] args)
        {
            // Hide cursor
            Console.CursorVisible = false;

            // Name this thing
            Console.Title = "Recent Followers for OBS";

            // We need the twitch-cli libary for this
            var libPath = Path.Combine(Directory.GetCurrentDirectory(), "lib");

            // Get the twitch-cli if not present
            if (!File.Exists(Path.Combine(libPath, "twitch.exe")))
            {
                getTwitchCliFromGitHub(libPath);
            }
            else
            {
                var cliVersion = await Cli.Wrap("lib/twitch.exe").WithArguments("version").WithWorkingDirectory(Directory.GetCurrentDirectory()).ExecuteBufferedAsync();
                if (cliVersion.StandardOutput != "twitch-cli/1.1.12\n")
                {
                    getTwitchCliFromGitHub(libPath);
                }
            }

            // initialize outputFolder
            outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "output");

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
            runHeartbeatTask();
#else
            heartbeatTimer = new Timer(x => runHeartbeatTask(), null, 0, 5000);
            viewerTimer = new Timer(x => runViewerTask(), null, 0, 5000);
            followerTimer = new Timer(x => runFollowerTask(), null, 0, 8000);
            //displayTimer = new Timer(x => OutputToConsole(), null, 7500, 1000);
            displayTimer = new System.Timers.Timer(500);
            displayTimer.Elapsed += new System.Timers.ElapsedEventHandler(OutputToConsole);

#endif

            Console.Title = $"Recent Followers for OBS - output to {outputFolder}";

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private static void getTwitchCliFromGitHub(string libPath)
        {
            using (var client = new WebClient())
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
                var zipPath = Path.Combine(libPath, "twitch-cli_1.1.12_Windows_x86_64.zip");
                Log.Logger.Information($"Downloading Twitch-CLI to {zipPath}...");
                client.DownloadFile(@"https://github.com/twitchdev/twitch-cli/releases/download/v1.1.12/twitch-cli_1.1.12_Windows_x86_64.zip", zipPath);
                ZipFile.ExtractToDirectory(zipPath, libPath, true);
                File.Delete(zipPath);

                var extractedPath = Path.Combine(libPath, "twitch-cli_1.1.12_Windows_x86_64");
                if (Directory.Exists(extractedPath))
                {
                    string[] files = Directory.GetFiles(extractedPath);

                    // Move the files to the lib
                    foreach (string s in files)
                    {
                        var fileName = Path.GetFileName(s);
                        var destFile = Path.Combine(libPath, fileName);
                        File.Move(s, destFile);
                    }

                    // Remove empty folder
                    Directory.Delete(extractedPath, true);
                }
            }
        }

        private static void runHeartbeatTask()
        {
            var rnd = new Random();
            displayHeartbeat = rnd.Next(147, 222).ToString();
            WriteToTextFile(heartbeatPath, displayHeartbeat);
#if DEBUG
            runViewerTask();
#endif
        }

        private static async void runViewerTask()
        {
            displayViewerCount = await GetViewerCountFromTwitch();
            WriteToTextFile(viewerPath, displayViewerCount);
#if DEBUG
            runFollowerTask();
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

        private static async void runFollowerTask()
        {
            // Ugly guard
            if (followerListChanged.HasValue) return;

            followerListChanged = false;
            var currentFollowers = await GetFollowersFromTwitch();

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

            displayTimer.Stop();

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
            displayTimer.Start();

#if DEBUG
            //OutputToConsole();
            OutputToConsole(null, null);
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
                Log.Logger.Error(ex.Message);
            }

            return followerListObject;
        }

        private static async Task<TwitchUser> GetTwitchUserByName(string userName)
        {
            TwitchUser twitchUser = null;

            try
            {
                Log.Logger.Information($"Getting Twitch user information from {userName}...");
                var userResult = await Cli.Wrap("lib/twitch.exe").WithArguments($"api get /users -q login={userName}").WithWorkingDirectory(Directory.GetCurrentDirectory()).ExecuteBufferedAsync();
                twitchUser = JsonSerializer.Deserialize<TwitchUserObject>(userResult.StandardOutput).Data[0];
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

            return twitchUser;
        }

        private static byte[] GetAvatarFromTwitch(TwitchUser twitchUser)
        {
            byte[] byteArray = null;
            try
            {
                Log.Logger.Information($"Loading Twitch avatar for user {twitchUser.DisplayName}...");
                using (var client = new WebClient())
                    //This just saves the png but we want more: we add the user name to it
                    //client.DownloadFile(twitchUser.ProfileImageUrl, Path.Combine(outputFolder, fileName));
                    byteArray = client.DownloadData(twitchUser.ProfileImageUrl);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }

            return byteArray;
        }

        private static void EditAndSaveAvatar(byte[] imageByteArray, string nameToBeAdded, string pathtoImage)
        {
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
                Log.Logger.Error(ex.Message);
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
                Log.Logger.Error(ex.Message);
            }
        }

        //private static void OutputToConsole()
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
