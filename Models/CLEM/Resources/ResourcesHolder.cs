﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;  //enumerator
using Newtonsoft.Json;
using System.Runtime.Serialization;
using Models.Core;
using Models.CLEM.Activities;
using Models.CLEM.Groupings;
using System.ComponentModel.DataAnnotations;
using Models.Core.Attributes;
using APSIM.Shared.Utilities;
using Models.CLEM.Interfaces;
using System.Reflection;

namespace Models.CLEM.Resources
{
    ///<summary>
    /// Manger for all resources available to the model
    ///</summary> 
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(ZoneCLEM))]
    [ValidParent(ParentType = typeof(Market))]
    [Description("This holds all resource groups used in the CLEM simulation")]
    [Version(1, 0, 1, "")]
    [HelpUri(@"Content/Features/Resources/ResourcesHolder.htm")]
    public class ResourcesHolder: CLEMModel, IValidatableObject, IReportPricingChange
    {
        [JsonIgnore]
        private IEnumerable<IModel> ResourceGroupList;

        private void InitialiseResourceGroupList()
        {
            if(ResourceGroupList == null)
                ResourceGroupList = this.FindAllChildren<IModel>().Where(a => a.Enabled);
        }

        /// <summary>
        /// Finds a shared marketplace
        /// </summary>
        /// <returns>Market</returns>
        [JsonIgnore]
        public Market FoundMarket { get; private set; }

        /// <summary>
        /// Determines if a market has been located
        /// </summary>
        /// <returns>Whether a market has been found</returns>
        public bool MarketPresent { get { return !(FoundMarket is null); } }


        /// <summary>
        /// Finds a resource in the the resource holder
        /// </summary>
        /// <returns>The resource</returns>
        public T FindResource<T>() where T : ResourceBaseWithTransactions
        {
            return this.FindChild<T>();
        }

        /// <summary>
        /// Finds a resource in the the resource holder
        /// </summary>
        /// <param name="name">Name of the resource</param>
        /// <returns>The resource</returns>
        public T FindResource<T>(string name) where T : ResourceBaseWithTransactions
        {
            return this.FindChild<T>(name);
        }

        /// <summary>
        /// Finds a resource in the the resource holder
        /// </summary>
        /// <param name="type">The type of the resource</param>
        /// <returns>The resource</returns>
        public ResourceBaseWithTransactions FindResource(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            MethodInfo method = this.GetType().GetMethod("FindResource", new Type[] { } );
            MethodInfo generic = method.MakeGenericMethod(type);
            return generic.Invoke(this, null) as ResourceBaseWithTransactions;
        }

        /// <summary>
        /// Find a resource type from  type of resources and name of resource type component
        /// </summary>
        /// <typeparam name="T">Type of ResourceType to return</typeparam>
        /// <typeparam name="R">Type of Resource group containing resource type</typeparam>
        /// <param name="requestingModel">The model requesting this resource</param>
        /// <param name="resourceName">The name identifier NameOfResource.NameOfResourceType or simply NameOfResourceType</param>
        /// <param name="missingResourceAction">Action if resource group missing</param>
        /// <param name="missingResourceTypeAction">Action if resource type is missing</param>
        /// <returns>A resource type component</returns>
        public T FindResourceType<R, T>(IModel requestingModel, string resourceName, OnMissingResourceActionTypes missingResourceAction = OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes missingResourceTypeAction = OnMissingResourceActionTypes.Ignore) where T : IResourceType where R : ResourceBaseWithTransactions
        {
            string[] nameParts = new string[] { "", resourceName };
            if (nameParts.Last().Contains('.'))
            {
                nameParts = nameParts.Last().Split('.');
                if (nameParts.Length > 2)
                    throw new ApsimXException(requestingModel, $"Invalid resource name identifier for [{requestingModel.Name}], expecting 'ResourceName.ResourceTypeName' or 'ResourceTypeName'. Value provided [{resourceName}]");
            }

            // not sure it's quickets to find the resource then look at it's children
            // or look through all descendents for the type and name
            // if we find children then we use R as a double check

            bool searchForAllIresourceType = false;
            bool resGroupNameMatch = true;
            T resType = default(T);
            ResourceBaseWithTransactions resGroup = null;
            if (!typeof(R).IsSubclassOf(typeof(ResourceBaseWithTransactions)))
            {
                if (nameParts.First() == "")
                    searchForAllIresourceType = true;
                else
                {
                    // find resource by name
                    resGroup = FindChild<R>(nameParts.First());
                }
            }
            else
            {
                resGroup = (nameParts.First() != "") ? FindResource<R>(nameParts.First()) : FindResource<R>();
                if (resGroup == null && nameParts.First() != "")
                {
                    // no resource name match so try with just the type
                    resGroupNameMatch = false;
                    resGroup = FindResource<R>();
                }
            }

            if (searchForAllIresourceType)
                resType = FindAllDescendants<T>(nameParts.Last()).FirstOrDefault();
            else
            {
                if (resGroup != null)
                    resType = (resGroup as IModel).FindChild<T>(nameParts.Last());
            }

            string errorMsg;
            if (resGroup == null)
            {
                errorMsg = $"Unable to locate resource group [r={typeof(R).Name}] for [a={requestingModel.Name}]";

                switch (missingResourceAction)
                {
                    case OnMissingResourceActionTypes.ReportErrorAndStop:
                        throw new Exception(errorMsg);
                    case OnMissingResourceActionTypes.ReportWarning:
                        Warnings.CheckAndWrite(errorMsg, Summary, this);
                        break;
                    default:
                        break;
                }
                    return default(T);
            }
            else
            {
                if (!resGroupNameMatch)
                {
                    errorMsg = $"Unable to locate resource named [r={nameParts.First()}] for [a={requestingModel.Name}] but a [{typeof(R).Name}] resource was found and will be used.";
                    Warnings.CheckAndWrite(errorMsg, Summary, this);
                }
            }

            if (resType as IModel is null)
            {
                errorMsg = $"Unable to locate resource type [r={nameParts.Last()}] in [r={resGroup.Name}] for [a={requestingModel.Name}]";
                switch (missingResourceTypeAction)
                {
                    case OnMissingResourceActionTypes.ReportErrorAndStop:
                        throw new Exception(errorMsg);
                    case OnMissingResourceActionTypes.ReportWarning:
                        Warnings.CheckAndWrite(errorMsg, Summary, this);
                        break;
                    default:
                        break;
                }
            }
            return resType;
        }

        /// <summary>
        /// Find a resource type from details supplied from a ResourceRequest
        /// </summary>
        /// <typeparam name="T">Type of ResourceType to return</typeparam>
        /// <typeparam name="R">Type of Resource group containing resource type</typeparam>
        /// <param name="request">Resource request with all required information</param>
        /// <param name="missingResourceAction">Action if resource group missing</param>
        /// <param name="missingResourceTypeAction">Action if resource type is missing</param>
        /// <returns>A resource type component</returns>
        public T FindResourceType<R,T>(ResourceRequest request, OnMissingResourceActionTypes missingResourceAction = OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes missingResourceTypeAction = OnMissingResourceActionTypes.Ignore) where T : IResourceType where R : ResourceBaseWithTransactions
        {
            if (request.Resource is T)
                return (T)Convert.ChangeType(request.Resource, typeof(T));

            return FindResourceType<R,T>(request.ActivityModel, request.ResourceTypeName, missingResourceAction, missingResourceTypeAction);
        }










        /// <summary>
        /// Determines whether resource items of the specified group type exist 
        /// </summary>
        /// <returns></returns>
        public bool ResourceItemsExist<T>() 
        {
            var resourceGroup = this.FindAllChildren<T>().FirstOrDefault() as IModel;
            if (resourceGroup != null)
                return resourceGroup.Children.Where(a => a.GetType() != typeof(Memo)).Any();
            return false;
        }

        /// <summary>
        /// Determines whether resource group of the specified type exist 
        /// </summary>
        /// <returns></returns>
        public bool ResourceGroupExists<T>()
        {
            return this.FindAllChildren<T>().Any();
        }

        /// <summary>
        /// Returns resource group of the specified type if enabled (for use in UI)
        /// </summary>
        /// <returns></returns>
        public T FindResourceGroup<T>()
        {
            return this.FindAllChildren<T>().FirstOrDefault(a => (a as IModel).Enabled);
        }

        /// <summary>
        /// Get resource by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public object GetResourceGroupByName(string name)
        {
            InitialiseResourceGroupList();
            return ResourceGroupList.FirstOrDefault(x => x.Name == name); 
        }

        /// <summary>
        /// Get resource by type
        /// </summary>
        /// <param name="resourceGroupType">Type of resource group</param>
        /// <returns></returns>
        public object GetResourceGroupByType(Type resourceGroupType)
        {
            InitialiseResourceGroupList();
            return ResourceGroupList.FirstOrDefault(a => a.GetType() == resourceGroupType);
        }

        /// <summary>
        /// Retrieve a ResourceType from a ResourceGroup based on a request item including filter and sort options
        /// </summary>
        /// <param name="request">A resource request item</param>
        /// <param name="missingResourceAction">Action to take if requested resource group not found</param>
        /// <param name="missingResourceTypeAction">Action to take if requested resource type not found</param>
        /// <returns>A reference to the item of type Model</returns>
        public IModel GetResourceItem(ResourceRequest request, OnMissingResourceActionTypes missingResourceAction, OnMissingResourceActionTypes missingResourceTypeAction)
        {
            if (request.FilterDetails != null)
            {
                if (request.ResourceType == null)
                {
                    string errorMsg = String.Format("Resource type must be supplied in resource request from [a={0}]", request.ActivityModel.Name);
                    throw new Exception(errorMsg);
                }

                var resourceGroup = this.GetResourceGroupByType(request.ResourceType);
                if(resourceGroup== null)
                {
                    string errorMsg = String.Format("Unable to locate resources of type [r{0}] for [a={1}]", request.ResourceType, request.ActivityModel.Name);
                    switch (missingResourceAction)
                    {
                        case OnMissingResourceActionTypes.ReportErrorAndStop:
                            throw new Exception(errorMsg);
                        case OnMissingResourceActionTypes.ReportWarning:
                            Summary.WriteWarning(request.ActivityModel, errorMsg);
                            break;
                        default:
                            break;
                    }
                    return null;
                }

                // get list of children matching the conditions in filter
                // and return the lowest item that has enough time available
                object resourceGroupObject = resourceGroup as object;
                switch (resourceGroupObject.GetType().ToString())
                {
                    case "Models.CLEM.Resources.Labour":
                        // get matching labour types
                        // use activity uid to ensure unique for this request
                        List<LabourType> items = (resourceGroup as Labour).Items;
                        items = items.Filter(request.FilterDetails.FirstOrDefault() as Model)
                            .Where(a => a.LastActivityRequestID != request.ActivityID)
                            .ToList();

                        if (items.Where(a => a.Amount >= request.Required).Count()>0)
                            // get labour least available but with the amount needed
                            return items.Where(a => a.Amount >= request.Required).OrderByDescending(a => a.Amount).FirstOrDefault();
                        else
                            // get labour with most available but with less than the amount needed
                            return items.OrderByDescending(a => a.Amount).FirstOrDefault();
                    default:
                        string errorMsg = "Resource cannot be filtered. Filtering not implemented for [r=" + resourceGroupObject.GetType().ToString() + "] from activity [a=" + request.ActivityModel.Name + "]";
                        Summary.WriteWarning(request.ActivityModel, errorMsg);
                        throw new Exception(errorMsg);
                }
            }
            else
            {
                // check style of ResourceTypeName used
                // this is either "Group.Type" from dropdown menus or "Type" only. 
                if (request.ResourceTypeName.Contains("."))
                    return GetResourceItem(request.ActivityModel, request.ResourceTypeName, missingResourceAction, missingResourceTypeAction);
                else
                    return GetResourceItem(request.ActivityModel, request.ResourceType, request.ResourceTypeName, missingResourceAction, missingResourceTypeAction);
            }
        }

        /// <summary>
        /// Retrieve a ResourceType from a ResourceGroup with specified names
        /// </summary>
        /// <param name="requestingModel">name of model requesting resource</param>
        /// <param name="resourceGroupType">Type of the resource group</param>
        /// <param name="resourceItemName">Name of the resource item</param>
        /// <param name="missingResourceAction">Action to take if requested resource group not found</param>
        /// <param name="missingResourceTypeAction">Action to take if requested resource type not found</param>
        /// <returns>A reference to the item of type object</returns>
        public IModel GetResourceItem(Model requestingModel, Type resourceGroupType, string resourceItemName, OnMissingResourceActionTypes missingResourceAction, OnMissingResourceActionTypes missingResourceTypeAction)
        {
            // locate specified resource
            Model resourceGroup = this.FindAllChildren().Where(c => resourceGroupType.IsAssignableFrom(c.GetType())).FirstOrDefault() as Model;
            if (resourceGroup != null)
            {
                IModel resource = resourceGroup.Children.Where(a => a.Name == resourceItemName & a.Enabled).FirstOrDefault();
                if (resource == null)
                {
                    string errorMsg = String.Format("Unable to locate resources item [r={0}] in resources [r={1}] for [a={2}]", resourceItemName, resourceGroupType.ToString(), requestingModel.Name);
                    switch (missingResourceTypeAction)
                    {
                        case OnMissingResourceActionTypes.ReportErrorAndStop:
                            throw new Exception(errorMsg);
                        case OnMissingResourceActionTypes.ReportWarning:
                            Summary.WriteWarning(requestingModel, errorMsg);
                            break;
                        default:
                            break;
                    }
                    return null;
                }
                return resource;
            }
            else
            {
                string errorMsg = String.Format("Unable to locate resources of type [r={0}] for [a={1}]", resourceGroupType.ToString(), requestingModel.Name);
                switch (missingResourceAction)
                {
                    case OnMissingResourceActionTypes.ReportErrorAndStop:
                        throw new Exception(errorMsg);
                    case OnMissingResourceActionTypes.ReportWarning:
                        Summary.WriteWarning(requestingModel, errorMsg);
                        break;
                    default:
                        break;
                }
                return null;
            }
        }

        /// <summary>
        /// Retrieve a ResourceType from a ResourceGroup with specified names
        /// </summary>
        /// <param name="requestingModel">name of model requesting resource</param>
        /// <param name="resourceGroupAndItem">Period separated list of resource group and type</param>
        /// <param name="missingResourceAction">Action to take if requested resource group not found</param>
        /// <param name="missingResourceTypeAction">Action to take if requested resource type not found</param>
        /// <returns>A reference to the item of type object</returns>
        public IModel GetResourceItem(Model requestingModel, string resourceGroupAndItem, OnMissingResourceActionTypes missingResourceAction, OnMissingResourceActionTypes missingResourceTypeAction)
        {
            if(resourceGroupAndItem == null)
                resourceGroupAndItem = " . ";

            // locate specified resource
            string[] names = resourceGroupAndItem.Split('.');
            if(names.Count()!=2)
            {
                string errorMsg = String.Format("Invalid resource group and type string for [{0}], expecting 'ResourceName.ResourceTypeName'. Value provided [{1}] ", requestingModel.Name, resourceGroupAndItem);
                throw new Exception(errorMsg);
            }

            if (this.GetResourceGroupByName(names[0]) is Model resourceGroup)
            {
                IModel resource = resourceGroup.Children.Where(a => a.Name == names[1] & a.Enabled).FirstOrDefault();
                if (resource == null)
                {
                    string errorMsg = String.Format("Unable to locate resources item [r={0}] in resources [r={1}] for [a={2}]", names[1], names[0], requestingModel.Name);
                    switch (missingResourceTypeAction)
                    {
                        case OnMissingResourceActionTypes.ReportErrorAndStop:
                            throw new Exception(errorMsg);
                        case OnMissingResourceActionTypes.ReportWarning:
                            Summary.WriteWarning(requestingModel, errorMsg);
                            break;
                        default:
                            break;
                    }
                    return null;
                }
                return resource;
            }
            else
            {
                string errorMsg = String.Format("Unable to locate resources of type [r={0}] for [a={1}]", names[0], requestingModel.Name);
                switch (missingResourceAction)
                {
                    case OnMissingResourceActionTypes.ReportErrorAndStop:
                        throw new Exception(errorMsg);
                    case OnMissingResourceActionTypes.ReportWarning:
                        Summary.WriteWarning(requestingModel, errorMsg);
                        break;
                    default:
                        break;
                }
                return null;
            }
        }

        /// <summary>
        /// Returns the link to the matching resource in the market place if found or creates a new clone copy for future transactions
        /// This allows this action to be performed once to store the link rather than at every transaction
        /// This functionality allows resources not in the market at the start of the simulation to be traded.
        /// </summary>
        /// <param name="resourceType">The resource type to trade</param>
        /// <returns>Whether the search was successful</returns>
        public IResourceWithTransactionType LinkToMarketResourceType(CLEMResourceTypeBase resourceType)
        {
            if (!(this.Parent is Market))
                throw new ApsimXException(this, $"Logic error in code. Trying to link a resource type [r={resourceType.Name}] from the market with the same market./nThis is a coding issue. Please contact the developers");

            // find parent group type
            ResourceBaseWithTransactions parent = (resourceType as Model).Parent as ResourceBaseWithTransactions;
            if (!(GetResourceGroupByType(parent.GetType()) is ResourceBaseWithTransactions resourceGroupInMarket))
            {
                // add warning the market is not currently trading in this resource
                string zoneName = FindAncestor<Zone>().Name;
                string warn = $"[{zoneName}] is currently not accepting resources of type [r={parent.GetType().Name}]\r\nOnly resources groups provided in the [r=ResourceHolder] in the simulation tree will be traded.";
                Warnings.CheckAndWrite(warn, Summary, this);
                return null;
            }

            // TODO: do some group checks. land units, currency

            // TODO: if market and looking for finance only return or create "Bank"

            // find resource type in group
            object resType = resourceGroupInMarket.FindChild<IResourceWithTransactionType>(resourceType.Name);

            // clone resource: too many problems with linked events to clone these objects and setup again
            // it will be the responsibility of the user to ensure the resources and details are in the market
            if (resType is null)
            {
                // add warning the market does not have the resource
                string warn = $"The resource [r={resourceType.Parent.Name}.{resourceType.Name}] does not exist in [m={this.Parent.Name}].\r\nAdd resource and associated components to the market to permit trading.";
                Warnings.CheckAndWrite(warn, Summary, this);
                return null;
            }

            // TODO: create a clone of the resource and put it in the market

            return resType as IResourceWithTransactionType;
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            // if this isn't a marketplace try find a shared market
            if(!(this.Parent is Market))
            {
                IModel parentSim = FindAncestor<Simulation>();
                FoundMarket = parentSim.FindAllChildren<Market>().FirstOrDefault();
            }
            else
                FoundMarket = this.Parent as Market;

            // link to price change in all descendents
            foreach (IReportPricingChange childModel in this.FindAllDescendants<IReportPricingChange>())
                childModel.PriceChangeOccurred += Resource_PricingChangeOccurred;

            InitialiseResourceGroupList();
        }

        /// <summary>
        /// Overrides the base class method to allow for clean up
        /// </summary>
        [EventSubscribe("Completed")]
        private void OnSimulationCompleted(object sender, EventArgs e)
        {
            foreach (IReportPricingChange childModel in this.FindAllDescendants<IReportPricingChange>())
                childModel.PriceChangeOccurred -= Resource_PricingChangeOccurred;
        }

        /// <summary>
        /// Performs the transmutation of resources into a required resource
        /// </summary>
        /// <param name="requests">The shortfall requests to try and transmutate</param>
        /// <param name="queryOnly">A switch to detemrine if this is a query where no resources are taken</param>
        public void TransmutateShortfall(IEnumerable<ResourceRequest> requests, bool queryOnly = true)
        {
            // Search through all limited resources and determine if transmutation available
            foreach (ResourceRequest request in requests.Where(a => a.Required > a.Available))
            {
                // Check if transmutation would be successful 
                if (request.AllowTransmutation && (queryOnly || request.TransmutationPossible))
                {
                    // get resource type if not already provided from request
                    if (!(request.Resource is IResourceType resourceTypeInShortfall))
                    {
                        if (request.ResourceTypeName.Contains('.'))
                        {
                            resourceTypeInShortfall = this.FindResourceType<ResourceBaseWithTransactions,IResourceType>(request.ActivityModel, request.ResourceTypeName, OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore) as IResourceType;
                        }
                        else
                        {
                            var resourceGroup = FindResource(request.ResourceType);
                            if (resourceGroup != null)
                                resourceTypeInShortfall = this.FindResourceType<ResourceBaseWithTransactions,IResourceType>(request.ActivityModel, $"{resourceGroup.Name}.{request.ResourceTypeName}", OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore) as IResourceType;
                            else
                                resourceTypeInShortfall = null;
                        }
                    }

                    if (resourceTypeInShortfall != null)
                    {
                        if (queryOnly)
                            // clear any transmutations before checking
                            request.SuccessfulTransmutation = null;

                        // get all transmutations if query only otherwise only successful transmutations previously checked
                        var transmutationsAvailable = (resourceTypeInShortfall as IModel).FindAllChildren<Transmutation>().Where(a => (queryOnly || (a == request.SuccessfulTransmutation)));
                        
                        foreach (Transmutation transmutation in transmutationsAvailable)
                        {
                            var transmutesAvailable = transmutation.FindAllChildren<ITransmute>();

                            // calculate the maximum amount of shortfall needed based on the transmute styles of all children
                            double packetsNeeded = transmutesAvailable.Select(a => a.ShortfallPackets(request.Required - request.Available)).Max();

                            bool allTransmutesSucceeed = true;
                            foreach (ITransmute transmute in transmutesAvailable)
                            {
                                if (transmute.TransmuteResourceType != null)
                                {
                                    // create new request for this transmutation cost
                                    ResourceRequest transRequest = new ResourceRequest
                                    {
                                        Resource = transmute.TransmuteResourceType,
                                        Required = packetsNeeded, // provide the amount of shortfall resource needed
                                        RelatesToResource = request.ResourceTypeName,
                                        ResourceType = transmute.ResourceGroup.GetType(),
                                        ActivityModel = request.ActivityModel,
                                        Category = transmutation.TransactionCategory,
                                    };

                                    // amount left over after transmute. This will be amount of the resource if query is false as Required passed is 0
                                    double activityCost = requests.Where(a => a.Resource == transmute.TransmuteResourceType).Sum(a => a.Required);
                                    if (!transmute.DoTransmute(transRequest, packetsNeeded, activityCost, this, queryOnly))
                                    {
                                        allTransmutesSucceeed = false;
                                        break;
                                    }
                                }
                                else
                                {
                                    // the transmute resource (B) was not found so we cannot complete this transmutation
                                    allTransmutesSucceeed = false;
                                    break;
                                }
                            }

                            if (queryOnly)
                            {
                                if (allTransmutesSucceeed)
                                {
                                    // set request success
                                    request.SuccessfulTransmutation = transmutation;
                                    break;
                                }
                            }
                            else // assumed successful transaction based on where clause in transaction selection
                                // Add resource: tops up resource from tansmutation so available in CheckResources
                                (resourceTypeInShortfall as IResourceType).Add(packetsNeeded * transmutation.TransmutationPacketSize, request.ActivityModel, request.ResourceTypeName, transmutation.TransactionCategory);
                        }
                    }
                }
            }
        }

        #region Report pricing change

        /// <inheritdoc/>
        [JsonIgnore]
        public ResourcePriceChangeDetails LastPriceChange { get; set; }

        /// <inheritdoc/>
        public event EventHandler PriceChangeOccurred;

        /// <summary>
        /// Price changed event
        /// </summary>
        /// <param name="e"></param>
        protected void OnPriceChanged(PriceChangeEventArgs e)
        {
            PriceChangeOccurred?.Invoke(this, e);
        }

        private void Resource_PricingChangeOccurred(object sender, EventArgs e)
        {
            LastPriceChange = (e as PriceChangeEventArgs).Details;
            OnPriceChanged(e as PriceChangeEventArgs);
        }

        #endregion

        #region validation

        /// <summary>
        /// Validate object
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            // check that only one instance of each resource group is present
            foreach (var item in this.FindAllChildren<IResourceType>().GroupBy(a => a.GetType()).Where(b => b.Count() > 1))
            {
                string[] memberNames = new string[] { item.Key.FullName };
                results.Add(new ValidationResult(String.Format("Only one (1) instance of any resource group is allowed in the Resources Holder. Multiple Resource Groups [{0}] found!", item.Key.FullName), memberNames));
            }

            // check that only one resource type with a given name is present
            foreach (var item in this.FindAllDescendants<IResourceType>().GroupBy(a => $"{a.GetType().Name}:{a.Name}").Where(b => b.Count() > 1))
            {
                var bits = item.Key.Split(':');
                string[] memberNames = new string[] { "Multiple resource type with same name" };
                results.Add(new ValidationResult($"Only one component of type [r={bits.First()}] can be named [{bits.Last()}] in [{this.NameWithParent}]", memberNames));
            }
            return results;
        }

        #endregion

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary(bool formatForParentControl)
        {
            return "<h1>Resources summary</h1>";
        }

        /// <inheritdoc/>
        public override string ModelSummaryOpeningTags(bool formatForParentControl)
        {
            return "\r\n<div class=\"resource\" style=\"opacity: " + SummaryOpacity(formatForParentControl).ToString() + "\">";
        }

        /// <inheritdoc/>
        public override string ModelSummaryClosingTags(bool formatForParentControl)
        {
            return "\r\n</div>";
        }

        #endregion
    }
}
