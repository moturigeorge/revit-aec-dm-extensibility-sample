#region Namespaces
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using revit_aec_dm_extensibility_sample.Services;
using revit_aec_dm_extensibility_sample.TokenHandlers;
using System.IO;
#endregion

namespace revit_aec_dm_extensibility_sample
{
    internal class App : IExternalApplication
    {
        private readonly IMemoryStorage _cache = new MemoryStorage();
        private readonly Guid _panelGuid = new Guid("4416B90A-C22C-4D05-AE27-F962761FFC30");
        private AttributesPanelProvider _customPanelProvider;
        private ExternalEvent _externalEvent;
        private SelectionChangedEventHandler _selectionHandler;
        private UIControlledApplication _application;
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                _application = application; // Store the reference

                string token = TokenHandler.Login();
                if (string.IsNullOrEmpty(token))
                {
                    TaskDialog.Show("Error", "Failed to obtain authentication token");
                    return Result.Failed;
                }


                _customPanelProvider = new AttributesPanelProvider(TokenHandler.GetCurrentToken, _cache);

                var panelId = new DockablePaneId(_panelGuid);
                application.RegisterDockablePane(panelId, "Cloud Custom Properties", _customPanelProvider);

                _selectionHandler = new SelectionChangedEventHandler(_customPanelProvider, _panelGuid);
                _externalEvent = ExternalEvent.Create(_selectionHandler);

                _application.ViewActivated += OnViewActivated;
                _application.SelectionChanged += OnSelectionChanged;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during OnStartup: " + ex.ToString());
                return Result.Failed;
            }
        }

        private void OnViewActivated(object sender, Autodesk.Revit.UI.Events.ViewActivatedEventArgs e)
        {
            try
            {
                Document doc = e.Document;

                if (doc != null)
                {
                    string fullPath = doc.PathName;
                    string fileName = Path.GetFileName(fullPath);

                    if (string.IsNullOrEmpty(fileName))
                    {
                        Console.WriteLine("File has not been saved yet.");
                    }
                    _customPanelProvider.SetRevitFileName(fileName);

                    var panelId = new DockablePaneId(_panelGuid);
                    var dockablePane = _application.GetDockablePane(panelId);
                    if (dockablePane != null)
                    {
                        dockablePane.Hide();
                    }
                }
                else
                {
                    Console.WriteLine("No active document found on ViewActivated (e.Document was null).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in ViewActivated event handler: " + ex.ToString());
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (_application != null)
                {
                    _application.SelectionChanged -= OnSelectionChanged;
                }

                if (_externalEvent != null)
                {
                    _externalEvent.Dispose();
                    _externalEvent = null;
                }
                TokenHandler.Cleanup();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return Result.Failed;
            }
        }

        private void OnSelectionChanged(object sender, Autodesk.Revit.UI.Events.SelectionChangedEventArgs e)
        {
            try
            {
                _externalEvent.Raise();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

    }
}