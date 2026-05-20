using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;

namespace SquadDash.Tests;

/// <summary>
/// Runs an action on a dedicated STA thread with a pumping WPF Dispatcher.
/// Required for any test that creates or manipulates WPF objects (RichTextBox,
/// TextPointer, adorners, etc.).
/// </summary>
internal static class WpfTestContext
{
    /// <summary>Runs <paramref name="action"/> on a fresh STA thread with a live Dispatcher.</summary>
    internal static void Run(Action action)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;

            // Queue the action so it runs once the pump is running.
            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                finally
                {
                    dispatcher.InvokeShutdown();
                }
            });

            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (caught != null)
            ExceptionDispatchInfo.Capture(caught).Throw();
    }

    /// <summary>Runs an async action on a fresh STA thread with a live Dispatcher.</summary>
    internal static void Run(Func<Task> asyncAction)
    {
        Run(() => asyncAction().GetAwaiter().GetResult());
    }
}
