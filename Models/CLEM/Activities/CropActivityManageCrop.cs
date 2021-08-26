﻿using Models.Core;
using Models.CLEM.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Models.Core.Attributes;
using System.IO;

namespace Models.CLEM.Activities
{
    /// <summary>Grow management activity</summary>
    /// <summary>This activity sets aside land for the crop(s)</summary>
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("This activity manages a crop(s) by assigning land to be used for child activities.")]
    [Version(1, 0, 1, "Beta build")]
    [Version(1, 0, 2, "Rotational cropping implemented")]
    [HelpUri(@"Content/Features/Activities/Crop/ManageCrop.htm")]
    public class CropActivityManageCrop: CLEMActivityBase, IValidatableObject, IPastureManager
    {
        [Link]
        private Clock clock = null;

        private bool gotLandRequested = false; //was this crop able to get the land it requested ?
        private int currentCropIndex = 0;

        /// <summary>
        /// Land type where crop is to be grown
        /// </summary>
        [Description("Land type where crop is to be grown")]
        [Core.Display(Type = DisplayType.DropDown, Values = "GetResourcesAvailableByName", ValuesArgs = new object[] { new Type[] { typeof(Land) } })]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Land resource type required")]
        public string LandItemNameToUse { get; set; }

        /// <summary>
        /// Area of land requested
        /// </summary>
        [Description("Area of crop")]
        [Required, GreaterThanEqualValue(0)]
        public double AreaRequested { get; set; }

        /// <summary>
        /// Use unallocated available
        /// </summary>
        [Description("Use unallocated land")]
        public bool UseAreaAvailable { get; set; }
        
        /// <summary>
        /// Area of land actually received (maybe less than requested)
        /// </summary>
        [JsonIgnore]
        public double Area { get; set; }

        /// <summary>
        /// Land item
        /// </summary>
        [JsonIgnore]
        public LandType LinkedLandItem { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public CropActivityManageCrop()
        {
            base.ModelSummaryStyle = HTMLSummaryStyle.SubActivityLevel2;
            TransactionCategory = "Crop";
        }

        /// <summary>An event handler to allow us to initialise</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            if (LandItemNameToUse != null && LandItemNameToUse != "")
            {
                // locate Land Type resource for this forage.
                LinkedLandItem = Resources.FindResourceType<Land, LandType>(this, LandItemNameToUse, OnMissingResourceActionTypes.ReportErrorAndStop, OnMissingResourceActionTypes.ReportErrorAndStop);

                if (UseAreaAvailable)
                    LinkedLandItem.TransactionOccurred += LinkedLandItem_TransactionOccurred;

                ResourceRequestList = new List<ResourceRequest>
                {
                new ResourceRequest()
                {
                    Resource = LinkedLandItem,
                    AllowTransmutation = false,
                    Required = UseAreaAvailable ? LinkedLandItem.AreaAvailable : AreaRequested,
                    ResourceType = typeof(Land),
                    ResourceTypeName = LandItemNameToUse,
                    ActivityModel = this,
                    Category = TransactionCategory,
                    FilterDetails = null
                }
                };

                CheckResources(ResourceRequestList, Guid.NewGuid());
                gotLandRequested = TakeResources(ResourceRequestList, false);

                //Now the Land has been allocated we have an Area 
                if (gotLandRequested)
                    //Assign the area actually got after taking it. It might be less than AreaRequested (if partial)
                    Area = ResourceRequestList.FirstOrDefault().Provided;
            }
            // set and enable first crop in the list for rotational cropping.
            int i = 0;
            foreach (var item in this.Children.OfType<CropActivityManageProduct>())
            {
                item.ActivityEnabled = (i == currentCropIndex);
                item.FirstTimeStepOfRotation = clock.StartDate.Year*100 + clock.StartDate.Month;
                i++;
            }
        }

        /// <summary>An event handler to allow us to make checks after resources and activities initialised.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMFinalSetupBeforeSimulation")]
        private void OnCLEMFinalSetupBeforeSimulation(object sender, EventArgs e)
        {
            if (Area == 0 && UseAreaAvailable)
                Summary.WriteWarning(this, String.Format("No area of [r={0}] has been assigned for [a={1}] at the start of the simulation.\r\nThis is because you have selected to use unallocated land and all land is used by other activities.", LinkedLandItem.Name, this.Name));
        }

        /// <summary>
        /// Method to rotate to the next crop in the list
        /// </summary>
        public void RotateCrop()
        {
            int numberCrops = this.Children.OfType<CropActivityManageProduct>().Count();
            if (numberCrops>1)
            {
                currentCropIndex++;
                if (currentCropIndex >= numberCrops)
                    currentCropIndex = 0;

                int i = 0;
                foreach (var item in this.Children.OfType<CropActivityManageProduct>())
                {
                    item.ActivityEnabled = (i == currentCropIndex);
                    if (item.ActivityEnabled)
                        item.FirstTimeStepOfRotation = item.FirstTimeStepOfRotation = clock.Today.AddDays(1).Year * 100 + clock.Today.AddDays(1).Month;
                    else
                        item.FirstTimeStepOfRotation = 0;
                    i++;
                }
            }
        }

        /// <summary>
        /// Overrides the base class method to allow for clean up
        /// </summary>
        [EventSubscribe("Completed")]
        private void OnSimulationCompleted(object sender, EventArgs e)
        {
            if (LinkedLandItem != null && UseAreaAvailable)
                LinkedLandItem.TransactionOccurred -= LinkedLandItem_TransactionOccurred;
        }

        // Method to listen for land use transactions 
        // This allows this activity to dynamically respond when use available area is selected
        private void LinkedLandItem_TransactionOccurred(object sender, EventArgs e)
        {
            Area = LinkedLandItem.AreaAvailable;
        }

        /// <inheritdoc/>
        public override void DoActivity()
        {
            Status = ActivityStatus.NoTask;
            return;
        }

        #region validation

        /// <summary>
        /// Validate model
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            // check that this activity contains at least one CollectProduct activity
            if (this.Children.OfType<CropActivityManageProduct>().Count() == 0)
            {
                string[] memberNames = new string[] { "Collect product activity" };
                results.Add(new ValidationResult("At least one [a=CropActivityManageProduct] activity must be present under this manage crop activity", memberNames));
            }
            return results;
        }
        #endregion

        #region descriptive summary

        /// <inheritdoc/>
        public override string ModelSummary(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                htmlWriter.Write("\r\n<div class=\"activityentry\">This crop uses ");

                Land parentLand = null;
                IModel clemParent = FindAncestor<ZoneCLEM>();
                if (LandItemNameToUse != null && LandItemNameToUse != "")
                    if (clemParent != null && clemParent.Enabled)
                        parentLand = clemParent.FindInScope(LandItemNameToUse.Split('.')[0]) as Land;

                if (UseAreaAvailable)
                    htmlWriter.Write("the unallocated portion of ");
                else
                {
                    if (parentLand == null)
                        htmlWriter.Write("<span class=\"setvalue\">" + AreaRequested.ToString("0.###") + "</span> <span class=\"errorlink\">[UNITS NOT SET]</span> of ");
                    else
                        htmlWriter.Write("<span class=\"setvalue\">" + AreaRequested.ToString("0.###") + "</span> " + parentLand.UnitsOfArea + " of ");
                }
                if (LandItemNameToUse == null || LandItemNameToUse == "")
                    htmlWriter.Write("<span class=\"errorlink\">[LAND NOT SET]</span>");
                else
                    htmlWriter.Write("<span class=\"resourcelink\">" + LandItemNameToUse + "</span>");
                htmlWriter.Write("</div>");
                return htmlWriter.ToString(); 
            }
        }

        /// <inheritdoc/>
        public override string ModelSummaryInnerClosingTags(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                if (this.FindAllChildren<CropActivityManageProduct>().Count() > 0)
                    htmlWriter.Write("\r\n</div>");
                return htmlWriter.ToString(); 
            }
        }

        /// <inheritdoc/>
        public override string ModelSummaryInnerOpeningTags(bool formatForParentControl)
        {
            using (StringWriter htmlWriter = new StringWriter())
            {
                if (this.FindAllChildren<CropActivityManageProduct>().Count() == 0)
                {
                    htmlWriter.Write("\r\n<div class=\"errorbanner clearfix\">");
                    htmlWriter.Write("<div class=\"filtererror\">No Crop Activity Manage Product component provided</div>");
                    htmlWriter.Write("</div>");
                }
                else
                {
                    bool rotation = this.FindAllChildren<CropActivityManageProduct>().Count() > 1;
                    if (rotation)
                        htmlWriter.Write("\r\n<div class=\"croprotationlabel\">Rotating through crops</div>");
                    htmlWriter.Write("\r\n<div class=\"croprotationborder\">");
                }
                return htmlWriter.ToString(); 
            }
        } 
        #endregion
    }
}
