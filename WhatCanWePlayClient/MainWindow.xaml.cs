using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace WhatCanWePlayClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly HttpClient httpClient = new HttpClient();
        Guid userGuid;
#if DEBUG
        string serverIp = "http://localhost:5181";
#else
        string ip = ""; //it will b fetched from my other server, cause my hosting hosting provider for this project might change the ip at some time
#endif
        DateTime lastCheckTime;
        DateTime lastUploadTime;

        public MainWindow()
        {
            InitializeComponent();


            string configFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WhatCanWePlay",
                    "config.ini");

            if (!File.Exists(configFilePath) || !Guid.TryParse(File.ReadAllText(configFilePath), out userGuid))
            {
                userGuid = Guid.NewGuid();
                new FileInfo(configFilePath).Directory!.Create();
                File.WriteAllText(configFilePath, userGuid.ToString());
            }

            //display user their guid
            GuidTBox.Text = userGuid.ToString();


            lastCheckTime = DateTime.Now;
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(userGuid.ToString());
        }

        private async Task FetchIp()
        {
            serverIp = await httpClient.GetStringAsync("https://martin.rmii.cz/files/aplikace/WhatCanWePlay/.ip.txt");
        }

        record Info(string Id, string Value);
        private async void UploadBtn_Click(object sender, RoutedEventArgs e)
        {
            //basic antispam so the api doesn't get spammed
            if ((DateTime.Now - lastUploadTime) < TimeSpan.FromMinutes(10))
            {
                ShowMessage(MessageType.UploadTimeout);
                return;
            }
            lastUploadTime = DateTime.Now;

            if (serverIp == "")
            {
                await FetchIp();
            }


            string myGames = string.Join('/', CheckInstalledGames());
            Info info = new Info(userGuid.ToString(), myGames);
            string json = System.Text.Json.JsonSerializer.Serialize(info);
            //string json = "{\"Id\": \"" + userGuid + "\" ,\"Value\": \"" + myGames + "\"}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(serverIp + "/users"),
                Content = new StringContent(json)
                {
                    Headers =
                    {
                        ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
                    }
                }
            };

            try
            {
                var response = await httpClient.SendAsync(request);
                if (response.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    ShowMessage(MessageType.SuccesfulUpload);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    ShowMessage(MessageType.UploadTimeout);
                    return;
                }
                else
                {
                    ShowMessage(MessageType.UnknownError, response.StatusCode.ToString());
                }
            }
            catch (HttpRequestException)
            {
                ShowMessage(MessageType.NoConnection);
            }
        }

        private List<string> CheckInstalledGames()
        {
            List<string> installedGames = new List<string>();

            //to find steam games:
            //(optional) check registry key "SteamPath" under "HKEY_CURRENT_USER\SOFTWARE\Valve\Steam" for steam path
            //or just assume it's always c:/program files (x86)/steam   (might b not tho)
            //add "/steamapps"
            //read file in there called "libraryfolders.vdf"    (info on parsing: https://stackoverflow.com/a/39557723/12741390)

            //  /^\s*"\d+"\n.*\n\s*"path"\s*"([^"]+)"/gm
            //the capturing group captures the path

            string output = "";

            string steamPath = @"C:\Program Files (x86)\steam"; //todo: get this path from registry noted above
            string libraryfoldersvdf = File.ReadAllText(Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"));
            Regex librariesRegex = new Regex(@"^\s*\u0022\d+\u0022\n.*\n\s*\u0022path\u0022\s*\u0022([^\u0022]+)\u0022", RegexOptions.Multiline);
            MatchCollection matches = librariesRegex.Matches(libraryfoldersvdf);

            foreach (Match match in matches)
            {

                string gameLibraryPath = match.Groups[1].Value;
                gameLibraryPath = Path.Combine(gameLibraryPath, "steamapps", "common");
                foreach (string dir in Directory.GetDirectories(gameLibraryPath))
                {
                    //GamesListBox.Items.Add(dir.Substring(dir.LastIndexOf('\\') + 1));
                    installedGames.Add(dir.Substring(dir.LastIndexOf('\\') + 1));
                }

            }

            //to find epic games:
            //locate the egs install folder
            //  "HKEY_CURRENT_USER\SOFTWARE\Epic Games\EOS" -> "ModSdkMetadataDir"  (hodnota neco jako "C:/ProgramData/Epic/EpicGamesLauncher/Data/Manifests")
            //check there for manifest files (as per https://www.partitionwizard.com/partitionmanager/change-epic-games-install-location.html#:~:text=launch%20the%20game.-,Way%202,-%3A%20Change%20Epic%20Game)
            //run through all the files (to be safe, only the "*.item" files in that folder
            //read either the "DisplayName" value or "MandatoryAppFolderName" (the file is JSON, but it should be possible to just parse it)
            //probably the folder name, since it already doesn't contain any nonaflphanumeric charactes and spaces -> less work for checking.

            //// this code works for parsing the name from the manifest file :D
            //string json = File.ReadAllText(@"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\C82F41134D16385273DC4895A5201B92.item");
            //List<string> a = json.Split('"').ToList();
            //folderName = a[a.IndexOf("MandatoryAppFolderName") + 2];

            string epicManifestsPath = @"C:/ProgramData/Epic/EpicGamesLauncher/Data/Manifests";
            foreach (string file in Directory.EnumerateFiles(epicManifestsPath, "*.item"))
            {
                string manifest = File.ReadAllText(file);
                List<string> manifestList = manifest.Split('"').ToList();

                installedGames.Add(manifestList[manifestList.IndexOf("MandatoryAppFolderName") + 2]);
                //GamesListBox.Items.Add(manifestList[manifestList.IndexOf("MandatoryAppFolderName") + 2]);
            }


            return installedGames;
        }

        private async void CheckBtn_Click(object sender, RoutedEventArgs e)
        {
            //basic antispam so the api doesn't get spammed
            if ((DateTime.Now - lastCheckTime) < TimeSpan.FromSeconds(5))
            {
                ShowMessage(MessageType.CheckTimeout);
                return;
            }
            lastCheckTime = DateTime.Now;

            if (serverIp == "")
            {
                await FetchIp();
            }


            //remove old games from the list
            GamesListBox.Items.Clear();


            List<string> possibleGames = CheckInstalledGames();
            Regex alphanumRgx = new Regex("[^a-zA-Z0-9]");//regex to remove all non alphanumeric chars
            possibleGames.ForEach(x => x = alphanumRgx.Replace(x, ""));

            string requestUriStr = serverIp + "/users";
            foreach (string friendGuid in FriendGuidsTBox.Text.Split(','))
            {
                if (!Guid.TryParse(friendGuid, out _))
                {
                    ShowMessage(MessageType.IncorrectGuid, friendGuid);
                    return;
                }
                requestUriStr += "/" + friendGuid;
            }

            try
            {
                var httpResp = await httpClient.GetAsync(requestUriStr);

                if (httpResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ShowMessage(MessageType.GuidNotFound);
                    return;
                }
                else if (httpResp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    ShowMessage(MessageType.CheckTimeout);
                    return;
                }
                else if (httpResp.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    //note: this shouldn'te really happen, cause the check is on client too, but who knows
                    ShowMessage(MessageType.IncorrectGuid, await httpResp.Content.ReadAsStringAsync() ?? "");
                    return;
                }

                
                string response = (await httpResp.Content.ReadAsStringAsync() ?? "").Replace("\"", "");

                foreach (string games in response.Split('\n')) // split by individual users
                {
                    List<string> friendsGames = games.Split('/').ToList();
                    friendsGames.ForEach(x => x = alphanumRgx.Replace(x, ""));
                    possibleGames = possibleGames.Intersect(friendsGames).ToList();
                }
            }
            catch (HttpRequestException)
            {
                ShowMessage(MessageType.NoConnection);
                return;
            }


            //show possible games
            possibleGames.ForEach(g => GamesListBox.Items.Add(g));

            if (possibleGames.Count == 0) {
                GamesListBox.Items.Add("NO GAMES FOUND");
            }


            //todo: check the names truncated free of any spaces (and maybe special(nonalphanumeric) chars too), even the spaces in between of the words
            //and obviously ToLoweCase();   or upper

        }


        private enum MessageType
        {
            UploadTimeout,
            SuccesfulUpload,
            NoConnection,
            CheckTimeout,
            GuidNotFound,
            UnknownError,
            IncorrectGuid
        }
        void ShowMessage(MessageType messageType, params string[] values)
        {
            switch (messageType)
            {
                case MessageType.UploadTimeout:
                    MessageBox.Show(
                        "You can only upload your games once every 10 minutes!" +
                        "\nPlease wait before trying again!",
                        "Slow down!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;
                case MessageType.SuccesfulUpload:
                    MessageBox.Show(
                        "Games successfully uploaded!." +
                        "\nShare your ID code to others now, so you can find what games you have installed in common with them.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    break;
                case MessageType.NoConnection:
                    MessageBox.Show(
                        "No Connection could be made to the server." +
                        "\nCheck your internet connection and if the issue persists, please contact me.",
                        "Connection error!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;
                case MessageType.CheckTimeout:
                    MessageBox.Show(
                        "You can only compare the games once every five seconds!" +
                        "\nPlease wait a moment before trying again!",
                        "Slow down!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    break;
                case MessageType.GuidNotFound:
                    MessageBox.Show(
                        "No games were found under that ID." +
                        "\nPlease make sure the other person has uploaded their games prior to checking",
                        "Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;
                case MessageType.UnknownError:
                    MessageBox.Show(
                        values[0] + "\nPlease report this error on github.",
                        "Unknown error",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Error);
                    break;
                case MessageType.IncorrectGuid:
                    MessageBox.Show(
                        "The ID " + values[0] + " is in an incorerect format!",
                        "Formatting error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    break;
            }
        }
    }
}
