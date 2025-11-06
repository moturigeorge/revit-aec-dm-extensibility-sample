using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using revit_aec_dm_extensibility_sample.Models;
using revit_aec_dm_extensibility_sample.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static revit_aec_dm_extensibility_sample.Services.GraphQLService;
using TextBox = System.Windows.Controls.TextBox;

namespace revit_aec_dm_extensibility_sample
{
    /// <summary>
    /// Provides a dockable panel for displaying and updating AEC Data Model element properties
    /// </summary>
    public class AttributesPanelProvider : UserControl, IDockablePaneProvider
    {
        #region Private Fields

        private readonly IMemoryStorage _cache;
        private readonly StackPanel _panel;
        private readonly StackPanel _contentPanel;
        private readonly Button _refreshButton;
        private readonly Func<string> _tokenGetter;
        private readonly TextBlock _loaderMessage;
        private readonly ILogger<AttributesPanelProvider> _logger;

        private string _documentId;
        private string _selectedElementName;
        private string _currentRevitFileName;
        private string _hubId;
        private string _projectId;
        private string _elementDesignId;
        private string _modelUrn;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the AttributesPanelProvider
        /// </summary>
        /// <param name="tokenGetter">Function to retrieve authentication token</param>
        /// <param name="cache">Optional memory cache for API responses</param>
        /// <param name="logger">Optional logger for diagnostic information</param>
        public AttributesPanelProvider(
            Func<string> tokenGetter,
            IMemoryStorage cache = null,
            ILogger<AttributesPanelProvider> logger = null)
        {
            _cache = cache ?? new MemoryStorage();
            _tokenGetter = tokenGetter ?? throw new ArgumentNullException(nameof(tokenGetter));
            _logger = logger;

            // Create the main panel with ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _panel = new StackPanel
            {
                Background = SystemColors.WindowBrush,
            };

            _contentPanel = new StackPanel
            {
                MinHeight = 200,
            };

            _loaderMessage = new TextBlock
            {
                Text = "Loading...",
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(10),
                Visibility = Visibility.Collapsed
            };

            _panel.Children.Add(_loaderMessage);
            _panel.Children.Add(_contentPanel);

            // Initialize refresh button
            _refreshButton = CreateRefreshButton();

            // Initialize update all button
            var updateAllButton = CreateUpdateAllButton();

            // Create button panel
            var buttonPanel = new StackPanel
            {
                Margin = new Thickness(5),
            };
            buttonPanel.Children.Add(_refreshButton);
            buttonPanel.Children.Add(updateAllButton);

            _panel.Children.Add(buttonPanel);

            scrollViewer.Content = _panel;
            this.Content = scrollViewer;
        }

        #endregion

        #region UI Initialization Methods

        /// <summary>
        /// Creates and configures the refresh button
        /// </summary>
        private Button CreateRefreshButton()
        {
            var button = new Button
            {
                Content = "Reload Latest",
                MinWidth = 50,
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(0, 114, 197)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Reload the latest properties from the cloud"
            };

            // Apply style if available
            if (Application.Current.Resources.Contains("MaterialDesignRaisedButton"))
            {
                button.Style = (Style)Application.Current.Resources["MaterialDesignRaisedButton"];
            }

            button.MouseEnter += (s, e) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(0, 90, 158));
            };

