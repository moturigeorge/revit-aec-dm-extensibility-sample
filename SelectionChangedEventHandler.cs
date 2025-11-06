using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.Net.Http;
using System.Windows.Controls;

namespace revit_aec_dm_extensibility_sample
{
    public class SelectionChangedEventHandler : IExternalEventHandler
    {
        private readonly AttributesPanelProvider _customPanelProvider;
        private readonly Guid _panelGuid;

        public SelectionChangedEventHandler(AttributesPanelProvider customPanelProvider, Guid panelGuid)
        {
            _customPanelProvider = customPanelProvider;
            _panelGuid = panelGuid;
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var uidoc = uiapp.ActiveUIDocument;

                if (uidoc != null)
                {
                    var selection = uidoc.Selection.GetElementIds();
                    if (selection.Count == 1)
                    {
                        var element = uidoc.Document.GetElement(selection.First());
                        // Check if it's a door or window
                        if (element is FamilyInstance familyInstance && (familyInstance.Symbol.Family.FamilyCategory.BuiltInCategory == BuiltInCategory.OST_Doors || familyInstance.Symbol.Family.FamilyCategory.BuiltInCategory == BuiltInCategory.OST_Windows))
                        {
                            var panelId = new DockablePaneId(_panelGuid);
                            var pane = uiapp.GetDockablePane(panelId);
                            pane.Show();

                            var externalId = familyInstance.UniqueId;
                            var selectedElementName = familyInstance.Symbol.Family.FamilyCategory.Name;
                            string hubId = null;
                            string projectId = null;
                            string modelUrn = null;
                            if (uidoc.Document.IsModelInCloud)
                            {
                                ModelPath modelPath = uidoc.Document.GetCloudModelPath();
                                if (modelPath != null && modelPath.CloudPath)
                                {
                                    hubId = uidoc.Document.GetHubId();
                                    projectId = uidoc.Document.GetProjectId();
                                    modelUrn = uidoc.Document.GetCloudModelUrn();

                                    //string projectGuid = modelPath.GetProjectGUID().ToString();
                                    //string modelGuid = modelPath.GetModelGUID().ToString();

                                    _customPanelProvider.ClearPanelContent();

                                    _customPanelProvider.FilterAndShowResults(hubId, projectId, modelUrn, selectedElementName, externalId);
                                }
                            }
                            else
                            {
                                _customPanelProvider.ClearPanelContent();

                                _customPanelProvider.FilterAndShowResults(hubId, projectId, modelUrn, selectedElementName, externalId);
                                TaskDialog.Show("Info", "This is not a cloud model.");
                            }
                        }
                        else
                        {
                            var panelId = new DockablePaneId(_panelGuid);
                            var pane = uiapp.GetDockablePane(panelId);
                            pane.Hide();

                            var propertiesPaneId = DockablePanes.BuiltInDockablePanes.PropertiesPalette;
                            var propertiesPane = uiapp.GetDockablePane(propertiesPaneId);
                            propertiesPane.Show();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public string GetName()
        {
            return "Selection Handler";
        }
    }
}