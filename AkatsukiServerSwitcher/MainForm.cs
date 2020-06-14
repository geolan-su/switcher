using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using switcher.Properties;

namespace AkatsukiServerSwitcher
{
    public partial class MainForm : Form
    {
        public bool akatsuki = false;
        public string akatsukiIP = "144.217.254.156";

        public string settingsPath = $@"{System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\Akatsuki Server Switcher";
        public string hostsPath = Environment.GetEnvironmentVariable("windir") + @"\system32\drivers\etc\hosts";

        public MainForm()
        {
            InitializeComponent();

            // Make sure hosts file exists
            if (!File.Exists(hostsPath))
                File.AppendAllText(hostsPath, "# Hosts file");

            // Create tooltips
            ToolTip OnOffTooltip = new ToolTip();
            OnOffTooltip.SetToolTip(this.switchButton, "Switch between osu! and akatsuki");
            ToolTip LocalRipwotTooltip = new ToolTip();
            LocalRipwotTooltip.SetToolTip(this.updateIPButton, "Get the right server IP address directly from the server.");
            ToolTip InstallCertificateTooltip = new ToolTip();
            InstallCertificateTooltip.SetToolTip(this.installCertificateButton, "Install/Remove HTTPS certificate.\nYou must have install the certificate in order to connect\nto Akatsuki.");

            // Create settings directory (if it doesn't exists)
            Directory.CreateDirectory(settingsPath);

            // Check if akatsuki.txt exists and if not create a default one
            if (!File.Exists($@"{settingsPath}\akatsuki.txt"))
            {
                File.AppendAllText($@"{settingsPath}\akatsuki.txt", akatsukiIP + Environment.NewLine);
            }

            // Read akatsuki.txt
            string[] akatsukiTxt = File.ReadAllLines($@"{settingsPath}\akatsuki.txt");

            // There should only be a single line, the IP.
            if (akatsukiTxt.Length == 1)
            {
                // Read IP
                akatsukiIP = akatsukiTxt[0];
            }
            else
            {
                // Something went wrong, use default settings
            }

            // Update settings
            updateSettings();

            // Get current hosts configuration
            findServer();

            // Get certificate status and update button text
            updateCertificateButton();

            // Check for updates
            //Thread ut = new Thread(updateThread);
            //ut.Start();

            // Check if we are using old server IP
            checkOldServerIP();
        }


        private void MainForm_Shown(object sender, EventArgs e)
        {
            updateStatusLabel();
        }

        public void saveSettings()
        {
            // Save settings to akatsuki.txt
            File.WriteAllText($@"{settingsPath}\akatsuki.txt", akatsukiIP + Environment.NewLine);
        }

        public bool findServer()
        {
            // Read hosts
            string[] hostsContent = File.ReadAllLines(hostsPath);

            // Loop through all strings
            for (var i = 0; i < hostsContent.Length; i++)
            {
                // Check if current line is not empty (otherwise it throws an exception)
                if (hostsContent[i] != "")
                {
                    // Check if current line is not commented and redirects to osu.ppy.sh
                    if ((Regex.Matches(hostsContent[i], "#").Count == 0) && (Regex.Matches(hostsContent[i], "osu.ppy.sh").Count > 0))
                    {
                        // Our hosts points to akatsuki
                        akatsuki = true;
                        return akatsuki;
                    }
                }
            }

            // Hosts doesn't contain any reference to osu.ppy.sh, we are not pointing to Akatsuki
            akatsuki = false;
            return akatsuki;
        }

