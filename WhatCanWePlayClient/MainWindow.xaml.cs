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
        string ip = "";  //todo: if hosting provider changes the ip, fetch the server's ip from my website where I'll change it manually if it changes on provider's end. That way there's no need to recompile the client
        DateTime lastActionTime;

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


            lastActionTime = DateTime.Now;
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(userGuid.ToString());
        }

        private async Task FetchIP()
        {
            ip = await httpClient.GetStringAsync("https://martin.rmii.cz/files/aplikace/WhatCanWePlay/.ip.txt");
        }

        record Info(string Id, string Value);
        private async void UploadBtn_Click(object sender, RoutedEventArgs e)
        {
            //basic antispam so the api doesn't get spammed
            if ((DateTime.Now - lastActionTime) < TimeSpan.FromMilliseconds(500))
            {
                //MessageTBlock.Text = "You can only compare the games every half a second!";
                MessageBox.Show("You can only upload your games every half a second!", "Slow down!", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            lastActionTime = DateTime.Now;

            if (ip == "")
            {
                await FetchIP();
            }


            string myGames = string.Join('/', CheckInstalledGames());
            Info info = new Info(userGuid.ToString(), myGames);
            string json = System.Text.Json.JsonSerializer.Serialize(info);
            //string json = "{\"Id\": \"" + userGuid + "\" ,\"Value\": \"" + myGames + "\"}";

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                //RequestUri = new Uri(ip+"/users"),
                RequestUri = new Uri(ip + "/users"),
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
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show(
                        "Games successfully uploaded!.\nShare your ID code to others now, so you can find what games you have installed in common with them.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    //todo: show more user-friendly error response, prolly switch by status code, when unreachable, tell them to notify me 
                    //nvm, it's prolly pointless, not sure what could go wrong, when the app is done. It can only fail on the server no being active, which is handled by the try catch
                    //switch (response.StatusCode)
                    //{
                    //    case System.Net.HttpStatusCode.BadRequest:
                    //        break;
                    //    case System.Net.HttpStatusCode.NotFound:
                    //        break;
                    //}
                    MessageBox.Show(response.StatusCode.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "No Connection could be made to the server.\nCheck your internet connection and if the issue persists, please contact me.",
                    "Connection error!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
            //MessageBox.Show(a[a.IndexOf("MandatoryAppFolderName") + 2]);

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
            if ((DateTime.Now - lastActionTime) < TimeSpan.FromMilliseconds(500))
            {
                //MessageTBlock.Text = "You can only compare the games every half a second!";
                MessageBox.Show("You can only compare the games every half a second!", "Slow down!", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            lastActionTime = DateTime.Now;

            if (ip == "")
            {
                await FetchIP();
            }


            //remove old games from the list
            GamesListBox.Items.Clear();


            List<string> possibleGames = CheckInstalledGames();
            Regex rgx = new Regex("[^a-zA-Z0-9]");//regex to remove all non alphanumeric chars
            possibleGames.ForEach(x => x = rgx.Replace(x, ""));


            foreach (string friendGuid in FriendGuidsTBox.Text.Split(','))
            {
                if (!Guid.TryParse(friendGuid, out _))
                {
                    MessageBox.Show("The ID " + friendGuid + " is in an incorerect format!", "", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string response = "";
                try
                {
                    response = await httpClient.GetStringAsync(ip + "/users/" + friendGuid.ToString());
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "No Connection could be made to the server.\nCheck your internet connection and if the issue persists, please contact me.",
                        "Connection error!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                response = response.Replace("\"", "");

                List<string> friendsGames = response.Split('/').ToList();

                friendsGames.ForEach(x => x = rgx.Replace(x, ""));
                //str = rgx.Replace(str, "");

                possibleGames = possibleGames.Intersect(friendsGames).ToList();
            }

            //show possible games
            possibleGames.ForEach(g => GamesListBox.Items.Add(g));



            //todo: check the names truncated free of any spaces (and maybe special(nonalphanumeric) chars too), even the spaces in between of the words
            //and obviously ToLoweCase();   or upper

        }
    }
}