            button.MouseLeave += (s, e) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(0, 114, 197));
            };

            button.Click += async (sender, e) =>
            {
                await RefreshPropertiesAsync();
            };

            return button;
        }

        /// <summary>
        /// Creates and configures the update all button
        /// </summary>
        private Button CreateUpdateAllButton()
        {
            var button = new Button
            {
                Content = "Save All to Cloud",
                MinWidth = 50,
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(0, 114, 197)),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Save all modified properties to the cloud"
            };

            // Apply style if available
            if (Application.Current.Resources.Contains("MaterialDesignRaisedButton"))
            {
                button.Style = (Style)Application.Current.Resources["MaterialDesignRaisedButton"];
            }

            button.MouseEnter += (s, e) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(0, 90, 158));
            };

            button.MouseLeave += (s, e) =>
            {
                button.Background = new SolidColorBrush(Color.FromRgb(0, 114, 197));
            };

            button.Click += UpdateAllButton_Click;

            return button;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Clears all content from the properties panel
        /// </summary>
        public void ClearPanelContent()
        {
            Dispatcher.Invoke(() =>
            {
                _contentPanel.Children.Clear();
            });
        }

        /// <summary>
        /// Sets the current Revit file name for filtering element groups
        /// </summary>
        /// <param name="fileName">The Revit file name</param>
        public void SetRevitFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger?.LogWarning("Attempted to set null or empty Revit file name");
                return;
            }

            _currentRevitFileName = fileName;
            _logger?.LogInformation("Revit file name set: {FileName}", fileName);
        }

        /// <summary>
        /// Displays loader indicator
        /// </summary>
        public void DisplayLoader()
        {
            Dispatcher.Invoke(() =>
            {
                _refreshButton.IsEnabled = false;
                ClearPanelContent();
                _loaderMessage.Visibility = Visibility.Visible;
            });
        }

        /// <summary>
        /// Hides loader indicator
        /// </summary>
        public void HideLoader()
        {
            Dispatcher.Invoke(() =>
            {
                _refreshButton.IsEnabled = true;
                _loaderMessage.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// Filters and displays element properties from the AEC Data Model
        /// </summary>
        public async void FilterAndShowResults(string hubId, string projectId, string modelUrn, string selectedElementName, string externalId)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(externalId))
            {
                _logger?.LogWarning("FilterAndShowResults called with null or empty external ID");
                HandleError("Invalid element selection");
                return;
            }

            try
            {
                // Store context for refresh operations
                _hubId = hubId;
                _projectId = projectId;
                _modelUrn = modelUrn;
                _selectedElementName = selectedElementName;
                _documentId = externalId;

                DisplayLoader();

                string token = _tokenGetter();
                if (string.IsNullOrWhiteSpace(token))
                {
                    HandleError("Authentication token is not available");
                    return;
                }

                var graphQLClient = new GraphQLService(token, _cache);

                // Get Hub
                var hub = await graphQLClient.GetHubByIdAsync(hubId);
                if (hub == null)
                {
                    _logger?.LogWarning("Hub not found: {HubId}", hubId);
                    HandleError("Your ClientId is not integrated in the cloud model location");
                    return;
                }

                // Get Project
                var project = await graphQLClient.GetProjectByIdAsync(hub.Id, projectId);
                if (project == null)
                {
                    _logger?.LogWarning("Project not found: {ProjectId} in hub {HubId}", projectId, hub.Id);
                    HandleError("No project found");
                    return;
                }

                // Get Element Groups
                var elementGroups = await graphQLClient.GetElementGroupsByProjectAsync(project.Id, modelUrn);
                if (elementGroups == null || !elementGroups.Any())
                {
                    _logger?.LogWarning("No element groups found for project {ProjectId}", project.Id);
                    HandleError("No element group found for this Revit file");
                    return;
                }

                // Filter element groups
                var elementDesign = elementGroups
                    //.Where(eg => eg.Name == _currentRevitFileName)
                    .Where(eg => eg.Components?.Results?.Any(comp => !string.IsNullOrEmpty(comp?.ElementGroup?.Id)) == true)
                    .SelectMany(eg => eg.Components.Results
                        .Where(comp => !string.IsNullOrEmpty(comp?.ElementGroup?.Id))
                        .Select(comp => new
                        {
                            extensionElementGroupId = eg.Id,
                            modelElementGroupId = comp.ElementGroup.Id
                        }))
                    .FirstOrDefault();

                if (elementDesign == null || string.IsNullOrEmpty(elementDesign.extensionElementGroupId))
                {
                    _logger?.LogWarning("No valid element group ID found in components");
                    HandleError("No valid element group ID found in components");
                    return;
                }

                _elementDesignId = elementDesign.extensionElementGroupId;
                var elementGroupId = elementDesign.modelElementGroupId;

                // Get associated elements
                var (elementGpId, associatedElements) = await graphQLClient.GetAssociatedElementIdsAsync(elementGroupId, selectedElementName, externalId);

                if (associatedElements == null || !associatedElements.Any())
                {
                    _logger?.LogWarning("No elements found for {ElementName} with ID {ExternalId}", selectedElementName, externalId);
                    HandleError("No elements found matching the criteria");
                    return;
                }

                // Get properties for all elements
                var elementsWithProperties = new List<(ExtendedElement element, bool isType)>();

                foreach (var (elementId, isType) in associatedElements)
                {
                    var elementProps = await graphQLClient.GetExtensionPropertiesAsync(new List<string> { elementId });
                    if (elementProps != null && elementProps.Any())
                    {
                        elementsWithProperties.Add((elementProps.First(), isType));
                    }
                }

                if (!elementsWithProperties.Any())
                {
                    _logger?.LogWarning("No properties found for elements");
                    HandleError("No properties found for the selected elements");
                    return;
                }

                UpdatePropertiesUI(elementsWithProperties);
                _logger?.LogInformation("Successfully loaded {Count} elements with properties", elementsWithProperties.Count);
            }
            catch (HttpRequestException httpEx)
            {
                _logger?.LogError(httpEx, "Network error while fetching element properties");
                HandleError($"Network error: {httpEx.Message}");
            }
            catch (JsonException jsonEx)
            {
                _logger?.LogError(jsonEx, "JSON parsing error");
                HandleError($"Data parsing error: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error in FilterAndShowResults");
                HandleError($"Unexpected error: {ex.Message}");
            }
            finally
            {
                HideLoader();
            }
        }

        /// <summary>
        /// Sets up the dockable pane configuration
        /// </summary>
        public void SetupDockablePane(DockablePaneProviderData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                data.FrameworkElement = this;
                data.InitialState = new DockablePaneState
                {
                    DockPosition = DockPosition.Tabbed,
                    TabBehind = DockablePanes.BuiltInDockablePanes.PropertiesPalette,
                    MinimumWidth = 300,
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting up dockable pane");
                throw;
            }
        }

        #endregion

        #region Private Event Handlers

        /// <summary>
        /// Handles the update all button click event
        /// </summary>
        private async void UpdateAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DisableButtons(_contentPanel);

                var updateTasks = new List<Task<bool>>();

                foreach (var child in _contentPanel.Children)
                {
                    if (child is Border border && border.Child is StackPanel cardContent)
                    {
                        var propertiesGrid = cardContent.Children
                            .OfType<Grid>()
                            .FirstOrDefault();

                        if (propertiesGrid != null)
                        {
                            for (int i = 0; i < propertiesGrid.RowDefinitions.Count; i++)
                            {
                                var textBox = propertiesGrid.Children.OfType<TextBox>().FirstOrDefault(tb => Grid.GetRow(tb) == i);

                                var button = propertiesGrid.Children.OfType<Button>().FirstOrDefault(b => Grid.GetRow(b) == i);

                                if (textBox != null && button?.Tag is PropertyUpdateContext context)
                                {
                                    string newValue = textBox.Text;
                                    updateTasks.Add(UpdateSinglePropertyAsync(context.ElementId, context.DefinitionId, newValue));
                                }
                            }
                        }
                    }
                }

                if (!updateTasks.Any())
                {
                    MessageBox.Show("No properties to update.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var results = await Task.WhenAll(updateTasks);
                bool allUpdatesSuccessful = results.All(success => success);

                if (allUpdatesSuccessful)
                {
                    _logger?.LogInformation("All {Count} properties updated successfully", results.Length);
                    MessageBox.Show("All properties updated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    await RefreshPropertiesAsync();
                }
                else
                {
                    int failedCount = results.Count(success => !success);
                    _logger?.LogWarning("{FailedCount} of {TotalCount} property updates failed", failedCount, results.Length);
                    MessageBox.Show($"Some properties failed to update. {failedCount} of {results.Length} updates failed.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in UpdateAllButton_Click");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EnableButtons(_contentPanel);
            }
        }

        /// <summary>
        /// Handles individual property update button clicks
        /// </summary>
        private async void OnUpdateButtonClick(object sender, RoutedEventArgs e, string elementId, string definitionId, TextBox valueTextBox)
        {
            if (sender is not Button button)
            {
                return;
            }

            // Validate inputs
            if (string.IsNullOrWhiteSpace(elementId) || string.IsNullOrWhiteSpace(definitionId) || valueTextBox == null)
            {
                _logger?.LogWarning("Invalid parameters for property update");
                MessageBox.Show("Invalid update parameters", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                SetPanelEnabledState(false);

                // Show updating indicator
                Dispatcher.Invoke(() =>
                {
                    button.Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Children =
                        {
                            new ProgressBar
                            {
                                Width = 16,
                                Height = 16,
                                IsIndeterminate = true
                            },
                            new TextBlock
                            {
                                Text = " Updating...",
                                Margin = new Thickness(5, 0, 0, 0),
                                FontSize = 10
                            }
                        }
                    };
                    button.IsEnabled = false;
                });

                string token = _tokenGetter();
                var graphQLClient = new GraphQLService(token, _cache);
                var properties = new List<PropertyUpdate>
                {
                    new PropertyUpdate
                    {
                        DefinitionId = definitionId,
                        Value = valueTextBox.Text
                    }
                };

                var updatedElements = await graphQLClient.UpdateExtensionPropertiesOnElements(new List<string> { elementId }, _elementDesignId, properties);

                if (updatedElements?.Data != null)
                {
                    _logger?.LogInformation("Property updated successfully for element {ElementId}", elementId);

                    // Show success feedback
                    Dispatcher.Invoke(() =>
                    {
                        var toolTip = new ToolTip
                        {
                            Content = "Property updated successfully!",
                            Background = Brushes.Green,
                            Foreground = Brushes.White
                        };
                        button.ToolTip = toolTip;
                        toolTip.IsOpen = true;

                        Task.Delay(2000).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                toolTip.IsOpen = false;
                                RestoreButtonContent(button);
                            });
                        });
                    });
                }
                else
                {
                    throw new Exception("Update response was null or invalid");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update property for element {ElementId}", elementId);
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to update property: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    RestoreButtonContent(button);
                });
            }
            finally
            {
                SetPanelEnabledState(true);

                Dispatcher.Invoke(() =>
                {
                    button.IsEnabled = true;
                });
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Updates extension properties for a single element in the cloud
        /// </summary>
        /// <param name="elementId">The unique identifier of the element</param>
        /// <param name="definitionId">The property definition ID</param>
        /// <param name="newValue">The new value to set</param>
        /// <returns>True if update succeeded, false otherwise</returns>
        private async Task<bool> UpdateSinglePropertyAsync(string elementId, string definitionId, string newValue)
        {
            try
            {
                string token = _tokenGetter();
                var graphQLClient = new GraphQLService(token, _cache);

                var properties = new List<PropertyUpdate>
                {
                    new PropertyUpdate
                    {
                        DefinitionId = definitionId,
                        Value = newValue
                    }
                };
                var updatedElements = await graphQLClient.UpdateExtensionPropertiesOnElements(new List<string> { elementId }, _elementDesignId, properties);

                return updatedElements?.Data != null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating property for element {ElementId}", elementId);
                return false;
            }
        }

        /// <summary>
        /// Refreshes the properties display
        /// </summary>
        private async Task RefreshPropertiesAsync()
        {
            if (string.IsNullOrEmpty(_selectedElementName) ||
                string.IsNullOrEmpty(_documentId) ||
                string.IsNullOrEmpty(_hubId) ||
                string.IsNullOrEmpty(_projectId) ||
                string.IsNullOrEmpty(_modelUrn))
            {
                _logger?.LogWarning("Cannot refresh: Missing required context information");
                return;
            }

            try
            {
                await RefreshPropertiesUI(_selectedElementName, _documentId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing properties");
                HandleError($"Failed to refresh properties: {ex.Message}");
            }
        }

        /// <summary>
        /// Refreshes the properties UI
        /// </summary>
        private async Task RefreshPropertiesUI(string selectedElementName, string externalId)
        {
            if (string.IsNullOrEmpty(externalId) ||
                string.IsNullOrEmpty(_hubId) ||
                string.IsNullOrEmpty(_projectId) ||
                string.IsNullOrEmpty(_modelUrn))
            {
                _logger?.LogWarning("Cannot refresh UI: Missing required parameters");
                return;
            }

            DisableButtons(_panel);
            try
            {
                FilterAndShowResults(_hubId, _projectId, _modelUrn, selectedElementName, externalId);
            }
            finally
            {
                EnableButtons(_panel);
            }
        }

        /// <summary>
        /// Displays an error message in the panel
        /// </summary>
        private void HandleError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ClearPanelContent();
                var errorText = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.Red,
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap
                };
                _contentPanel.Children.Add(errorText);
            });
        }

        /// <summary>
        /// Updates the UI with element properties
        /// </summary>
        private void UpdatePropertiesUI(List<(ExtendedElement element, bool isType)> elements)
        {
            if (elements == null)
            {
                _logger?.LogWarning("UpdatePropertiesUI called with null elements");
                return;
            }

            try
            {
                Dispatcher.Invoke(() =>
                {
                    ClearPanelContent();

                    if (!elements.Any())
                    {
                        _contentPanel.Children.Add(new TextBlock
                        {
                            Text = "No data found for the selected element.",
                            FontSize = 12,
                            Foreground = Brushes.Gray,
                            Margin = new Thickness(5),
                            HorizontalAlignment = HorizontalAlignment.Center
                        });
                        return;
                    }

                    foreach (var (element, isType) in elements)
                    {
                        if (element == null)
                        {
                            continue;
                        }

                        var elementCard = CreateElementCard(element, isType);
                        _contentPanel.Children.Add(elementCard);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating properties UI");
                Dispatcher.Invoke(() =>
                {
                    _contentPanel.Children.Add(new TextBlock
                    {
                        Text = $"Error loading properties: {ex.Message}",
                        Foreground = Brushes.Red,
                        Margin = new Thickness(10),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap
                    });
                });
            }
        }

        /// <summary>
        /// Creates a UI card for an element with its properties
        /// </summary>
        private Border CreateElementCard(ExtendedElement element, bool isType)
        {
            var elementCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(5),
                Padding = new Thickness(8)
            };

            var cardContent = new StackPanel();

            // Header
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            headerPanel.Children.Add(new TextBlock
            {
                Text = isType ? "Cloud Attributes Type" : "Cloud Attributes",
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 114, 197)),
                Margin = new Thickness(2)
            });

            if (!string.IsNullOrEmpty(element.Name))
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = $": {element.Name}",
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 5, 5),
                    FontStyle = FontStyles.Italic
                });
            }

            cardContent.Children.Add(headerPanel);

            // Check for properties
            if (element.Properties?.Results == null || !element.Properties.Results.Any())
            {
                cardContent.Children.Add(new TextBlock
                {
                    Text = "No properties available",
                    FontSize = 13,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(10, 5, 10, 10),
                    FontStyle = FontStyles.Italic
                });
                elementCard.Child = cardContent;
                return elementCard;
            }

            var componentElement = element.components?.results?.FirstOrDefault(c => !string.IsNullOrEmpty(c.element?.Id));
            string elementIdToUpdate = componentElement?.element?.Id ?? element.Id;

            if (string.IsNullOrEmpty(elementIdToUpdate))
            {
                _logger?.LogWarning("No valid element ID found for element {ElementName}", element.Name);
                cardContent.Children.Add(new TextBlock
                {
                    Text = "Error: No valid element ID",
                    FontSize = 13,
                    Foreground = Brushes.Red,
                    Margin = new Thickness(10, 5, 10, 10)
                });
                elementCard.Child = cardContent;
                return elementCard;
            }

            var propertiesGrid = new Grid
            {
                Margin = new Thickness(0)
            };
            propertiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            propertiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            propertiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int rowIndex = 0;
            foreach (var property in element.Properties.Results)
            {
                if (property == null || property.Definition == null)
                {
                    continue;
                }

                propertiesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var propertyLabel = new TextBlock
                {
                    Text = property.Name ?? "Unnamed",
                    FontSize = 10,
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetRow(propertyLabel, rowIndex);
                Grid.SetColumn(propertyLabel, 0);

                // Property value textbox
                var propertyValue = new TextBox
                {
                    Text = property.Value?.ToString() ?? string.Empty,
                    FontSize = 10,
                    Margin = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.LightGray,
                    Padding = new Thickness(3)
                };
                Grid.SetRow(propertyValue, rowIndex);
                Grid.SetColumn(propertyValue, 1);

                // Update button
                var updateButton = CreateUpdateButton(elementIdToUpdate, property.Definition.Id, propertyValue);
                Grid.SetRow(updateButton, rowIndex);
                Grid.SetColumn(updateButton, 2);

                updateButton.Tag = new PropertyUpdateContext
                {
                    ElementId = elementIdToUpdate,
                    DefinitionId = property.Definition.Id
                };

                propertiesGrid.Children.Add(propertyLabel);
                propertiesGrid.Children.Add(propertyValue);
                propertiesGrid.Children.Add(updateButton);

                rowIndex++;
            }

            cardContent.Children.Add(propertiesGrid);
            elementCard.Child = cardContent;
            return elementCard;
        }

        /// <summary>
        /// Creates an update button for a property
        /// </summary>
        private Button CreateUpdateButton(string elementId, string definitionId, TextBox valueTextBox)
        {
            var button = new Button
            {
                Margin = new Thickness(5),
                Padding = new Thickness(0),
                MinWidth = 30,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                ToolTip = "Update this property",
                Cursor = Cursors.Hand
            };

            if (Application.Current.Resources.Contains("MaterialDesignFlatButton"))
            {
                button.Style = (Style)Application.Current.Resources["MaterialDesignFlatButton"];
            }

            RestoreButtonContent(button);

            button.MouseEnter += (s, e) =>
            {
                button.Background = new SolidColorBrush(Color.FromArgb(20, 0, 114, 197));
            };
            button.MouseLeave += (s, e) =>
            {
                button.Background = Brushes.Transparent;
            };

            // Click handler
            button.Click += (sender, e) => OnUpdateButtonClick(
                sender, e,
                elementId,
                definitionId,
                valueTextBox
            );

            return button;
        }

        /// <summary>
        /// Restores the default icon content for an update button
        /// </summary>
        private void RestoreButtonContent(Button button)
        {
            try
            {
                button.Content = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/revit_aec_dm_extensibility_sample;component/Resources/cloud-upload.png")),
                    Width = 16,
                    Height = 16
                };
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load button icon, using text fallback");
                button.Content = "↑";
            }
        }

        /// <summary>
        /// Disables all buttons within a panel
        /// </summary>
        private void DisableButtons(Panel parentPanel)
        {
            if (parentPanel == null)
            {
                return;
            }

            foreach (var child in parentPanel.Children)
            {
                if (child is Button button)
                {
                    button.IsEnabled = false;
                }
                else if (child is Panel panel)
                {
                    DisableButtons(panel);
                }
            }
        }

        /// <summary>
        /// Enables all buttons within a panel
        /// </summary>
        private void EnableButtons(Panel parentPanel)
        {
            if (parentPanel == null)
            {
                return;
            }

            foreach (var child in parentPanel.Children)
            {
                if (child is Button button)
                {
                    button.IsEnabled = true;
                }
                else if (child is Panel panel)
                {
                    EnableButtons(panel);
                }
            }
        }

        /// <summary>
        /// Sets the enabled state of the entire panel
        /// </summary>
        private void SetPanelEnabledState(bool isEnabled)
        {
            Dispatcher.Invoke(() =>
            {
                SetChildrenEnabledState(_panel, isEnabled);

                if (_refreshButton != null)
                {
                    _refreshButton.IsEnabled = isEnabled;
                }
            });
        }

        /// <summary>
        /// Recursively sets the enabled state of all controls in a visual tree
        /// </summary>
        private void SetChildrenEnabledState(DependencyObject parent, bool isEnabled)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is Control control)
                {
                    control.IsEnabled = isEnabled;
                }

                SetChildrenEnabledState(child, isEnabled);
            }
        }

        #endregion
    }
}