        public bool updateServer()
        {
            IPAddress type;
            if (akatsukiIP == "" || (IPAddress.TryParse(akatsukiIP, out type) && type.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork))
            {
                statusLabel.Text = "Invalid Akatsuki/Mirror IP address";
                return false;
            }

            // Check read only
            if(IsFileReadOnly(hostsPath))
                SetFileReadAccess(hostsPath, false);

            // Read hosts
            string[] hostsContent = File.ReadAllLines(hostsPath);

            // Check for any osu.ppy.sh line, remove them
            for (var i = 0; i < hostsContent.Length; i++)
            {
                if (Regex.Matches(hostsContent[i], @"(?:(?:osu|c[e4-6]?|a|i)\.ppy\.sh|# Akatsuki Server Switcher)").Count > 0)
                {
                    // Line that points (or used to point) to osu.ppy.sh, remove it
                    hostsContent[i] = "";
                }
            }

            // Empty hosts
            try
            {
                File.WriteAllText(hostsPath, "");

                // Rewrite hosts
                for (var i = 0; i < hostsContent.Length; i++)
                {
                    if (hostsContent[i] != "")
                    {
                        // Current line is not empty, write it
                        File.AppendAllText(hostsPath, hostsContent[i] + Environment.NewLine);
                    }
                }

                // Point to akatsuki if required
                if (akatsuki)
                {
                    File.AppendAllText(hostsPath, $"# Akatsuki Server Switcher{Environment.NewLine}");
                    File.AppendAllText(hostsPath, $"{akatsukiIP}\tosu.ppy.sh{Environment.NewLine}");
                    File.AppendAllText(hostsPath, $"{akatsukiIP}\ta.ppy.sh{Environment.NewLine}");
                    File.AppendAllText(hostsPath, $"{akatsukiIP}\ti.ppy.sh{Environment.NewLine}");
                    File.AppendAllText(hostsPath, $"{akatsukiIP}\tc.ppy.sh{Environment.NewLine}");
                    File.AppendAllText(hostsPath, $"{akatsukiIP}\tce.ppy.sh{Environment.NewLine}");
                    for (int i = 4; i <= 6; i++) // only c4-6 are actually used.
                        File.AppendAllText(hostsPath, $"{akatsukiIP}\tc{i}.ppy.sh{Environment.NewLine}");
                }

                return true;
            }
            catch
            {
                MessageBox.Show($"Error while writing hosts file.{Environment.NewLine}This is usually caused by an antivirus program blocking access.");
                return false;
            }
        }

        public void updateStatusLabel()
        {
            // Update statusLabel based on akatsuki variable
            statusLabel.Text = akatsuki ? $"You are playing on Akatsuki's server.{Environment.NewLine}{IPTextBox.Text}" : "You are playing on osu!'s server.";
            // Ayy k maron sn pigor xd
            updateJennaWarning();
        }

        public void updateJennaWarning()
        {
            if (Application.OpenForms.Count >= 1)
                if (akatsukiIP == "127.0.0.1")
                    Application.OpenForms[0].Height = 330;
                else
                    Application.OpenForms[0].Height = 202;
        }

        public void updateSettings()
        {
            // Update textBoxes in settings group
            IPTextBox.Text = akatsukiIP;
        }

        private void IPTextBox_TextChanged(object sender, EventArgs e)
        {
            // Settings: Update IP address
            akatsukiIP = IPTextBox.Text;
        }

        private void switchButton_Click(object sender, EventArgs e)
        {
            // Get current hosts status, because it might have changed
            findServer();

            // Switch between akatsuki/osu!, write hosts and update label
            akatsuki = !akatsuki;
            if (updateServer())
                updateStatusLabel();
        }

        public void checkAkatsukiConnection()
        {
            // Checks if osu.ppy.sh actually points to akatsuki
            try
            {

                string s;
                using (WebClient client = new WebClient())
                {
                    byte[] response =
                    client.UploadValues("http://osu.ppy.sh/", new NameValueCollection()
                    {
                        { "switcher", "true" },
                    });
                    s = Encoding.UTF8.GetString(response);
                }

                if (s == "ok")
                    updateStatusLabel();    // This changes statuslabel.text to "You are playing on Akatsuki"
                else
                    statusLabel.Text = "Error while connecting to Akatsuki.";
            }
            catch
            {
                // 4xx / 5xx error
                statusLabel.Text = "Error while connecting to Akatsuki.";
            }
        }

