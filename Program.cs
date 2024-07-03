using System; 
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Linq;
namespace CaptiveConnector{    
    class Program{
        static async Task Main(string[] args) {
            var wifiSetting = new WifiSetting();
            wifiSetting.Ssid = args[0];
            if(args[1] == "0")
            {
                await NoDogSplash();
            }
            else if(args[1] =="1")
            {
                await Starbucks();
            }
            else if(args[1] == "2")
            {
                await NoCaptivePortal();
            }
        }
        static async Task NoCaptivePortal()
        {
             
        }
        static async Task NoDogSplash()
        {
            
        }
        static async Task Starbucks()
        {
            
        }
        static bool AttemptLogin(IWebDriver driver)
        {
            var actions = new List<Func<IWebDriver, bool>>
            {
                TryClickToAcceptTerms
            };

            foreach (var action in actions)
            {
                if (action(driver))
                {
                    return true;
                }
            }

            return false;
        }
        static bool TryClickToAcceptTerms(IWebDriver driver)
        {
            try
            {
                // Find and click the checkbox
                var checkbox = FindBestMatch(driver, "//input[@type='checkbox']", new[] { "accept", "agree", "terms" });
                if (checkbox != null)
                {
                    checkbox.Click();
                    Thread.Sleep(1000);
                }

                // Find and click the agree button
                var agreeButton = FindBestMatch(driver, "//button | //input[@type='submit'] | //a", new[] { "agree", "accept", "continue" });
                if (agreeButton != null)
                {
                    agreeButton.Click();
                    Thread.Sleep(2000);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error in TryClickToAcceptTerms: {e.Message}");
            }
            return false;
        }
        static async Task<IWebElement> FindBestMatch(IWebDriver driver, string xpath, string[] keywords)
        {
            var elements = driver.FindElements(By.XPath(xpath));
            foreach(var element in elements)
            {
                foreach(var keyword in keywords)
                {
//                    if((await GetSynonyms(keyword)).FirstOrDefault(stringToCheck => stringToCheck.Contains(keyword)));
                }
            }
            return null;
        }
    static async Task<List<string>> GetSynonyms(string word)
    {
        string pythonPath = "/home/jk/.venv/bin/python3"; // or specify full path if necessary
        string scriptPath = "/home/jk/Downloads/Starbucks/similarity.py"; // replace with actual path
        List<string> synonyms = new List<string>();
        // Command-line arguments to pass to Python script (if any)
        string arguments = $"{word}";

        // Create process start info
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = pythonPath;
        startInfo.Arguments = $"\"{scriptPath}\" {arguments}";
        startInfo.RedirectStandardOutput = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
    
        // Start the process
        using (Process process = Process.Start(startInfo))
        {
            // Read output (synonyms) from Python script
            using (var reader = process.StandardOutput)
            {
                string result = reader.ReadToEnd();
                string[] wordsArray = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                synonyms = new List<string>(wordsArray);
            }
        }
        return synonyms;
    }
    static async Task<String> GetCaptivePortalUrlAsync()
    {
        string hostname = "www.msftconnecttest.com";
        
        // Resolve the hostname to get IP addresses
        IPHostEntry hostEntry = await Dns.GetHostEntryAsync(hostname);
        
        foreach (IPAddress ip in hostEntry.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return null;
    }
      static async Task<bool> IsCaptivePortalAsync()
        {
        string url = "http://www.msftconnecttest.com/connecttest.txt";
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(url);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        if (content.Contains("Microsoft Connect Test"))
                        {
                            return false; // No captive portal
                        }
                    }

                    return true; // Captive portal detected
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking captive portal: {ex.Message}");
                return false;
            }
        }   
        private static async Task<bool> TryWifiConnectionWithNetworkManager(WifiSetting wifiSetting)
        {

            var di = new DirectoryInfo(@"/etc/NetworkManager/system-connections");
            Console.WriteLine("Files Found: " + di.GetFiles().Count());
            foreach (var item in di.GetFiles())
            {
                Console.WriteLine("Delete File: " + item.FullName);
                File.Delete(item.FullName);
            }
            Console.WriteLine("Files Left: " + di.GetFiles().Count());

            var cmd = $@"nmcli connection reload";
            ShellHelper.ExecuteProcess("sudo", cmd, "");
            var ifname = $@"wlp1s0";
            if (wifiSetting.Security == 0)
            {
                cmd = $@"sudo nmcli c add type wifi con-name {wifiSetting.Ssid} ifname {ifname} ssid {wifiSetting.Ssid}  802-11-wireless-security.key-mgmt wpa-psk 802-11-wireless-security.psk {wifiSetting.Key}";
                Console.WriteLine(cmd);
                ShellHelper.ExecuteProcess("sudo", cmd, "");
            }
            else if(wifiSetting.Security == 1)
            {
                cmd = $@"sudo nmcli connection add type wifi con-name ""{wifiSetting.Ssid}"" connection.autoconnect-priority 1 ifname {ifname} ssid ""{wifiSetting.Ssid}"" wifi-sec.key-mgmt wpa-eap 802-1x.eap peap 802-1x.phase2-auth mschapv2 802-1x.identity ""{wifiSetting.UserName}"" 802-1x.password ""{wifiSetting.Password}""";
                Console.WriteLine(cmd);
                ShellHelper.ExecuteProcess("sudo", cmd, "");
            }
            else
            {
                cmd = $@"sudo nmcli c add type wifi con-name {wifiSetting.Ssid} ifname {ifname} ssid {wifiSetting.Ssid}";
                Console.WriteLine(cmd);
                ShellHelper.ExecuteProcess("sudo", cmd, "");
            }
            

            cmd = $@"sudo nmcli c up {wifiSetting.Ssid} ";
                Console.WriteLine(cmd);
            ShellHelper.ExecuteProcess("sudo", cmd, "");

            var tryCount = 5;
            var tryIndex = 0;
            while (tryIndex < tryCount)
            {

                var pingMs = CheckInternet();
                if (pingMs > 0)
                {
                    return true;
                }
                await Task.Delay(1000);

                tryIndex++;
            }
            return false;
        }
        private static decimal CheckInternet()
        {

            //_logger.LogInformation("CheckInternet: 0");

            var arguments = "ping -c 1 8.8.8.8";
            var proc = new Process
            {
                EnableRaisingEvents = false,
                StartInfo =
                            {
                                FileName = "sudo",
                                Arguments = arguments,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false
                            }
            };
            proc.Start();
            var ms = decimal.Zero;
            var data = proc.StandardOutput.ReadToEnd();
            //_logger.LogInformation("CheckInternet: " + data);
            foreach (var item in data.Split('\n'))
            {

                if (item.Contains("icmp_seq=1"))
                {
                    var parts = item.Split(Convert.ToChar("="));
                    try
                    {
                        ms = Convert.ToDecimal((parts.LastOrDefault()).Replace(" ms", ""));
                    }
                    catch (Exception)
                    {

                      //  _logger.LogError("CheckInternet: Decimal Parse Error: " + item);
                    }

                }

            }
            //_logger.LogInformation("CheckInternet: " + ms);

            if (ms == 0)
            {
                var curlOutput = ShellHelper.ExecuteProcess("sudo", "curl http://www.gooogle.com", "");
                if (curlOutput.Trim().Length > 0)
                {
                  
                    if (!curlOutput.ToLowerInvariant().StartsWith("curl: (6) could not resolve host:"))
                    {
                        ms = 99;
                    }
                    else
                    {
                    }

                }
            }
            else
            {
                if ((int)ms == 99)
                {
                    ms = 98;
                }
            }


            return ms;
        }
 
    }
    public static class ShellHelper
    {
        public static string ExecuteProcess(string fileName, string arguments, string workingDirectory)
        {
            var proc = new Process
            {
                EnableRaisingEvents = false,
                StartInfo =
                            {
                                FileName = fileName,
                                Arguments = arguments,
                                WorkingDirectory = workingDirectory,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false
                            }
            };
            proc.Start();
            proc.WaitForExit();
            return proc.StandardOutput.ReadToEnd();
        }
    }
    public class WifiSetting
    {
        public byte Index { get; set; } = 0;
        public bool Hidden { get; set; } = false;
        public string Ssid { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

        public byte Security { get; set; } = 0;

        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;


        public bool IsLastKnown { get; set; } = false;
        public bool IsSystemDefault { get; set; } = false;


    }
} 
