using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using QuickLook.Common.Plugin;
using QuickLook.Plugin.XdfViewer.Xdf;
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
            var fileName = Path.GetFileName(path);
            context.ViewerContent = panel;
            context.Title = fileName;

            Task.Run(() =>
            {
                try
                {
                    var file = XdfReader.Read(path);
                    var vm = file == null ? null : new XdfOverviewViewModel(file, fileName);
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
                }
            });
        }

        public void Cleanup()
        {
            _panel = null;
        }
    }
}