        private void genuineButton1_Click(object sender, EventArgs e)
        {
            // Save settings and close
            saveSettings();
            Application.Exit();
        }

        private void groupBox1_Paint(object sender, PaintEventArgs e)
        {
            GroupBox box = sender as GroupBox;
            DrawGroupBox(box, e.Graphics, Color.White, Color.FromArgb(100, 100, 100));
        }

        private void DrawGroupBox(GroupBox box, Graphics g, Color textColor, Color borderColor)
        {
            if (box != null)
            {
                Brush textBrush = new SolidBrush(textColor);
                Brush borderBrush = new SolidBrush(borderColor);
                Pen borderPen = new Pen(borderBrush);
                SizeF strSize = g.MeasureString(box.Text, box.Font);
                Rectangle rect = new Rectangle(box.ClientRectangle.X,
                                               box.ClientRectangle.Y + (int)(strSize.Height / 2),
                                               box.ClientRectangle.Width - 1,
                                               box.ClientRectangle.Height - (int)(strSize.Height / 2) - 1);

                // Clear text and border
                g.Clear(this.BackColor);

                // Draw text
                g.DrawString(box.Text, box.Font, textBrush, box.Padding.Left, 0);

                // Drawing Border
                //Left
                g.DrawLine(borderPen, rect.Location, new Point(rect.X, rect.Y + rect.Height));
                //Right
                g.DrawLine(borderPen, new Point(rect.X + rect.Width, rect.Y), new Point(rect.X + rect.Width, rect.Y + rect.Height));
                //Bottom
                g.DrawLine(borderPen, new Point(rect.X, rect.Y + rect.Height), new Point(rect.X + rect.Width, rect.Y + rect.Height));
                //Top1
                g.DrawLine(borderPen, new Point(rect.X, rect.Y), new Point(rect.X + box.Padding.Left, rect.Y));
                //Top2
                g.DrawLine(borderPen, new Point(rect.X + box.Padding.Left + (int)(strSize.Width), rect.Y), new Point(rect.X + rect.Width, rect.Y));
            }
        }

        /* Update thread
        void updateThread()
        {
            try
            {
                // Get latest version from MinUpdater
                WebClient client = new WebClient();
                var latestVersionID = Int32.Parse(client.DownloadString("https://mu.nyodev.xyz/ver.php?id=18"));

                // Compare versions
                if (latestVersionID > currentVersion)
                {
                    // New update available
                    DialogResult dialogResult = MessageBox.Show("There is a new version of Akatsuki's Server Switcher available, do you want to download it now?", "New update available!", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.Yes)
                    {
                        Process.Start("https://mu.nyodev.xyz/upd.php?id=18");
                        Environment.Exit(0);
                    }
                }
            }
            catch
            {
                // Error
            }
        }
        */

        void updateServerIP()
        {
            try
            {
                // Get server ip from akatsuki website
                WebClient client = new WebClient();
                string remoteIP = client.DownloadString("https://akatsuki.pw/static/ip/ip.txt").TrimEnd('\r', '\n');

                // Akatsuki IP
                if (akatsukiIP != remoteIP)
                {
                    IPTextBox.Text = remoteIP;
                    if (updateServer())
                        updateStatusLabel();
                }
            }
            catch
            {
                // Error
            }
        }

        void checkOldServerIP()
        {
            try
            {
                // Get old ip from akatsuki website
                WebClient client = new WebClient();
                string[] oldIPs = client.DownloadString("http://akatsuki.pw/static/ip/oldips.txt").Split('\n');
                for (int i = 0; i < oldIPs.Length; i++)
                {
                    if (IPTextBox.Text == oldIPs[i])
                    {
                        MessageBox.Show("You are using an IP address from an old server.\nIt will be updated to our new IP.");
                        updateServerIP();
                        break;
                    }
                }
            }
            catch
            {
                // Error
            }
        }

