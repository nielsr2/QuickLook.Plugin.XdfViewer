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
            _panel = new XdfPreviewPanel();
            context.ViewerContent = _panel;
            context.Title = Path.GetFileName(path);

            Task.Run(() =>
            {
                try
                {
                    var file = XdfReader.Read(path);
                    var vm = file == null
                        ? null
                        : new XdfOverviewViewModel(file, Path.GetFileName(path));
                    _panel.Dispatcher.Invoke(() =>
                    {
                        _panel.ShowModel(vm);
                        context.IsBusy = false;
                    });
                }
                catch (Exception ex)
                {
                    _panel.Dispatcher.Invoke(() =>
                    {
                        _panel.ShowError(ex.Message);
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
