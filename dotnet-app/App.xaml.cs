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
            
            // Set window icon
            window.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/logo-square.ico"));
            
            // Start Python service
            StartPythonService();
        }
        
        private void StartPythonService()
        {
            try
            {
                string pythonServicePath = Path.Combine(
                    AppContext.BaseDirectory,
                    "PythonService",
                    "slouti-service.exe"
                );
                
                if (!File.Exists(pythonServicePath))
                {
                    Debug.WriteLine($"Python service not found at: {pythonServicePath}");
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
                
                _pythonServiceProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[Python Service] {e.Data}");
                    }
                };
                
                _pythonServiceProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"[Python Service Error] {e.Data}");
                    }
                };
                
                _pythonServiceProcess.Start();
                _pythonServiceProcess.BeginOutputReadLine();
                _pythonServiceProcess.BeginErrorReadLine();
                
                Debug.WriteLine($"Python service started successfully (PID: {_pythonServiceProcess.Id})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start Python service: {ex.Message}");
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
                    Debug.WriteLine("Python service stopped");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping Python service: {ex.Message}");
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