        private void updateIPButton_Click(object sender, EventArgs e)
        {
            updateServerIP();
        }


        // Returns wether a file is read-only.
        public static bool IsFileReadOnly(string FileName)
        {
            // Create a new FileInfo object.
            FileInfo fInfo = new FileInfo(FileName);

            // Return the IsReadOnly property value.
            return fInfo.IsReadOnly;
        }

        // Sets the read-only value of a file.
        public static void SetFileReadAccess(string FileName, bool SetReadOnly)
        {
            // Create a new FileInfo object.
            FileInfo fInfo = new FileInfo(FileName);

            // Set the IsReadOnly property.
            fInfo.IsReadOnly = SetReadOnly;
        }

        private void updateCertificateButton(bool __installed = true, bool check = true)
        {
            bool installed = __installed;
            if (check)
            {
                X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, "TheHangout", true);
                installed = certs.Count > 0 ? true : false;
            }
            if (installed)
            {
                installCertificateButton.Text = "Remove certificate";
                installCertificateButton.Font = new Font(installCertificateButton.Font.Name, installCertificateButton.Font.Size, FontStyle.Regular);
            }
            else
            {
                installCertificateButton.Text = "Install certificate";
                installCertificateButton.Font = new Font(installCertificateButton.Font.Name, installCertificateButton.Font.Size, FontStyle.Bold);
            }
        }

        private void installCertificateButton_Click(object sender, EventArgs e)
        {
            // Check and install certificate
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            X509Certificate2Collection certs = store.Certificates.Find(X509FindType.FindBySubjectName, "TheHangout", true);

            if (certs.Count > 0)
            {
                // Certificate already installed, remove it
                DialogResult yn = MessageBox.Show("Are you sure you want to remove akatsuki's HTTPS certificate?\nThere's no need to remove it, you'll be able to browse both akatsuki and osu!\nwithout any problem even if the certificate is installed and the switcher is off.", "Akatsuki certificate installer", MessageBoxButtons.YesNo);
                if (yn == DialogResult.No)
                {
                    store.Close();
                    return;
                }
                try
                {
                    foreach (X509Certificate2 cert in certs)
                        store.Remove(certs[0]);

                    updateCertificateButton(false, false);
                    MessageBox.Show("Certificate removed!", "Akatsuki certificate installer");
                }
                catch
                {
                    MessageBox.Show("Error while removing certificate.", "Akatsuki certificate installer");
                }
            }
            else
            {
                // Install certificate
                try
                {
                    // Save the certificate in settingsPath temporary
                    string certFilePath = $@"{settingsPath}\certificate.crt";
                    File.WriteAllBytes(certFilePath, Resources.certificate);

                    // Get all certficates
                    X509Certificate2Collection collection = new X509Certificate2Collection();
                    collection.Import(certFilePath);

                    // Install all certificates
                    foreach (X509Certificate2 cert in collection)
                        store.Add(cert);

                    updateCertificateButton(true, false);
                    MessageBox.Show("Certificate installed! Try connecting to Akatsuki with beta/stable/cutting edge", "Akatsuki certificate installer");

                    // Delete temp certificate file
                    File.Delete(certFilePath);
                }
                catch
                {
                    MessageBox.Show("Error while installing certificate.", "Akatsuki certificate installer");
                }
            }

            store.Close();
        }

        private void localButton_Click(object sender, EventArgs e)
        {
            if (IPTextBox.Text == "127.0.0.1")
                updateServerIP();
            else
                IPTextBox.Text = "127.0.0.1";

            // Switch between akatsuki/osu!, write hosts and update label
            if (updateServer())
                updateStatusLabel();

            akatsuki = true;
        }

        private void discordImage_Click(object sender, EventArgs e)
        {
            Process.Start("https://akatsuki.pw/discord");
        }

        private void youtubeImage_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.youtube.com/channel/UCjf8Fx_BlUr-htEy6hficcQ");
        }
    }
}
