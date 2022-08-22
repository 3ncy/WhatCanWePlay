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

namespace WhatCanWePlayClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Guid userGuid;

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

        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(userGuid.ToString());
        }

        private void UploadBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckInstalledGames();
            //and then upload said buttons
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

        private void CheckBtn_Click(object sender, RoutedEventArgs e)
        {
            List<string> myGames = CheckInstalledGames();

            //note - check the names truncated free of any spaces (and maybe special(nonalphanumeric) chars too), even the spaces in between of the words
            //and obviously ToLoweCase();   or upper
            myGames.ForEach(x => GamesListBox.Items.Add(x));

        }
    }
}
