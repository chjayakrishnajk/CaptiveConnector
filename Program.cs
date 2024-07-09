using System; 
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Linq;
using Serilog;
using System.Net.NetworkInformation;
using System.Text;
namespace CaptiveConnector{    
    class Program{
        public static string wifiInterface = "wlan0";
        static async Task Main(string[] args) {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File($"logs/log_{DateTime.Now:yyyyMMdd_HHmmss}.txt", rollingInterval: RollingInterval.Infinite ,retainedFileCountLimit: null)
            .CreateLogger();
            var wifiSetting = new WifiSetting();
            wifiSetting.Ssid = args[0];
            if(!string.IsNullOrEmpty(args[3]))
            {
                wifiInterface = args[3];
            }
            if(args[1] == "0")
            {
                await NoDogSplash();
            }
            else if(args[1] =="1")
            {
                Log.Information("Choosing Starbucks workflow");
                wifiSetting.Security = 2;
                await Starbucks(wifiSetting);
            }
            else if(args[1] == "2")
            {
                Log.Information("Choosing Testing workflow");
                wifiSetting.Key = args[2];
                await Testing();
            }
            Log.Information("Ended");
            Log.Information("Ending IP Address: " + GetWlanIpAddress());
            Log.Information("Internet: "+ !(await IsCaptivePortalAsync()));
            Log.CloseAndFlush();
        }
        static async Task Testing()
        {
            var options = new ChromeOptions();
            options.AddArgument("--no-sandbox");
            options.AddArguments("headless");
            var driver = new ChromeDriver(options);
            var captiveUrl = await GetCaptivePortalUrlAsync();
            driver.Navigate().GoToUrl(captiveUrl);
            while(await IsCaptivePortalAsync())
            {
                await AttemptLogin(driver);
            }
            Log.Information("Logged in");
        }
        static async Task NoDogSplash()
        {
            
        }
        static async Task Starbucks(WifiSetting wifiSetting)
        {
            Log.Information("Before Connecting: " + GetWlanIpAddress());
            await TryWifiConnectionWithNetworkManager(wifiSetting);
            Log.Information("After Connecting: " + GetWlanIpAddress());
            var options = new ChromeOptions();
            //options.AddArguments("headless");
            options.AddArgument("--no-sandbox");
            var driver = new ChromeDriver(options);
            Log.Information("ChromeDriver Loaded");
            var captiveUrl = await GetCaptivePortalUrlAsync();
            Log.Information("Captive Url: " + captiveUrl);
            driver.Navigate().GoToUrl(captiveUrl); 
            Thread.Sleep(2000);
            int i = 0;
            while(await IsCaptivePortalAsync() && i < 10)
            {
                await AttemptLogin(driver);
                ++i;
            }
            driver.Quit();
            Log.Information("Logged in");
            
        }
        static async Task<bool> AttemptLogin(IWebDriver driver)
        {
            var actions = new List<Func<IWebDriver, Task<bool>>>
            {
                TryClickToAcceptTerms,
                TryToEnterEmail
            }; 

            foreach (var action in actions)
            {
                if (await action(driver))
                {
                    return true;
                }
            }
            return false;
        }
        static async Task<bool> TryToEnterEmail(IWebDriver driver)
        {
            
            Log.Information("Trying to Enter Email");
            File.WriteAllText($"html/{driver.Url.Replace("/","")}{DateTime.Now.ToString("HHmmss")}.html", driver.PageSource);
            var textfields = driver.FindElements(By.XPath("//input[@type='text' or @type='email' or @type='password' and not(@type='hidden')]"));
            if(textfields.Count == 0)
            {
                Log.Information("No Text Fields Found");
                return false;
            }
            try
            {
                var email = driver.FindElement(By.XPath("//input[@type='email']"));
                Log.Information("Email Field: " + email.GetAttribute("outerHTML"));
                if (email != null)
                {
                    var tempmail = await RandomEmailGenerator.GenerateRandomEmailAsync();
                    Log.Information($"Temp mail: "+tempmail);
                    email.SendKeys(tempmail);
                    Thread.Sleep(1500);
                    var signin= await FindBestMatch(driver, "//button | //input[@type='submit' or @type='button'] | //a[@href]", new[] { "agree", "accept", "continue", "connect", "confirm", "proceed", "next", "submit", "yes", "I agree", "I accept", "start", "join", "sign up", "register", "complete", "finish", "done", "okay", "allow", "authorize", "permit", "go", "ok", "sign", "login", "access", "authenticate", "enable" },new[] {"dont","don't","no","not","google","facebook","twitter" });
                    if (signin!= null)
                    {
                        signin.Click();
                        Thread.Sleep(2000);
                        return true;
                    }
                }                
            }
            catch (Exception e)
            {
                Log.Information($"Error in TryClickToAcceptTerms: {e.Message}");
            }
            Log.Information("Returning False");
            return false;
        }
        static async Task<bool> TryClickToAcceptTerms(IWebDriver driver)
        {
            Log.Information("Trying to accept terms");
            File.WriteAllText($"html/{driver.Url.Replace("/","")}{DateTime.Now.ToString("HHmmss")}.html", driver.PageSource);
            try
            {
                // Find and click the checkbox
                var checkbox = await FindBestMatch(driver, "//input[@type='checkbox']", new[] { "accept", "acceptance", "accepted", "agree", "agreed", "agreement", "terms", "conditions", "terms and conditions", "consent", "approve", "approval", "acknowledge", "acknowledgment", "comply", "compliance" },new[] {"dont","don't","no","not","google","facebook","twitter" });
                if (checkbox != null)
                {
                    checkbox.Click();
                    Thread.Sleep(1000);
                }
                var textfields = driver.FindElements(By.XPath("//input[@type='text' or @type='email' or @type='password' and not(@type='hidden')]"));
                if(textfields.Count !=0)
                {
                    Log.Information($"{textfields.Count} Text Fields Found");
                    return false;
                }
                Log.Information("Checking for agree button");
                // Find and click the agree button                    
                var agreeButton = await FindBestMatch(driver, "//button | //input[@type='submit' or @type='button'] | //a[@href]", new[] { "agree", "accept", "continue", "connect", "confirm", "proceed", "next", "submit", "yes", "I agree", "I accept", "start", "join", "sign up", "register", "complete", "finish", "done", "okay", "allow", "authorize", "permit", "go", "ok", "sign", "login", "access", "authenticate", "enable" },new[] {"dont","don't","no","not","google","facebook","twitter" });

                if (agreeButton != null)
                {
                    agreeButton.Click();
                    Thread.Sleep(2000);
                    return true;
                }
            }
            catch (Exception e)
            {
                Log.Information($"Error in TryClickToAcceptTerms: {e.Message}");
            }
            Log.Information("Returning False");
            return false;
        }
        static async Task<IWebElement> FindBestMatch(IWebDriver driver, string xpath, string[] keywords,string[] negative)
        {
            Log.Information("XPATH: " + xpath);
            var elements = driver.FindElements(By.XPath(xpath));
            Log.Information("Got Elements");
            Log.Information($"{elements.Count()} elements");
            foreach(var element in elements)
            {
                List<string> texts = new List<string>();            
                texts.Add(element.Text?.ToLower() ?? "");
                texts.Add(element.GetAttribute("value")?.ToLower() ?? "");
                texts.Add(element.GetAttribute("name")?.ToLower() ?? "");
                Log.Information("Element: " + element.GetAttribute("outerHTML"));
                foreach(string text in texts)
                {
                    if(keywords.Any(x=> text.Contains(x)) && !string.IsNullOrEmpty(text)&& !negative.Any(x=> text.Contains(x)))
                    {
                        Log.Information($"{keywords.Where(x=> x.Contains(text)).FirstOrDefault()} Matched with {text}");
                        return element;
                    }
                    else
                    {
                        if(negative.Any(x=> text.Contains(x)))
                        {
                            Log.Information($"Found a negative word {negative.Where(x=> text.Contains(x)).FirstOrDefault()} in {text}");
                        }
                    }
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
                synonyms = synonyms.ConvertAll(d =>d.ToLower());
                synonyms.Add(word.ToLower());
            }
        }
        return synonyms;
    }
    static async Task<String> GetCaptivePortalUrlAsync()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false // Disable automatic redirect following
        };
        using (HttpClient client = new HttpClient(handler))
        {
            try
            {
                // Send a GET request
                HttpResponseMessage response = await client.GetAsync("http://www.msftconnecttest.com/connecttest.txt");
                Log.Information("Response from Connect Test: " + await response.Content.ReadAsStringAsync());
                // Check if the response contains a redirect
                if (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.RedirectKeepVerb || response.StatusCode == HttpStatusCode.RedirectMethod)
                {
                    // Get the Location header value
                    if (response.Headers.Location != null)
                    {
                        string location = response.Headers.Location.ToString();
                        Log.Information("Redirect Location: " + location);
                        return location;
                    }
                    else
                    {
                        Log.Information("No Location header found.");
                    }
                }
                else
                {
                    Log.Information("No redirect occurred. Status Code: " + response.StatusCode);
                }
            }
            catch (HttpRequestException e)
            {
                Log.Information("Request error: " + e.Message);
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
                Log.Information($"Error checking captive portal: {ex.Message}");
                return false;
            }
        }  
        static string GetWlanIpAddress()
         {
             try{
                 return NetworkInterface.GetAllNetworkInterfaces()
                     .FirstOrDefault(ni => ni.Name.ToLower() == wifiInterface &&
                                           ni.OperationalStatus == OperationalStatus.Up)
                     ?.GetIPProperties()
                     .UnicastAddresses
                     .FirstOrDefault(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                     ?.Address.ToString();
             }
             catch(Exception ex)
             {
                 return "Not found";
             }
         }
        private static async Task<bool> TryWifiConnectionWithNetworkManager(WifiSetting wifiSetting)
        {
            var di = new DirectoryInfo(@"/etc/NetworkManager/system-connections");
            Log.Information("Files Found: " + di.GetFiles().Count());
            foreach (var item in di.GetFiles())
            {
                if(!item.FullName.Contains("preconfigure"))
                {
                    Log.Information("Delete File: " + item.FullName);
                    File.Delete(item.FullName);
                }
            }
            Log.Information("Files Left: " + di.GetFiles().Count());
            await Task.Delay(1000);
            var cmd = $@"nmcli connection reload";
            ShellHelper.ExecuteProcess("sudo", cmd, "");
            cmd = "sudo nmcli device wifi rescan";
            ShellHelper.ExecuteProcess("sudo",cmd,"");
            if (wifiSetting.Security == 0)
            {
                cmd = $@"sudo nmcli c add type wifi con-name {wifiSetting.Ssid} ifname {wifiInterface} ssid {wifiSetting.Ssid}  802-11-wireless-security.key-mgmt wpa-psk 802-11-wireless-security.psk {wifiSetting.Key}";
                Log.Information("Running cmd: "+ cmd);
                ShellHelper.ExecuteProcess("sudo", cmd, "");
            }
            else if(wifiSetting.Security == 1)
            {
                cmd = $@"sudo nmcli connection add type wifi con-name ""{wifiSetting.Ssid}"" connection.autoconnect-priority 1 ifname {wifiInterface} ssid ""{wifiSetting.Ssid}"" wifi-sec.key-mgmt wpa-eap 802-1x.eap peap 802-1x.phase2-auth mschapv2 802-1x.identity ""{wifiSetting.UserName}"" 802-1x.password ""{wifiSetting.Password}""";
                Log.Information("Running cmd: "+ cmd);
                ShellHelper.ExecuteProcess("sudo", cmd, "");
            }
            else if(wifiSetting.Security == 2)
            {
                cmd = $@"sudo nmcli c add type wifi con-name {wifiSetting.Ssid} ifname {wifiInterface} ssid {wifiSetting.Ssid}";
                cmd = $"sudo nmcli device wifi connect \"{wifiSetting.Ssid}\""; //temp
                Log.Information("Running cmd: "+ cmd);
                ShellHelper.ExecuteProcess("sudo", cmd, "");
            }
            

            cmd = $@"sudo nmcli c up {wifiSetting.Ssid} ";
            Log.Information("Running cmd: "+ cmd);
            ShellHelper.ExecuteProcess("sudo", cmd, ""); 

            var tryCount = 5;
            var tryIndex = 0;
            while (tryIndex < tryCount)
            {
                Log.Information("Checking PING");
                var pingMs = CheckInternet();
                Log.Information(pingMs.ToString());
                if (pingMs > 0)
                {
                    Log.Information("PING is UP");
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
                var curlOutput = ShellHelper.ExecuteProcess("sudo", "curl http://www.google.com", "");
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
            var output = proc.StandardOutput.ReadToEnd();
            Log.Debug($"Output of {arguments} is {output}");
            return output; 
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
    public class RandomEmailGenerator
{
    private static readonly Random random = new Random();
    private static readonly string[] domains = { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "example.com" };

    public static async Task<string> GenerateRandomEmailAsync()
    {
        string username = await GenerateRandomStringAsync(8);
        string domain = await Task.Run(() => domains[random.Next(domains.Length)]);
        return $"{username}@{domain}";
    }

    private static async Task<string> GenerateRandomStringAsync(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        StringBuilder sb = new StringBuilder(length);

        await Task.Run(() =>
        {
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[random.Next(chars.Length)]);
            }
        });

        return sb.ToString();
    }
}
}
