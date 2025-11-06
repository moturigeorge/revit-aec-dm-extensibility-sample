using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using revit_aec_dm_extensibility_sample.Models;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;

namespace revit_aec_dm_extensibility_sample.Services
{
    public class GraphQLService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryStorage _cache;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new();

        public GraphQLService(string authToken, IMemoryStorage cache = null)
        {

            _httpClient = new HttpClient(new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 20
            })
            {
                BaseAddress = new Uri("https://developer.api.autodesk.com/aec/private/graphql"),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + authToken);
            _cache = cache ?? new MemoryStorage();
        }

        private async Task<T> CachedRequest<T>(string cacheKey, Func<Task<T>> fetchFunc, TimeSpan? cacheDuration = null)
        {
            var keyLock = _keyLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

            await keyLock.WaitAsync();
            try
            {
                if (_cache.TryGet<T>(cacheKey, out var cachedValue))
                {
                    return cachedValue;
                }

                var result = await fetchFunc();
                _cache.Store(cacheKey, result, cacheDuration ?? TimeSpan.FromMinutes(5));
                return result;
            }
            finally
            {
                keyLock.Release();
                _keyLocks.TryRemove(cacheKey, out _);
            }
        }

        public async Task<Hub> GetHubByIdAsync(string hubId, bool forceRefresh = false)
        {
            var cacheKey = $"hub_{hubId}";
            if (forceRefresh) _cache.Remove(cacheKey);

            return await CachedRequest(cacheKey, async () =>
            {
                var allHubs = await GetHubsAsync(forceRefresh);
                return allHubs.FirstOrDefault(e => e.AlternativeIdentifiers?.DataManagementAPIHubId == hubId);
            });
        }

        public async Task<Project> GetProjectByIdAsync(string hubId, string projectId, bool forceRefresh = false)
        {
            var cacheKey = $"project_{hubId}_{projectId}";
            if (forceRefresh) _cache.Remove(cacheKey);

            return await CachedRequest(cacheKey, async () =>
            {
                var projects = await GetProjectsAsync(hubId, forceRefresh: forceRefresh);
                return projects.FirstOrDefault(e => e.AlternativeIdentifiers?.DataManagementAPIProjectId == projectId);
            });
        }

        public async Task<List<ElementGroup>> GetElementGroupsByProjectAsync(string projectId, string modelUrn)
        {
            var cacheKey = $"elementGroups_{projectId}_{modelUrn}";
            return await CachedRequest(cacheKey, async () =>
            {
                var query = new
                {
                    query = @"
                        query GetElementGroupsByProject($projectId: ID!, $filter: ElementGroupFilterInput!) {
                          elementGroupsByProject(projectId: $projectId, filter: $filter) {
                            pagination {
                              cursor
                            }
                            results {
                              name
                              id
                              alternativeIdentifiers {
                                fileUrn
                                fileVersionUrn
                              }
                              components {
                                results {
                                  ... on ExtensionComponent {
                                    elementGroup {
                                      id
                                    }
                                  }
                                }
                              }
                            }
                          }
                        }",
                    variables = new
                    {
                        projectId,
                        filter = new { fileUrn = modelUrn }
                    }
                };
                var response = await ExecuteGraphQLQuery<ElementGroupsResponse>(query);
                return response?.Data?.ElementGroups?.Results ?? new List<ElementGroup>();
            });
        }

        //public async Task<(string elementGroupId, List<(string elementId, bool isType)>)> GetAssociatedElementIdsOptimizedAsync(string elementGroupId, string categoryFilter, string externalId)
        //{
        //    if (string.IsNullOrEmpty(externalId))
        //        return (elementGroupId, new List<(string, bool)>());

        //    var (elementsTask, typeElementsTask) = (GetElementsByCategoryAsync(elementGroupId, categoryFilter), GetElementsByCategoryAsync(elementGroupId, externalId, true));

        //    await Task.WhenAll(elementsTask, typeElementsTask);
        //    var (elements, _) = await elementsTask;
        //    var (typeElements, _) = await typeElementsTask;

        //    var apiElement = elements.FirstOrDefault(e => e.AlternativeIdentifiers?.ExternalElementId == externalId);
        //    if (apiElement == null)
        //    {
        //        return (elementGroupId, typeElements.Select(e => (e.Id, true)).ToList());
        //    }

        //    var results = new List<(string, bool)> { (apiElement.Id, false) };

        //    var typeExternalId = await Task.Run(() =>
        //        apiElement.References?.Results?
        //            .FirstOrDefault(r => r.Name == "Type")?
        //            .Value?.Properties?.Results?
        //            .FirstOrDefault(p => p.Name == "External ID")?
        //            .DisplayValue
        //    );

        //    if (!string.IsNullOrEmpty(typeExternalId))
        //    {
        //        results.AddRange(typeElements.Select(e => (e.Id, true)));
        //    }

        //    return (elementGroupId, results);
        //}

        //public async Task<List<ExtendedElement>> GetExtensionPropertiesBatchAsync(string elementGroupId, List<string> elementIds)
        //{
        //    if (elementIds == null || !elementIds.Any())
        //        return new List<ExtendedElement>();

        //    var query = new
        //    {
        //        query = @"
        //            query associatedElementsByElements($elementIds: [ID!]!) {
        //              associatedElementsByElements(elementIds: $elementIds) {
        //                results {
        //                  id
        //                  name
        //                  properties {
        //                    results {
        //                      name
        //                      value
        //                    }
        //                  }
        //                }
        //              }
        //            }",
        //        variables = new { elementIds }
        //    };

        //    var response = await ExecuteGraphQLQuery<ExtensionPropertiesResponse>(query);
        //    return response?.Data?.AssociatedElements?.Results ?? new List<ExtendedElement>();
        //}

        private async Task<T> ExecuteGraphQLQuery<T>(object query)
        {
            try
            {
                var jsonPayload = JsonConvert.SerializeObject(query);
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync("", content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<T>(jsonResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GraphQL query failed: {ex.Message}");
                return default;
            }
        }


        public async Task<List<Hub>> GetHubsAsync(bool forceRefresh = false)
        {
            try
            {
                const string cacheKey = "hubs_list";
                if (forceRefresh) _cache.Remove(cacheKey);

                return await CachedRequest(cacheKey, async () =>
                {
                    var graphQLQuery = new
                    {
                        query = @"
                        query GetHubs {
                          hubs {
                            pagination {
                              cursor
                            }
                            results {
                              id
                              name                              
                              alternativeIdentifiers {
                                 dataManagementAPIHubId
                              }
                            }
                          }
                        }"
                    };

                    var jsonPayload = JsonConvert.SerializeObject(graphQLQuery);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await _httpClient.PostAsync("", content);
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                    var result = JsonConvert.DeserializeObject<HubsResponse>(jsonResponse);

                    return result?.Data?.Hubs?.Results ?? new List<Hub>();
                });
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"HTTP request failed: {httpEx.Message}");
                return new List<Hub>();
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Console.WriteLine($"JSON parsing failed: {jsonEx.Message}");
                return new List<Hub>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return new List<Hub>();
            }
        }


        public async Task<List<Project>> GetProjectsAsync(string hubId, string projectNameFilter = null, bool forceRefresh = false)
        {
            try
            {
                var cacheKey = $"projects_{hubId}_{projectNameFilter}";
                if (forceRefresh) _cache.Remove(cacheKey);

                return await CachedRequest(cacheKey, async () =>
                {
                    var graphQLQuery = new
                    {
                        query = @"
                            query GetProjects($HubId: ID!, $filter: ProjectFilterInput) {
                              projects(hubId: $HubId, filter: $filter) {
                                pagination {
                                  cursor
                                }
                                results {
                                  id
                                  name
                                  alternativeIdentifiers {
                                     dataManagementAPIProjectId
                                  } 
                                }
                              }
                            }",
                        variables = new
                        {
                            HubId = hubId,
                            filter = !string.IsNullOrEmpty(projectNameFilter)
                                ? new { name = projectNameFilter }
                                : null
                        }
                    };

                    var jsonPayload = JsonConvert.SerializeObject(graphQLQuery);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await _httpClient.PostAsync("", content);
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    response.EnsureSuccessStatusCode();
                    var result = JsonConvert.DeserializeObject<ProjectsResponse>(jsonResponse);
                    return result?.Data?.Projects?.Results ?? new List<Project>();
                });
            }
            catch (HttpRequestException httpEx)
            {
                Console.WriteLine($"HTTP request failed: {httpEx.Message}");
                return new List<Project>();
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Console.WriteLine($"JSON parsing failed: {jsonEx.Message}");
                return new List<Project>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                return new List<Project>();
            }
        }


        public async Task<(List<Models.Element> Elements, string NextCursor)> GetElementsByCategoryAsync(string elementGroupId, string filter, bool isExternalIdFilter = false, string paginationCursor = null)
        {
            try
            {
                var graphQLQuery = new
                {
                    query = @"
                    query GetElementsByElementGroup($elementGroupId: ID! $filter: ElementFilterInput $pagination: PaginationInput) {
                      elementsByElementGroup(elementGroupId: $elementGroupId filter: $filter pagination: $pagination) {
                        pagination {
                          cursor
                        }
                        results {
                          id
                          name
                          alternativeIdentifiers {
                            externalElementId
                            revitElementId
                          }
                          references {
                            results {
                              name
                              displayValue
                              value {
                                name
                                properties {
                                  results {
                                    name
                                    displayValue
                                  }
                                }
                              }
                            }
                          }
                          properties {
                            results {
                              name
                              value
                              definition {
                                units {
                                  name
                                }
                              }
                            }
                          }
                        }
                      }
                    }",
                    variables = new
                    {
                        elementGroupId = elementGroupId,
                        filter = new
                        {
                            query = isExternalIdFilter ? $"\'property.name.External ID\'=={filter}" : $"property.name.category=={filter}"
                        },
                        pagination = new
                        {
                            //cursor = paginationCursor ?? "",
                            limit = 100
                        }
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(graphQLQuery);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync("", content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                var result = JsonConvert.DeserializeObject<GetElementsResponse>(jsonResponse);

                return (
                    result?.Data?.Elements?.Results ?? new List<Models.Element>(),
                    result?.Data?.Elements?.Pagination?.Cursor
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return (new List<Models.Element>(), null);
            }
        }

        public async Task<(string elementGroupId, List<(string elementId, bool isType)>)> GetAssociatedElementIdsAsync(string elementGroupId, string categoryFilter, string externalId)
        {
            var results = new List<(string, bool)>();
            try
            {
                var (elements, _) = await GetElementsByCategoryAsync(elementGroupId, categoryFilter);

                if (string.IsNullOrEmpty(externalId))
                {
                    Console.WriteLine("External ID not provided");
                    return (elementGroupId, results);
                }

                var apiElement = elements.FirstOrDefault(e => e.AlternativeIdentifiers?.ExternalElementId == externalId);

                if (apiElement == null)
                {
                    Console.WriteLine("Element not found in API results");
                    var (typeElements, _) = await GetElementsByCategoryAsync(elementGroupId, externalId, true);
                    results.AddRange(typeElements.Select(e => (e.Id, true)));
                    return (elementGroupId, results);
                }

                results.Add((apiElement.Id, false));
                string typeExternalId = null;
                var typeReference = apiElement.References?.Results?.FirstOrDefault(r => r.Name == "Type");

                if (typeReference != null)
                {
                    var externalIdProperty = typeReference.Value?.Properties?.Results?.FirstOrDefault(p => p.Name == "External ID");
                    typeExternalId = externalIdProperty?.DisplayValue;
                }

                if (!string.IsNullOrEmpty(typeExternalId))
                {
                    // Second call - get elements with matching Type External ID
                    var (associatedElements, _) = await GetElementsByCategoryAsync(elementGroupId, typeExternalId, true);
                    results.AddRange(associatedElements.Select(e => (e.Id, true)));
                }
                return (elementGroupId, results);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting associated elements: {ex.Message}");
                return (elementGroupId, results);
            }
        }


        public async Task<List<ExtendedElement>> GetExtensionPropertiesAsync(List<string> elementIds)
        {
            try
            {
                var graphQLQuery = new
                {
                    query = @"
                    query associatedElementsByElements($elementIds: [ID!]!) {
                      associatedElementsByElements(elementIds: $elementIds) {
                        results {
                          id
                          name
                          createdBy {
                            userName
                          }
                          lastModifiedBy {
                            userName
                          }
                          components {
                            results {
                              ... on ExtensionComponent {
                                element {
                                  id
                                }
                              }
                            }
                          }
                          references {
                            pagination {
                              cursor
                            }
                            results {
                              name
                              displayValue
                              value {
                                name
                                properties {
                                  results {
                                    name
                                    displayValue
                                  }
                                }
                              }
                            }
                          }
                          properties {
                            results {
                              name
                              value
                              definition {
                                id
                              }
                            }
                          }
                          elementGroup {
                            id
                            name
                          }
                        }
                      }
                    }",
                    variables = new
                    {
                        elementIds = elementIds
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(graphQLQuery);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync("", content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                var result = JsonConvert.DeserializeObject<ExtensionPropertiesResponse>(jsonResponse);

                return result?.Data?.AssociatedElements?.Results ?? new List<ExtendedElement>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new List<ExtendedElement>();
            }
        }


        public async Task<UpdateExtensionResponse> UpdateExtensionPropertiesOnElements(List<string> elementIds, string elementGroupId, List<PropertyUpdate> properties)
        {
            try
            {
                var minifiedQuery = "mutation UpdateExtensionPropertiesOnElements( $input : UpdateExtensionPropertiesInput! ){ updateExtensionPropertiesOnElements(input: $input ){ elements{ id name properties{ results{ name value } } lastModifiedBy{ userName } } } }";

                var graphQLQuery = new
                {
                    query = minifiedQuery,
                    variables = new
                    {
                        input = new
                        {
                            targets = new[]
                            {
                                new
                                {
                                    elementIds = elementIds,
                                    extensionGroupId = elementGroupId
                                }
                            },
                            properties = properties.Select(p => new
                            {
                                definitionId = p.DefinitionId,
                                value = p.Value
                            }).ToArray()
                        }
                    }
                };

                var jsonPayload = JsonConvert.SerializeObject(graphQLQuery);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync("", content);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                var result = JsonConvert.DeserializeObject<UpdateExtensionResponse>(jsonResponse);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return new UpdateExtensionResponse();
            }
        }


    }
}