using System;

namespace CSharp_ImGui_Client
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                System.IO.File.WriteAllText("crashlog.txt", "AppDomain Unhandled:\r\n" + e.ExceptionObject?.ToString());
            };
            
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.IO.File.WriteAllText("crashlog.txt", "Task Unobserved:\r\n" + e.Exception?.ToString());
            };

            try
            {
                var app = new System.Windows.Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    System.IO.File.WriteAllText("crashlog.txt", "Dispatcher Unhandled:\r\n" + e.Exception?.ToString());
                    e.Handled = true;
                };
                app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
                app.Startup += (s, e) =>
                {
                    try
                    {
                        var login = new LoginWindow();
                        if (login.ShowDialog() == true)
                        {
                            var main = new MainWindow();
                            app.MainWindow = main;
                            app.ShutdownMode = System.Windows.ShutdownMode.OnLastWindowClose;
                            main.Show();
                        }
                        else
                        {
                            app.Shutdown();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText("crashlog.txt", ex.ToString());
                        app.Shutdown();
                    }
                };
                app.Run();
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("crashlog.txt", ex.ToString());
            }
        }
    }
}
