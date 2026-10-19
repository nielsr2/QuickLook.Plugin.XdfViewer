using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.XdfViewer.Settings;
using QuickLook.Plugin.XdfViewer.Xdf;
using QuickLook.Plugin.XdfViewer.Xdf.Model;
using QuickLook.Plugin.XdfViewer.ViewModels;

namespace QuickLook.Plugin.XdfViewer
{
    public class Plugin : IViewer
    {
        private XdfPreviewPanel _panel;

        public int Priority => 0;

        public void Init() { }

        public bool CanHandle(string path)
        {
            return !Directory.Exists(path)
                   && path.EndsWith(".xdf", StringComparison.OrdinalIgnoreCase);
        }

        public void Prepare(string path, ContextObject context)
        {
            context.PreferredSize = new Size(750, 550);
        }

        public void View(string path, ContextObject context)
        {
            var panel = new XdfPreviewPanel();
            _panel = panel;
            panel.ApplyTheme(context.Theme);
            var fileName = Path.GetFileName(path);
            context.ViewerContent = panel;
            context.Title = fileName;

            panel.ReloadRequested += (s, e) => Load(path, fileName, panel, context);
            Load(path, fileName, panel, context);
        }

        private static void Load(string path, string fileName, XdfPreviewPanel panel, ContextObject context)
        {
            // Read the setting on the UI thread, like every other settings access, before the background hop.
            bool regularTextAsEvents = PluginSettings.RegularTextStreamsAsEvents;

            Task.Run(() =>
            {
                XdfFile file = null;
                XdfOverviewViewModel vm = null;

                // Phase 1: read the file structure and show the overview immediately. This must
                // stay instant, so it only does the cheap structural parse (no sample decoding).
                try
                {
                    file = XdfReader.Read(path, regularTextAsEvents);
                    vm = file == null ? null : new XdfOverviewViewModel(file, fileName);
                    panel.Dispatcher.Invoke(() =>
                    {
                        panel.ShowModel(vm);
                        context.IsBusy = false;
                    });
                }
                catch (Exception ex)
                {
                    panel.Dispatcher.Invoke(() =>
                    {
                        panel.ShowError(ex.Message);
                        context.IsBusy = false;
                    });
                    return;
                }

                if (file == null || vm == null) return;

                // Phase 2: background sparkline pass. Runs after the overview is already on screen;
                // a failure here must not disturb the overview that Phase 1 already showed, so it's
                // isolated in its own try/catch and never calls ShowError.
                try
                {
                    panel.Dispatcher.Invoke(() => panel.SetDataSource(path, file));

                    var envs = XdfReader.FillEnvelopes(file, path, new EnvelopeOptions
                    {
                        Buckets = XdfReader.SparklineBuckets,
                        MaxSamplesPerStream = XdfReader.MaxEnvelopeSamplesPerStream
                    });
                    panel.Dispatcher.Invoke(() => panel.ApplyEnvelopes(envs));
                }
                catch (Exception)
                {
                    // Swallow: the overview already succeeded and is on screen. No sparklines is a
                    // degraded-but-fine outcome, not an error worth surfacing over a good preview.
                }
            });
        }

        public void Cleanup()
        {
            _panel = null;
        }
    }
}
