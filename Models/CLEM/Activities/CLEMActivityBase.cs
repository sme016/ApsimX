﻿using Models.Core;
using Models.CLEM.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Models.CLEM.Groupings;
using Models.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Models.CLEM.Activities
{
    ///<summary>
    /// CLEM Activity base model
    ///</summary> 
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [Description("This is the CLEM Activity Base Class and should not be used directly.")]
    [Version(1, 0, 1, "")]
    public abstract class CLEMActivityBase: CLEMModel
    {
        /// <summary>
        /// A protected link to the CLEM resource holder
        /// </summary>
        [Link]
        protected ResourcesHolder Resources = null;

        private bool enabled = true;
        private IEnumerable<CLEMActivityBase> activityChildren = null;
        private ZoneCLEM parentZone = null;

        /// <summary>
        /// Label to assign each transaction created by this activity in ledgers
        /// </summary>
        [Description("Category for transactions")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Category for transactions required")]
        virtual public string TransactionCategory { get; set; }

        /// <summary>
        /// Insufficient resources available action
        /// </summary>
        [Description("Insufficient resources available action")]
        public OnPartialResourcesAvailableActionTypes OnPartialResourcesAvailableAction { get; set; }

        /// <summary>
        /// Current list of resources requested by this activity
        /// </summary>
        [JsonIgnore]
        public List<ResourceRequest> ResourceRequestList { get; set; }

        /// <summary>
        /// Current list of activities under this activity
        /// </summary>
        [JsonIgnore]
        public List<CLEMActivityBase> ActivityList { get; set; }

        /// <summary>
        /// Current status of this activity
        /// </summary>
        [JsonIgnore]
        public ActivityStatus Status { get; set; }

        /// <summary>
        /// Resource allocation style
        /// </summary>
        [JsonIgnore]
        public ResourceAllocationStyle AllocationStyle { get; set; }

        /// <summary>
        /// A list of activity base chldren for this activity
        /// </summary>
        public IEnumerable<CLEMActivityBase> ActivityChildren
        {
            get
            {
                if (activityChildren is null)
                    activityChildren = FindAllChildren<CLEMActivityBase>();
                return activityChildren;
            }
        }

        /// <summary>
        /// Current status of this activity
        /// </summary>
        [JsonIgnore]
        public bool ActivityEnabled
        {
            get
            {
                return enabled;
            }
            set
            {
                if(value!=enabled)
                    foreach (var child in this.FindAllChildren<CLEMActivityBase>())
                        child.ActivityEnabled = value;
                    enabled = value;
            }
        }

        /// <summary>
        /// Multiplier for farms in this zone
        /// </summary>
        public double FarmMultiplier 
        {
            get
            {
                if(parentZone is null)
                    parentZone = FindAncestor<ZoneCLEM>();

                if(parentZone is null)
                    return 1;
                else
                    return parentZone.FarmMultiplier;
            }
        }

        /// <summary>
        /// Property to check if timing of this activity is ok based on child and parent ActivityTimers in UI tree
        /// </summary>
        /// <returns>T/F</returns>
        public virtual new bool TimingOK
        {
            get
            {
                // use timing to not perform activity based on Enabled state
                if (!ActivityEnabled)
                    return false;

                // sum all where true=0 and false=1 so that all must be zero to get a sum total of zero or there are no timers
                int result = 0;
                IModel current = this as IModel;
                while (current.GetType() != typeof(ZoneCLEM) & current.GetType() != typeof(Market))
                {
                    if(current is CLEMModel)
                        result += (current as CLEMModel).ActivityTimers.Sum(a => a.ActivityDue ? 0 : 1);
                    current = current.Parent as IModel;
                }
                return (result == 0);
            }
        }

        /// <summary>
        /// Resource shortfall occured event handler
        /// </summary>
        public event EventHandler ResourceShortfallOccurred;

        /// <summary>
        /// Activity performed event handler
        /// </summary>
        public event EventHandler ActivityPerformed;

        /// <summary>
        /// Method to check if timing of this activity is ok based on child and parent ActivityTimers in UI tree and a specified date
        /// </summary>
        /// <returns>T/F</returns>
        public bool TimingCheck(DateTime date)
        {
            // use timing to not perform activity based on Enabled state
            if (!ActivityEnabled)
                return false;

            // sum all where true=0 and false=1 so that all must be zero to get a sum total of zero or there are no timers
            int result = 0;
            IModel current = this as IModel;
            while (current.GetType() != typeof(ZoneCLEM))
            {
                if (current is CLEMModel)
                    result += (current as CLEMModel).ActivityTimers.Sum(a => a.Check(date) ? 0 : 1);
                current = current.Parent as IModel;
            }
            return (result == 0);
        }

        /// <summary>
        /// Property to check if timing of this activity is ok based on child and parent ActivityTimers in UI tree
        /// </summary>
        /// <returns>T/F</returns>
        public bool TimingExists
        {
            get
            {
                // sum all where true=0 and false=1 so that all must be zero to get a sum total of zero or there are no timers
                int result = 0;
                IModel current = this as IModel;
                while (current.GetType() != typeof(ZoneCLEM))
                {
                    if (current is CLEMModel)
                        result += (current as CLEMModel).ActivityTimers.Count();
                    current = current.Parent as IModel;
                }
                return (result != 0);
            }
        }

        /// <summary>
        /// A method to allow the resource holder to be set when [Link] not possible for dynamically created model
        /// </summary>
        /// <param name="resourceHolder">The resource holder to provide</param>
        public void SetLinkedModels(ResourcesHolder resourceHolder)
        {
            Resources = resourceHolder;
        }

        /// <summary>
        /// Sets the status of the activity to success if the activity was in fact needed this time step
        /// </summary>
        public void SetStatusSuccess()
        {
            if(Status== ActivityStatus.NotNeeded)
                Status = ActivityStatus.Success;
        }

        /// <summary>
        /// return a list of components available given the specified types
        /// </summary>
        /// <param name="typesToFind">the list of types to locate</param>
        /// <returns>A list of names of components</returns>
        public IEnumerable<string> GetReadersAvailableByName(Type[] typesToFind)
        {
            List<string> results = new List<string>();
            Simulation simulation = this.FindAncestor<Simulation>();
            if (simulation != null)
            {
                foreach (Type type in typesToFind)
                    results.AddRange(simulation.FindAllDescendants().Where(a => a.GetType() == type).Select(a => a.Name));
            }
            return results.AsEnumerable();
        }

        /// <summary>
        /// Method to cascade calls for calling activites performed for all activities in the UI tree. 
        /// </summary>
        public virtual void ClearAllAllActivitiesPerformedStatus()
        {
            ClearActivitiesPerformedStatus();
        }

        /// <summary>
        /// Protected method to cascade clearing of status for all dynamic activities created for this activity. 
        /// </summary>
        protected void ClearActivitiesPerformedStatus()
        {
            // clear status of all dynamically created CLEMActivityBase activities
            if (ActivityList != null)
            {
                foreach (CLEMActivityBase activity in ActivityList)
                {
                    activity.Status = ActivityStatus.Ignored;
                    activity.ClearAllAllActivitiesPerformedStatus();
                }
            }
            // clear status for all children of type CLEMActivityBase
            foreach (CLEMActivityBase activity in ActivityChildren)
            {
                activity.Status = ActivityStatus.Ignored;
                activity.ClearAllAllActivitiesPerformedStatus();
            }
        }

        /// <summary>
        /// Method to cascade calls for calling activites performed for all activities in the UI tree. 
        /// </summary>
        public virtual void ReportAllAllActivitiesPerformed()
        {
            ReportActivitiesPerformed();
        }

        /// <summary>
        /// Protected method to cascade calls for activities performed for all dynamically created activities
        /// </summary>
        protected void ReportActivitiesPerformed()
        {
            this.TriggerOnActivityPerformed();
            // call activity performed for all dynamically created CLEMActivityBase activities
            if (ActivityList != null)
            {
                foreach (CLEMActivityBase activity in ActivityList)
                    activity.ReportAllAllActivitiesPerformed();
            }
            // call activity performed  for all children of type CLEMActivityBase
            foreach (CLEMActivityBase activity in ActivityChildren)
                activity.ReportAllAllActivitiesPerformed();
        }

        /// <summary>
        /// Method to cascade calls for resources for all activities in the UI tree. 
        /// Responds to CLEMInitialiseActivity in the Activity model holing top level list of activities
        /// </summary>
        public virtual void GetResourcesForAllActivityInitialisation()
        {
            ResourcesForAllActivityInitialisation();
        }

        /// <summary>
        /// Protected method to cascade calls for resources for all activities in the UI tree. 
        /// </summary>
        protected void ResourcesForAllActivityInitialisation()
        {
            if (this.Enabled)
            {
                // Get resources needed and use substitution if needed and provided, then move through children getting their resources.
                GetResourcesRequiredForInitialisation();

                // get resources required for all dynamically created CLEMActivityBase activities
                if (ActivityList != null)
                {
                    foreach (CLEMActivityBase activity in ActivityList)
                        activity.GetResourcesForAllActivityInitialisation();
                }
                // get resources required for all children of type CLEMActivityBase
                foreach (CLEMActivityBase activity in ActivityChildren)
                {
                    if (activity.Enabled)
                        activity.GetResourcesForAllActivityInitialisation();
                }
            }
        }

        /// <summary>
        /// Method to cascade calls for resources for all activities in the UI tree. 
        /// Responds to CLEMGetResourcesRequired in the Activity model holing top level list of activities
        /// </summary>
        public virtual void GetResourcesForAllActivities(CLEMModel model)
        {
            if (this.Enabled)
            {
                if (TimingOK)
                    ResourcesForAllActivities(model);
                else
                {
                    this.Status = ActivityStatus.Ignored;
                    if (ActivityList != null)
                    {
                        foreach (CLEMActivityBase activity in ActivityList)
                            activity.Status = ActivityStatus.Ignored;
                    }
                    // get resources required for all children of type CLEMActivityBase
                    foreach (CLEMActivityBase activity in ActivityChildren)
                        activity.Status = ActivityStatus.Ignored;
                }
            }
        }

        /// <summary>
        /// protected method to cascade calls for resources for all activities in the UI tree. 
        /// </summary>
        protected void ResourcesForAllActivities(CLEMModel model)
        {
            // Get resources needed and use substitution if needed and provided, then move through children getting their resources.

            if ((model.GetType() == typeof(ActivitiesHolder) & this.AllocationStyle == ResourceAllocationStyle.Automatic) | (model.GetType() != typeof(ActivitiesHolder)))
            {
                // this will be performed if
                // (a) the call has come from the Activity Holder and is therefore using the GetResourcesRequired event and the allocation style is automatic, or
                // (b) the call has come from the Activity
                GetResourcesRequiredForActivity();
            }

            // get resources required for all dynamically created CLEMActivityBase activities
            if (ActivityList != null)
            {
                foreach (CLEMActivityBase activity in ActivityList)
                    activity.GetResourcesForAllActivities(model);
            }
            // get resources required for all children of type CLEMActivityBase
            foreach (CLEMActivityBase activity in ActivityChildren)
                activity.GetResourcesForAllActivities(model);
        }

        /// <summary>
        /// Method to get required resources for initialisation of this activity. 
        /// </summary>
        public virtual void GetResourcesRequiredForInitialisation()
        {
            ResourcesRequiredForInitialisation();
        }

        /// <summary>
        /// Protected method to get required resources for initialisation of this activity. 
        /// </summary>
        protected void ResourcesRequiredForInitialisation()
        {
            // determine what resources are needed for initialisation
            ResourceRequestList = GetResourcesNeededForinitialisation();

            CheckResources(ResourceRequestList, Guid.NewGuid());
            TakeResources(ResourceRequestList, false);

            ResourceRequestList = null;
        }

        /// <summary>
        /// Method to get this time steps current required resources for this activity. 
        /// </summary>
        public virtual void GetResourcesRequiredForActivity()
        {
            ResourcesRequiredForActivity();
        }

        /// <summary>
        /// Protected method to get this time steps current required resources for this activity. 
        /// </summary>
        protected void ResourcesRequiredForActivity()
        {
            // clear Resources Required list
            ResourceRequestList = new List<ResourceRequest>();

            if (this.TimingOK)
            {
                // add any labour resources required (automated here so not needed in Activity code)
                ResourceRequestList.AddRange(GetLabourResourcesNeededForActivity());

                // add any non-labour resources needed (from method in Activity code)
                var requests = GetResourcesNeededForActivity();
                if (requests != null)
                    ResourceRequestList.AddRange(requests);

                // check availability
                CheckResources(ResourceRequestList, Guid.NewGuid());

                // adjust if needed
                AdjustResourcesNeededForActivity();

                // take resources
                bool tookRequestedResources = TakeResources(ResourceRequestList, false);

                // if no resources required perform Activity if code is present.
                // if resources are returned (all available or UseResourcesAvailable action) perform Activity
                if (tookRequestedResources || (ResourceRequestList.Count == 0))
                    DoActivity();
            }
        }

        /// <summary>
        /// A common method to get the labour resource requests from the activity.
        /// </summary>
        /// <returns></returns>
        protected List<ResourceRequest> GetLabourResourcesNeededForActivity()
        {
            List<ResourceRequest> labourResourceRequestList = new List<ResourceRequest>();
            foreach (LabourRequirement item in FindAllChildren<LabourRequirement>())
            {
                GetDaysLabourRequiredReturnArgs daysResult = GetDaysLabourRequired(item);
                if (daysResult.DaysNeeded > 0)
                {
                    foreach (LabourFilterGroup fg in item.FindAllChildren<LabourFilterGroup>())
                    {
                        int numberOfPpl = 1;
                        if (item.ApplyToAll)
                            // how many matches
                            numberOfPpl = Resources.FindResourceGroup<Labour>().Items.Filter(fg).Count();
                        for (int i = 0; i < numberOfPpl; i++)
                        {
                            labourResourceRequestList.Add(new ResourceRequest()
                            {
                                AllowTransmutation = true,
                                Required = daysResult.DaysNeeded,
                                ResourceType = typeof(Labour),
                                ResourceTypeName = "",
                                ActivityModel = this,
                                FilterDetails = new List<object>() { fg },
                                Category = daysResult.Category,
                                RelatesToResource = daysResult.RelatesToResource
                            }
                            ); ;
                        }
                    }
                }
            }
            return labourResourceRequestList;
        }

        /// <summary>
        /// Method to provide the proportional limit based on labour shortfall
        /// A proportion less than 1 will only be returned if LabourShortfallAffectsActivity is true in the LabourRequirement
        /// </summary>
        /// <returns></returns>
        public double LabourLimitProportion
        {
            get
            {
                double proportion = 1.0;
                if (ResourceRequestList == null)
                    return proportion;

                double totalNeeded = ResourceRequestList.Where(a => a.ResourceType == typeof(LabourType)).Sum(a => a.Required);

                foreach (ResourceRequest item in ResourceRequestList.Where(a => a.ResourceType == typeof(LabourType)))
                    if (item.FilterDetails != null && ((item.FilterDetails.First() as LabourFilterGroup).Parent as LabourRequirement).LabourShortfallAffectsActivity)
                        proportion *= item.Provided / item.Required;
                return proportion;
            }
        }

        /// <summary>
        /// Method to provide the proportional limit based on specified resource type
        /// A proportion less than 1 will only be returned if LabourShortfallAffectsActivity is true in the LabourRequirement
        /// </summary>
        /// <returns></returns>
        public double LimitProportion(Type resourceType)
        {
            double proportion = 1.0;
            if (ResourceRequestList == null)
                return proportion;

            double totalNeeded = ResourceRequestList.Where(a => a.ResourceType == resourceType).Sum(a => a.Required);

            foreach (ResourceRequest item in ResourceRequestList.Where(a => a.ResourceType == resourceType))
            {
                if (resourceType == typeof(LabourType))
                    if (item.FilterDetails != null && ((item.FilterDetails.First() as LabourFilterGroup).Parent as LabourRequirement).LabourShortfallAffectsActivity)
                        proportion *= item.Provided / item.Required;
                else // all other types
                    proportion *= item.Provided / item.Required;
            }
            return proportion;
        }

        /// <summary>
        /// Method to determine if activity limited based on labour shortfall has been set
        /// </summary>
        /// <returns></returns>
        public bool IsLabourLimitSet
        {
            get
            {
                foreach (LabourRequirement item in FindAllChildren<LabourRequirement>())
                    if (item.LabourShortfallAffectsActivity)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Method to determine available labour based on filters and take it if requested.
        /// </summary>
        /// <param name="request">Resource request details</param>
        /// <param name="removeFromResource">Determines if only calculating available labour or labour removed</param>
        /// <param name="callingModel">Model calling this method</param>
        /// <param name="resourceHolder">Location of resource holder</param>
        /// <param name="partialAction">Action on partial resources available</param>
        /// <returns></returns>
        public static double TakeLabour(ResourceRequest request, bool removeFromResource, IModel callingModel, ResourcesHolder resourceHolder, OnPartialResourcesAvailableActionTypes partialAction )
        {
            double amountProvided = 0;
            double amountNeeded = request.Required;
            LabourFilterGroup current = request.FilterDetails.OfType<LabourFilterGroup>().FirstOrDefault();

            LabourRequirement lr;
            if (current!=null)
            {
                if (current.Parent is LabourRequirement)
                    lr = current.Parent as LabourRequirement;
                else
                    // coming from Transmutation request
                    lr = new LabourRequirement()
                    {
                        ApplyToAll = false,
                        MaximumPerPerson = 1000,
                        MinimumPerPerson = 0
                    };
            }
            else
                lr = callingModel.FindAllChildren<LabourRequirement>().FirstOrDefault();

            int currentIndex = 0;
            if (current==null)
                // no filtergroup provided so assume any labour
                current = new LabourFilterGroup();

            request.ResourceTypeName = "Labour";
            ResourceRequest removeRequest = new ResourceRequest()
            {
                ActivityID = request.ActivityID,
                ActivityModel = request.ActivityModel,
                AdditionalDetails = request.AdditionalDetails,
                AllowTransmutation = request.AllowTransmutation,
                Available = request.Available,
                FilterDetails = request.FilterDetails,
                Provided = request.Provided,
                Category = request.Category,
                RelatesToResource = request.RelatesToResource,
                Required = request.Required,
                Resource = request.Resource,
                ResourceType = request.ResourceType,
                ResourceTypeName = (request.Resource is null? "":(request.Resource as CLEMModel).NameWithParent)
            };

            // start with top most LabourFilterGroup
            while (current != null && amountProvided < amountNeeded)
            {
                List<LabourType> items = (resourceHolder.GetResourceGroupByType(request.ResourceType) as Labour).Items;
                items = items.Where(a => (a.LastActivityRequestID != request.ActivityID) || (a.LastActivityRequestID == request.ActivityID && a.LastActivityRequestAmount < lr.MaximumPerPerson)).ToList();
                items = items.Filter(current).ToList();

                // search for people who can do whole task first
                while (amountProvided < amountNeeded && items.Where(a => a.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson) >= request.Required).Count() > 0)
                {
                    // get labour least available but with the amount needed
                    LabourType lt = items.Where(a => a.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson) >= request.Required).OrderBy(a => a.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson)).FirstOrDefault();

                    double amount = Math.Min(amountNeeded - amountProvided, lt.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson));

                    // limit to max allowed per person
                    amount = Math.Min(amount, lr.MaximumPerPerson);
                    // limit to min per person to do activity
                    if (amount < lr.MinimumPerPerson)
                    {
                        request.Category = "Min labour limit";
                        return amountProvided;
                    }

                    amountProvided += amount;
                    removeRequest.Required = amount;
                    if (removeFromResource)
                    {
                        lt.LastActivityRequestID = request.ActivityID;
                        lt.LastActivityRequestAmount = amount;
                        lt.Remove(removeRequest);
                        request.Provided += removeRequest.Provided;
                        request.Value += request.Provided * lt.PayRate();
                    }
                }

                // if still needed and allow partial resource use.
                if (partialAction == OnPartialResourcesAvailableActionTypes.UseResourcesAvailable)
                {
                    if (amountProvided < amountNeeded)
                    {
                        // then search for those that meet criteria and can do part of task
                        foreach (LabourType item in items.Where(a => a.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson) >= 0).OrderByDescending(a => a.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson))) 
                        {
                            if (amountProvided >= amountNeeded)
                                break;

                            double amount = Math.Min(amountNeeded - amountProvided, item.LabourCurrentlyAvailableForActivity(request.ActivityID, lr.MaximumPerPerson));

                            // limit to max allowed per person
                            amount = Math.Min(amount, lr.MaximumPerPerson);

                            // limit to min per person to do activity
                            if (amount >= lr.MinimumPerPerson)
                            {
                                amountProvided += amount;
                                removeRequest.Required = amount;
                                if (removeFromResource)
                                {
                                    if(item.LastActivityRequestID != request.ActivityID)
                                        item.LastActivityRequestAmount = 0;
                                    item.LastActivityRequestID = request.ActivityID;
                                    item.LastActivityRequestAmount += amount;
                                    item.Remove(removeRequest);
                                    request.Provided += removeRequest.Provided;
                                    request.Value += request.Provided * item.PayRate();
                                }
                            }
                            else
                                currentIndex = request.FilterDetails.Count;
                        }
                    }
                }
                currentIndex++;
                var currentFilterGroups = current.FindAllChildren<LabourFilterGroup>();
                if (currentFilterGroups.Any())
                    current = currentFilterGroups.FirstOrDefault();
                else
                    current = null;
            }
            // report amount gained.
            return amountProvided;
        }

        /// <summary>
        /// Method to determine available non-labour resources and take if requested.
        /// </summary>
        /// <param name="request">Resource request details</param>
        /// <param name="removeFromResource">Determines if only calculating available labour or labour removed</param>
        /// <returns></returns>
        private double TakeNonLabour(ResourceRequest request, bool removeFromResource)
        {
            // get available resource
            if (request.Resource == null)
                //If it hasn't been assigned try and find it now.
                request.Resource = Resources.FindResourceType<ResourceBaseWithTransactions, IResourceType>(request, OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore);

            if (request.Resource != null)
                // get amount available
                request.Available = Math.Min(request.Resource.Amount, request.Required);

            if(removeFromResource && request.Resource != null)
                request.Resource.Remove(request);

            return request.Available;
        }

        /// <summary>
        /// Determine resources available and perform transmutation if needed.
        /// </summary>
        /// <param name="resourceRequests">List of requests</param>
        /// <param name="uniqueActivityID">Unique id for the activity</param>
        public void CheckResources(IEnumerable<ResourceRequest> resourceRequests, Guid uniqueActivityID)
        {
            if (resourceRequests is null || !resourceRequests.Any())
            {
                this.Status = ActivityStatus.Success;
                return;
            }

            foreach (ResourceRequest request in resourceRequests)
            {
                request.ActivityID = uniqueActivityID;
                request.Available = 0;

                // If resource group does not exist then provide required.
                // This means when resource is not added to model it will not limit simulations
                if (request.ResourceType == null || Resources.GetResourceGroupByType(request.ResourceType) == null)
                {
                    request.Available = request.Required;
                    request.Provided = request.Required;
                }
                else
                {
                    if (request.ResourceType == typeof(Labour))
                        // get available labour based on rules.
                        request.Available = TakeLabour(request, false, this, Resources, this.OnPartialResourcesAvailableAction);
                    else
                        request.Available = TakeNonLabour(request, false);
                }
            }

            // are all resources available
            IEnumerable<ResourceRequest> shortfallRequests = resourceRequests.Where(a => a.Required > a.Available);
            if (shortfallRequests.Any())
                // check what transmutations can occur
                Resources.TransmutateShortfall(shortfallRequests);

            // check if need to do transmutations
            int countTransmutationsSuccessful = shortfallRequests.Where(a => a.TransmutationPossible == true && a.AllowTransmutation).Count();
            bool allTransmutationsSuccessful = (shortfallRequests.Where(a => a.TransmutationPossible == false && a.AllowTransmutation).Count() == 0);

            // OR at least one transmutation successful and PerformWithPartialResources
            if ((shortfallRequests.Any() && (shortfallRequests.Count() == countTransmutationsSuccessful)) || (countTransmutationsSuccessful > 0 && OnPartialResourcesAvailableAction == OnPartialResourcesAvailableActionTypes.UseResourcesAvailable))
            {
                // do transmutations.
                // this uses the current zone resources, but will find markets if needed in the process
                Resources.TransmutateShortfall(shortfallRequests, false);

                // recheck resource amounts now that resources have been topped up
                foreach (ResourceRequest request in resourceRequests)
                {
                    // get resource
                    request.Available = 0;
                    if (request.Resource != null)
                        // get amount available
                        request.Available = Math.Min(request.Resource.Amount, request.Required);
                }
            }

            bool deficitFound = false;
            // report any resource defecits here
            foreach (var item in resourceRequests.Where(a => (a.Available - a.Required) > 0.000001))
            {
                ResourceRequestEventArgs rrEventArgs = new ResourceRequestEventArgs() { Request = item };

                if (item.Resource != null && (item.Resource as Model).FindAncestor<Market>() != null)
                {
                    ActivitiesHolder marketActivities = Resources.FoundMarket.FindChild<ActivitiesHolder>();
                    if(marketActivities != null)
                        marketActivities.ActivitiesHolder_ResourceShortfallOccurred(this, rrEventArgs);
                }
                else
                    OnShortfallOccurred(rrEventArgs);
                Status = ActivityStatus.Partial;
                deficitFound = true;
            }
            if(!deficitFound)
                this.Status = ActivityStatus.Success;
        }

        /// <summary>
        /// Try to take the Resources based on Resource Request List provided.
        /// Returns true if it was able to take the resources it needed.
        /// Returns false if it was unable to take the resources it needed.
        /// </summary>
        /// <param name="resourceRequestList"></param>
        /// <param name="triggerActivityPerformed"></param>
        public bool TakeResources(List<ResourceRequest> resourceRequestList, bool triggerActivityPerformed)
        {
            // no resources required or this is an Activity folder.
            if ((resourceRequestList == null)||(!resourceRequestList.Any()))
                return false;

            // remove activity resources 
            // check if deficit and performWithPartial
            if ((resourceRequestList.Where(a => a.Required > a.Available).Count() == 0) || OnPartialResourcesAvailableAction != OnPartialResourcesAvailableActionTypes.SkipActivity)
            {
                if(OnPartialResourcesAvailableAction == OnPartialResourcesAvailableActionTypes.ReportErrorAndStop)
                {
                    string resourcelist = "";
                    foreach (var item in resourceRequestList.Where(a => a.Required > a.Available))
                        resourcelist += item.ResourceType.Name + ",";

                    resourcelist = resourcelist.Trim(',');
                    if (resourcelist.Length > 0)
                    {
                        Summary.WriteError(this, String.Format("Ensure all resources are available or change OnPartialResourcesAvailableAction setting for activity [a={0}]", this.Name));
                        Status = ActivityStatus.Critical;
                        throw new Exception(String.Format("@i:Insufficient resources [r={0}] for activity [a={1}]", resourcelist, this.Name));
                    }
                }

                foreach (ResourceRequest request in resourceRequestList)
                {
                    // get resource
                    request.Provided = 0;
                    // do not take if the resource does not exist
                    if (request.ResourceType != null && Resources.GetResourceGroupByType(request.ResourceType) != null)
                    { 
                        if (request.ResourceType == typeof(Labour))
                            // get available labour based on rules.
                            request.Available = TakeLabour(request, true, this, Resources, this.OnPartialResourcesAvailableAction);
                        else
                            request.Available = TakeNonLabour(request, true);
                    }
                }
            }
            else
                Status = ActivityStatus.Ignored;
            return Status != ActivityStatus.Ignored;
        }

        /// <summary>
        /// Method to trigger an Activity Performed event 
        /// </summary>
        public void TriggerOnActivityPerformed()
        {
            ActivityPerformedEventArgs activitye = new ActivityPerformedEventArgs
            {
                Activity = this
            };
            this.OnActivityPerformed(activitye);
        }

        /// <summary>
        /// Method to trigger an Activity Performed event 
        /// </summary>
        /// <param name="status">The status of this activity to be reported</param>
        public void TriggerOnActivityPerformed(ActivityStatus status)
        {
            this.Status = status;
            ActivityPerformedEventArgs activitye = new ActivityPerformedEventArgs
            {
                Activity = new ActivityFolder() { Name = this.Name, Status = status }
            };
            this.OnActivityPerformed(activitye);
        }

        /// <summary>
        /// Base method to determine the number of days labour required based on Activity requirements and labour settings.
        /// Functionality provided in derived classes
        /// </summary>
        public virtual GetDaysLabourRequiredReturnArgs GetDaysLabourRequired(LabourRequirement requirement)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Method to determine list of resources and amounts needed. 
        /// Functionality provided in derived classes
        /// </summary>
        public virtual List<ResourceRequest> GetResourcesNeededForActivity()
        {
            return null;
        }

        /// <summary>
        /// Method to adjust activities needed based on shortfalls before they are taken from resource pools. 
        /// Functionality provided in derived classes
        /// </summary>
        public virtual void AdjustResourcesNeededForActivity()
        {
            return;
        }

        /// <summary>
        /// Method to determine list of resources and amounts needed for initilaisation. 
        /// Functionality provided in derived classes
        /// </summary>
        public virtual List<ResourceRequest> GetResourcesNeededForinitialisation()
        {
            return null;
        }

        /// <summary>
        /// Method to perform activity tasks if expected as soon as resources are available
        /// Functionality provided in derived classes
        /// </summary>
        public virtual void DoActivity()
        {
            return;
        }


        /// <summary>
        /// Shortfall occurred 
        /// </summary>
        /// <param name="e"></param>
        protected void OnShortfallOccurred(EventArgs e)
        {
            ResourceShortfallOccurred?.Invoke(this, e);
        }


        /// <summary>
        /// Activity has occurred 
        /// </summary>
        /// <param name="e"></param>
        protected void OnActivityPerformed(EventArgs e)
        {
            ActivityPerformed?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Structure to return values form a labour days request
    /// </summary>
    public class GetDaysLabourRequiredReturnArgs
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="daysNeeded"></param>
        /// <param name="category"></param>
        /// <param name="relatesToResource"></param>
        public GetDaysLabourRequiredReturnArgs(double daysNeeded, string category, string relatesToResource)
        {
            DaysNeeded = daysNeeded;
            Category = category;
            RelatesToResource = relatesToResource;
        }

        /// <summary>
        /// Calculated days needed
        /// </summary>
        public double DaysNeeded { get; set; }

        /// <summary>
        /// Transaction category
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Transacation relates to resource
        /// </summary>
        public string RelatesToResource { get; set; }
    }

    /// <summary>
    /// Status of activity
    /// </summary>
    public enum ActivityStatus
    {
        /// <summary>
        /// Performed with all resources available
        /// </summary>
        Success,
        /// <summary>
        /// Performed with partial resources available
        /// </summary>
        Partial,
        /// <summary>
        /// Insufficient resources so activity ignored
        /// </summary>
        Ignored,
        /// <summary>
        /// Insufficient resources so simulation stopped
        /// </summary>
        Critical,
        /// <summary>
        /// Indicates a timer occurred successfully
        /// </summary>
        Timer,
        /// <summary>
        /// Indicates a calculation event occurred
        /// </summary>
        Calculation,
        /// <summary>
        /// Indicates activity occurred but was not needed
        /// </summary>
        NotNeeded,
        /// <summary>
        /// Indicates activity caused a warning and was not performed
        /// </summary>
        Warning,
        /// <summary>
        /// Indicates activity was place holder or parent activity
        /// </summary>
        NoTask
    }

    /// <summary>
    /// Status of activity
    /// </summary>
    public enum ResourceAllocationStyle
    {
        /// <summary>
        /// Automatically perform in CLEMGetResourcesRequired
        /// </summary>
        Automatic,
        /// <summary>
        /// Manually perform in activity code.
        /// </summary>
        Manual
    }

}
