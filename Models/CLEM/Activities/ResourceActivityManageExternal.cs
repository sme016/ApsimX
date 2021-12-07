using Models.CLEM.Interfaces;
using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Models.CLEM.Activities
{
    /// <summary>
    /// Activity to manage external resources from resource reader
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("Manage the input and output of external resources specified in a file")]
    [HelpUri(@"Content/Features/Activities/All resources/ManageExternalResource.htm")]
    [Version(1, 0, 1, "")]
    public class ResourceActivityManageExternal: CLEMActivityBase
    {
        [Link]
        private Clock clock = null;

        private FileResource fileResource = null;
        private FinanceType bankAccount = null;
        [JsonIgnore]
        [NonSerialized]
        private DataView currentEntries;
        [JsonIgnore]
        [NonSerialized]
        private List<IResourceType> resourceList;
        double earned = 0;
        double spent = 0;

        /// <summary>
        /// Name of the model for the resource input file
        /// </summary>
        [Description("Name of resource data reader")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Resource data reader required")]
        [Models.Core.Display(Type = DisplayType.DropDown, Values = "GetReadersAvailableByName", ValuesArgs = new object[] { new Type[] { typeof(FileResource) } })]
        public string ResourceDataReader { get; set; }

        /// <summary>
        /// Bank account to use
        /// </summary>
        [Description("Bank account to use")]
        [System.ComponentModel.DefaultValue("No financial implications")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new object[] { "No financial implications", typeof(Finance) } })]
        public string AccountName { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ResourceActivityManageExternal()
        {
            base.SetDefaults();
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            // get bank account object to use if provided
            if (AccountName != "No financial implications")
                bankAccount = Resources.FindResourceType<Finance, FinanceType>(this, AccountName, OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.Ignore);

            // get reader
            Model parentZone = this.FindAllAncestors<Zone>().FirstOrDefault();
            if(parentZone != null)
                fileResource = parentZone.FindAllChildren<FileResource>(ResourceDataReader).FirstOrDefault() as FileResource;
        }

        #region validation

        /// <summary>
        /// Validate this object
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            if (fileResource == null)
            {
                string[] memberNames = new string[] { "FileResourceReader" };
                results.Add(new ValidationResult("Unable to locate resource input file.\r\nAdd a [f=ResourceReader] component to the simulation tree.", memberNames));
            }
            return results;
        } 
        #endregion

        /// <inheritdoc/>
        public override List<ResourceRequest> GetResourcesNeededForActivity()
        {
            List<ResourceRequest> requests = new List<ResourceRequest>();
            earned = 0;
            spent = 0;

            // get data
            currentEntries = fileResource.GetCurrentResourceData(clock.Today.Month, clock.Today.Year);
            resourceList = new List<IResourceType>();
            if (currentEntries.Count > 0)
            {
                IResourceType resource = null;

                foreach (DataRowView item in currentEntries)
                {
                    // find resource
                    string resName = item[fileResource.ResourceNameColumnName].ToString();

                    if (resName.Contains("."))
                        resource = Resources.FindResourceType<ResourceBaseWithTransactions, IResourceType>(this, resName, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);
                    else
                    {
                        var found = Resources.FindAllDescendants<IResourceType>(resName);
                        if (found.Count() == 1)
                        {
                            resource = found.FirstOrDefault();

                            // highlight unsupported resource types
                            // TODO: add ability to include labour (days) and ruminants (number added/removed)
                            switch (resource.GetType().ToString())
                            {
                                case "Models.CLEM.Resources.LandType":
                                case "Models.CLEM.Resources.RuminantType":
                                case "Models.CLEM.Resources.LabourType":
                                case "Models.CLEM.Resources.GrazeFoodStoreType":
                                case "Models.CLEM.Resources.OtherAnimalsType":
                                    string warn = $"[a={this.Name}] does not support [r={resource.GetType()}]\r\nThis resource will be ignored. Contact developers for more information";
                                    Warnings.CheckAndWrite(warn, Summary, this, MessageType.Error);
                                    resource = null;
                                    break;
                            default:
                                    break;
                            }

                            // if finances
                            if (resource != null && bankAccount != null)
                            {
                                double amount = Convert.ToDouble(item[fileResource.AmountColumnName], CultureInfo.InvariantCulture);

                                // get price of resource
                                ResourcePricing price = resource.Price((amount > 0 ? PurchaseOrSalePricingStyleType.Purchase : PurchaseOrSalePricingStyleType.Sale));

                                double amountAvailable = (amount < 0) ? Math.Min(Math.Abs(amount), resource.Amount) : amount;

                                double packets = amountAvailable / price.PacketSize;
                                if (price.UseWholePackets)
                                    packets = Math.Truncate(packets);

                                if (amount < 0)
                                    earned += packets * price.PricePerPacket;
                                else
                                    spent += packets * price.PricePerPacket;
                            }
                        }
                        else
                        {
                            string warn = "";
                            if (found.Count() == 0)
                                warn = $"[a={this.Name}] could not find a resource [r={resName}] provided by [x={fileResource.Name}] in the local [r=ResourcesHolder]\r\nExternal transactions with this resource will be ignored\r\nYou can either add this resource to your simulation or remove it from the input file to avoid this warning";
                            else
                                warn = $"[a={this.Name}] could not distinguish between multiple occurences of resource [r={resName}] provided by [x={fileResource.Name}] in the local [r=ResourcesHolder]\r\nEnsure all resource names are unique across stores, or use ResourceStore.ResourceType notation to specify resources in the input file";

                            Warnings.CheckAndWrite(warn, Summary, this, MessageType.Error);
                        }
                    }
                    if(resource != null)
                        resourceList.Add(resource);
                }
            }
            return requests;
        }

        /// <inheritdoc/>
        public override GetDaysLabourRequiredReturnArgs GetDaysLabourRequired(LabourRequirement requirement)
        {
            double daysNeeded;
            switch (requirement.UnitType)
            {
                case LabourUnitType.Fixed:
                    daysNeeded = requirement.LabourPerUnit;
                    break;
                default:
                    throw new Exception(String.Format("LabourUnitType {0} is not supported for {1} in {2}", requirement.UnitType, requirement.Name, this.Name));
            }
            return new GetDaysLabourRequiredReturnArgs(daysNeeded, "External", null);
        }

        /// <summary>
        /// Method used to perform activity if it can occur as soon as resources are available.
        /// </summary>
        public override void DoActivity()
        {
            this.Status = ActivityStatus.NotNeeded;

            // get labour shortfall
            double labourLimit = this.LabourLimitProportion;
            // get finance purchase shortfall
            double financeLimit = (spent - earned > 0) ? Math.Min(bankAccount.Amount, spent - earned) / (spent - earned) : 1;

            if (resourceList.Count() == 0)
            {
                if (currentEntries.Count > 0)
                    this.Status = ActivityStatus.Warning;
                return;
            }
            else
            {
                if (financeLimit < 1)
                {
                    ResourceRequest purchaseRequest = new ResourceRequest
                    {
                        ActivityModel = this,
                        Required = spent - earned,
                        Available = bankAccount.Amount,
                        Provided = bankAccount.Amount,
                        AllowTransmutation = false,
                        ResourceType = typeof(Finance),
                        ResourceTypeName = AccountName,
                        Category = "External purchases"
                    };
                    ResourceRequestEventArgs rre = new ResourceRequestEventArgs() { Request = purchaseRequest };
                    OnShortfallOccurred(rre);
                }

                if (financeLimit < 1 || labourLimit < 1)
                {
                    switch (OnPartialResourcesAvailableAction)
                    {
                        case OnPartialResourcesAvailableActionTypes.ReportErrorAndStop:
                                Summary.WriteMessage(this, $"Ensure resources are available or change OnPartialResourcesAvailableAction setting for activity [a={this.NameWithParent}]", MessageType.Warning);
                                Status = ActivityStatus.Critical;
                                throw new ApsimXException(this, $"Insufficient resources [r={AccountName}] for activity [a={this.NameWithParent}]");
                        case OnPartialResourcesAvailableActionTypes.SkipActivity:
                            this.Status = ActivityStatus.Ignored;
                            return;
                        default:
                            break;
                    }
                    this.Status = ActivityStatus.Partial;
                }
                else
                    this.Status = ActivityStatus.Success;

                // loop through all resources to exchange and make transactions
                for (int i = 0; i < currentEntries.Count; i++)
                {
                    if (resourceList[i] is null)
                        this.Status = ActivityStatus.Warning;
                    else
                    {
                        // matching resource was found
                        double amount = Convert.ToDouble(currentEntries[i][fileResource.AmountColumnName], CultureInfo.InvariantCulture);
                        bool isSale = (amount < 0);
                        amount = Math.Abs(amount);
                        ResourcePricing price = null;
                        if (bankAccount != null && !(resourceList[i] is FinanceType))
                        {
                            price = resourceList[i].Price((amount > 0 ? PurchaseOrSalePricingStyleType.Purchase : PurchaseOrSalePricingStyleType.Sale));
                        }
                        // transactions
                        if (isSale)
                        {
                            // sell, so limit to labour and amount available
                            amount = Math.Min(amount * labourLimit, resourceList[i].Amount);
                            if (amount > 0)
                            {
                                if (price != null)
                                {
                                    double packets = amount / price.PacketSize;
                                    if (price.UseWholePackets)
                                    {
                                        packets = Math.Truncate(packets);
                                        amount = packets * price.PacketSize;
                                    }
                                    bankAccount.Add(packets * price.PricePerPacket, this, (resourceList[i] as CLEMModel).NameWithParent, "External output");
                                }
                                ResourceRequest sellRequest = new ResourceRequest
                                {
                                    ActivityModel = this,
                                    Required = amount,
                                    AllowTransmutation = false,
                                    Category = "External output",
                                    RelatesToResource = (resourceList[i] as CLEMModel).NameWithParent
                                };
                                resourceList[i].Remove(sellRequest);
                            }
                        }
                        else
                        {
                            // limit to labour and financial constraints as this is a purchase
                            amount *= Math.Min(labourLimit, financeLimit);
                            if (amount > 0)
                            {
                                if (price != null)
                                {
                                    // need to limit amount by financial constraints
                                    double packets = amount / price.PacketSize;
                                    if (price.UseWholePackets)
                                    {
                                        packets = Math.Truncate(packets);
                                    }
                                    amount = packets * price.PacketSize;
                                    ResourceRequest sellRequestDollars = new ResourceRequest
                                    {
                                        ActivityModel = this,
                                        Required = packets * price.PacketSize,
                                        AllowTransmutation = false,
                                        Category = "External input",
                                        RelatesToResource = (resourceList[i] as CLEMModel).NameWithParent
                                    };
                                    bankAccount.Remove(sellRequestDollars);
                                }
                                resourceList[i].Add(amount, this, (resourceList[i] as CLEMModel).NameWithParent, "External input");
                            }
                        }

                    }
                }
            }

        }

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary()
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">Resources added or removed are provided by ");
                htmlWriter.Write(CLEMModel.DisplaySummaryValueSnippet(ResourceDataReader, "Reader not set", HTMLSummaryStyle.FileReader));
                htmlWriter.Write("</div>");
                htmlWriter.Write("\r\n<div class=\"activityentry\">");
                if (AccountName == null || AccountName == "")
                    htmlWriter.Write("Financial transactions will be made to <span class=\"errorlink\">FinanceType not set</span>");
                else if (AccountName == "No financial implications")
                    htmlWriter.Write("No financial constraints relating to pricing and packet sizes associated with each resource will be included.");
                else
                    htmlWriter.Write("Pricing and packet sizes associated with each resource will be used with <span class=\"resourcelink\">" + AccountName + "</span>");
                htmlWriter.Write("</div>");
                return htmlWriter.ToString(); 
            }
        } 
        #endregion

    }
}
