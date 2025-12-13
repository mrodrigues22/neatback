using Microsoft.UI.Xaml.Navigation;
using System.Diagnostics;
using System.IO;

namespace Slouti
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window window = Window.Current;
        private Process? _pythonServiceProcess;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            
            // Handle app exit to cleanup Python service
            AppDomain.CurrentDomain.ProcessExit += (s, e) => CleanupPythonService();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new Window();

            if (window.Content is not Frame rootFrame)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                window.Content = rootFrame;
            }

            _ = rootFrame.Navigate(typeof(MainPage), e.Arguments);
            window.Activate();
            
            // Set window title
            window.Title = "Slouti - AI Posture Guardian";
            
            // Set window icon
            window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/logo-square.ico"));
            
            // Start Python service
            StartPythonService();
        }
        
        private void StartPythonService()
        {
            try
            {
                // Kill any existing slouti-service processes to avoid port conflicts
                KillExistingServiceProcesses();
                
                string pythonServicePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "PythonService",
                    "slouti-service.exe"
                );
                
                if (!File.Exists(pythonServicePath))
                {
                    return;
                }
                
                _pythonServiceProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = pythonServicePath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };
                
                _pythonServiceProcess.OutputDataReceived += (sender, e) => { };
                
                _pythonServiceProcess.ErrorDataReceived += (sender, e) => { };
                
                _pythonServiceProcess.Start();
                _pythonServiceProcess.BeginOutputReadLine();
                _pythonServiceProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                // Service failed to start
            }
        }
        
        private void KillExistingServiceProcesses()
        {
            try
            {
                // Find and kill any existing slouti-service processes
                var existingProcesses = Process.GetProcessesByName("slouti-service");
                foreach (var process in existingProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(2000); // Wait up to 2 seconds for clean exit
                        process.Dispose();
                    }
                    catch
                    {
                        // Process might have already exited
                    }
                }
            }
            catch
            {
                // Failed to kill existing processes, continue anyway
            }
        }
        
        private void CleanupPythonService()
        {
            try
            {
                if (_pythonServiceProcess != null && !_pythonServiceProcess.HasExited)
                {
                    _pythonServiceProcess.Kill();
                    _pythonServiceProcess.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Error stopping service
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
